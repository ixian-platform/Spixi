using IXICore;
using IXICore.Meta;
using Newtonsoft.Json;
using SPIXI.MiniApps.ActionRequestModels;
using SPIXI.MiniApps.ActionResponseModels;
using IXICore.Utils;
using static IXICore.Transaction;
using IXICore.RegNames;
using System.Text;
using IXICore.Network;
using IXICore.Streaming;

namespace SPIXI.MiniApps
{
    public static class MiniAppCommands
    {
        public const string AUTH = "auth";
        public const string NAME_REGISTER = "registerName";
        public const string NAME_UPDATE = "updateName";
        public const string NAME_EXTEND = "extendName";
        public const string NAME_UPDATE_CAPACITY = "updateCapacity";
        public const string NAME_ALLOW_SUBNAMES = "allowSubnames";
        public const string NAME_TRANSFER = "transferName";
        public const string SEND_PAYMENT = "sendPayment";

        public const string NETWORK_DATA_SEND = "ds";
        public const string STORAGE_GET = "getStorage";
        public const string STORAGE_SET = "setStorage";
    }

    public class MiniAppActionHandler
    {
        private string appId;
        private byte[] sessionId;

        private Address localUserAddress;
        private Address[] remoteUserAddresses;

        private int sdkVersion = 40;

        private MiniAppStorage miniAppStorage;

        public MiniAppActionHandler(
            string appId,
            byte[] sessionId,
            Address localUserAddress,
            Address[] remoteUserAddresses,
            int sdkVersion,
            MiniAppStorage miniAppStorage)
        {
            this.appId = appId;
            this.sessionId = sessionId;
            this.localUserAddress = localUserAddress;
            this.remoteUserAddresses = remoteUserAddresses;
            this.sdkVersion = sdkVersion;
            this.miniAppStorage = miniAppStorage;
        }

        public string? processAction(string command, string actionData)
        {
            string? resp = null;
            switch (command)
            {
                case MiniAppCommands.AUTH:
                    resp = processAuth(actionData);
                    break;

                case MiniAppCommands.NAME_REGISTER:
                    resp = processRegisterName(actionData);
                    break;

                case MiniAppCommands.NAME_UPDATE:
                    resp = processUpdateName(actionData);
                    break;

                case MiniAppCommands.NAME_EXTEND:
                    resp = processExtendName(actionData);
                    break;

                case MiniAppCommands.NAME_UPDATE_CAPACITY:
                    resp = processUpdateCapacity(actionData);
                    break;

                /*case MiniAppCommands.NAME_ALLOW_SUBNAMES:
                    resp = processAllowSubnames(actionData);
                    break;

                case MiniAppCommands.NAME_TRANSFER:
                    resp = processTransferName(actionData);
                    break;*/

                case MiniAppCommands.SEND_PAYMENT:
                    resp = processSendPayment(actionData, NetworkClientManager.getRandomConnectedClientAddresses(3));
                    break;

                case MiniAppCommands.NETWORK_DATA_SEND:
                    resp = processDataSend(actionData);
                    break;

                case MiniAppCommands.STORAGE_GET:
                    resp = processStorageGet(actionData);
                    break;

                case MiniAppCommands.STORAGE_SET:
                    resp = processStorageSet(actionData);
                    break;
            }
            return resp;
        }

        public string processDataSend(string data)
        {
            NetworkDataSendAction? ndsAction = JsonConvert.DeserializeObject<NetworkDataSendAction>(data);
            byte[] d = UTF8Encoding.UTF8.GetBytes(ndsAction.d);
            string? pid = ndsAction.pid;
            Address? recipient = ndsAction.r;
            Address[] sendTo = remoteUserAddresses;

            byte[]? protocolIdBytes = null;
            if (!string.IsNullOrEmpty(pid))
            {
                protocolIdBytes = CryptoManager.lib.sha3_512Trunc(UTF8Encoding.UTF8.GetBytes(pid));
            }

            if (recipient != null)
            {
                sendTo = [recipient];
            }

            foreach (Address address in sendTo)
            {
                Friend f = FriendList.getFriend(address);
                if (f != null)
                {
                    // Send as normal app data to SessionID or as protocol data if ProtocolID is specified.
                    if (protocolIdBytes != null)
                        StreamProcessor.sendAppProtocolData(f, protocolIdBytes, d);
                    else
                        StreamProcessor.sendAppData(f, sessionId, d);
                }
                else
                {
                    Logging.error("Friend {0} does not exist in the friend list.", address.ToString());
                }
            }

            return "";
        }

        public string processStorageGet(string data)
        {
            StorageGetAction? sga = JsonConvert.DeserializeObject<StorageGetAction>(data);
            string table = sga.t;
            string key = sga.k;
            byte[]? value = miniAppStorage.getStorageData(appId, table, key);
            string strValue = "null";
            if (value != null)
            {
                strValue = Convert.ToBase64String(value);
            }
            MiniAppActionResponse resp = new MiniAppActionResponse()
            {
                r = strValue,
                id = sga.id
            };
            return JsonConvert.SerializeObject(resp);
        }

        public string processStorageSet(string data)
        {
            StorageSetAction? ssa = JsonConvert.DeserializeObject<StorageSetAction>(data);
            string table = ssa.t;
            string key = ssa.k;
            string value = ssa.v;
            miniAppStorage.setStorageData(appId, table, key, Convert.FromBase64String(value));
            MiniAppActionResponse resp = new MiniAppActionResponse()
            {
                r = value,
                id = ssa.id
            };
            return JsonConvert.SerializeObject(resp);
        }

        public static string processAuth(string authData)
        {
            AuthAction? authAction = JsonConvert.DeserializeObject<AuthAction>(authData);
            byte[] pubKey = IxianHandler.getWalletStorage().getPrimaryPublicKey();

            var serviceChallengeBytes = Crypto.stringToHash(authAction.data.challenge);
            var randomBytes = CryptoManager.lib.getSecureRandomBytes(64);
            var finalChallenge = new byte[ConsensusConfig.ixianChecksumLock.Length + randomBytes.Length + serviceChallengeBytes.Length];
            Buffer.BlockCopy(ConsensusConfig.ixianChecksumLock, 0, finalChallenge, 0, ConsensusConfig.ixianChecksumLock.Length);
            Buffer.BlockCopy(serviceChallengeBytes, 0, finalChallenge, ConsensusConfig.ixianChecksumLock.Length, serviceChallengeBytes.Length);
            Buffer.BlockCopy(randomBytes, 0, finalChallenge, ConsensusConfig.ixianChecksumLock.Length + serviceChallengeBytes.Length, randomBytes.Length);

            byte[] sig = CryptoManager.lib.getSignature(finalChallenge, IxianHandler.getWalletStorage().getPrimaryPrivateKey());
            AuthResponse authResponse = new AuthResponse()
            {
                challenge = Crypto.hashToString(finalChallenge),
                publicKey = Crypto.hashToString(pubKey),
                signature = Crypto.hashToString(sig),
                id = authAction.data.challenge
            };

            return JsonConvert.SerializeObject(authResponse);
        }

        private static Transaction createRegNameTransaction(ToEntry toEntry, Address feeRecipient, IxiNumber recipientFeeAmount)
        {
            IxiNumber fee = ConsensusConfig.forceTransactionPrice;
            Address from = IxianHandler.getWalletStorage().getPrimaryAddress();
            Address pubKey = new Address(IxianHandler.getWalletStorage().getPrimaryPublicKey());

            var toList = new Dictionary<Address, ToEntry>(new AddressComparer())
            {
                { ConsensusConfig.rnRewardPoolAddress, toEntry }
            };

            if (feeRecipient != null)
            {
                toList.Add(feeRecipient, new ToEntry(Transaction.maxVersion, recipientFeeAmount));
            }

            Transaction tx = new Transaction((int)Transaction.Type.RegName, fee, toList, from, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());

            return tx;
        }

        public static string processRegisterName(string nameData)
        {
            RegNameAction<RegNameRegisterAction>? nameAction = JsonConvert.DeserializeObject<RegNameAction<RegNameRegisterAction>>(nameData);
            var nad = nameAction.data;
            Address pubKey = new Address(IxianHandler.getWalletStorage().getPrimaryPublicKey());
            IxiNumber regFee = RegisteredNamesTransactions.calculateExpectedRegistrationFee(nad.registrationTimeInBlocks, nad.capacity);
            var toEntry = RegisteredNamesTransactions.createRegisterToEntry(nad.name,
                nad.registrationTimeInBlocks,
                nad.capacity,
                pubKey,
                nad.recoveryHash != null ? nad.recoveryHash : pubKey,
                regFee);
            Transaction tx = createRegNameTransaction(toEntry, nameAction.feeRecipientAddress, nameAction.feeAmount);
            TransactionResponse txResponse = new TransactionResponse()
            {
                tx = Convert.ToBase64String(tx.getBytes()),
                txid = tx.getTxIdString(),
                id = nameAction.id
            };
            return JsonConvert.SerializeObject(txResponse);
        }

        public static string processUpdateName(string nameData)
        {
            RegNameAction<RegNameUpdateRecordsAction>? nameAction = JsonConvert.DeserializeObject<RegNameAction<RegNameUpdateRecordsAction>>(nameData);
            var nad = nameAction.data;
            var narnr = nameAction.nameRecord;
            narnr.dataRecords.AddRange(nameAction.nameDataRecords);

            Address pubKey = new Address(IxianHandler.getWalletStorage(narnr.nextPkHash).getPrimaryPublicKey());

            List<RegisteredNameDataRecord> dataRecords = new();
            foreach (var record in nad.records)
            {
                byte[]? name = null;
                if (record.name != null)
                {
                    name = IxiNameUtils.encodeAndHashIxiNameRecordKey(narnr.name, record.name);
                }
                
                int ttl = record.ttl;
                
                byte[]? data = null;
                if (record.data != null)
                {
                    data = IxiNameUtils.encryptRecord(UTF8Encoding.UTF8.GetBytes(nad.decodedName), UTF8Encoding.UTF8.GetBytes(record.name), UTF8Encoding.UTF8.GetBytes(record.data));
                }

                byte[]? checksum = null;
                if (record.checksum != null)
                {
                    checksum = record.checksum;
                }
                dataRecords.Add(new RegisteredNameDataRecord(name, ttl, data, checksum));
            }

            var newChecksum = RegisteredNamesTransactions.calculateRegNameChecksumFromUpdatedDataRecords(narnr, IxiNameUtils.encodeAndHashIxiName(nad.decodedName), dataRecords, narnr.sequence + 1, pubKey);
            byte[] sig = CryptoManager.lib.getSignature(newChecksum, IxianHandler.getWalletStorage(narnr.nextPkHash).getPrimaryPrivateKey());

            var toEntry = RegisteredNamesTransactions.createUpdateRecordToEntry(nad.name,
                dataRecords,
                narnr.sequence,
                pubKey,
                pubKey,
                sig);

            Transaction tx = createRegNameTransaction(toEntry, nameAction.feeRecipientAddress, nameAction.feeAmount);
            TransactionResponse txResponse = new TransactionResponse()
            {
                tx = Convert.ToBase64String(tx.getBytes()),
                txid = tx.getTxIdString(),
                id = nameAction.id
            };
            return JsonConvert.SerializeObject(txResponse);
        }

        public static string processExtendName(string nameData)
        {
            RegNameAction<RegNameExtendAction>? nameAction = JsonConvert.DeserializeObject<RegNameAction<RegNameExtendAction>>(nameData);
            var nad = nameAction.data;
            var narnr = nameAction.nameRecord;
            Address pubKey = new Address(IxianHandler.getWalletStorage().getPrimaryPublicKey());
            IxiNumber extFee = RegisteredNamesTransactions.calculateExpectedRegistrationFee(nad.extensionTimeInBlocks, narnr.capacity);
            var toEntry = RegisteredNamesTransactions.createExtendToEntry(nad.name,
                nad.extensionTimeInBlocks,
                extFee);
            Transaction tx = createRegNameTransaction(toEntry, nameAction.feeRecipientAddress, nameAction.feeAmount);
            TransactionResponse txResponse = new TransactionResponse()
            {
                tx = Convert.ToBase64String(tx.getBytes()),
                txid = tx.getTxIdString(),
                id = nameAction.id
            };
            return JsonConvert.SerializeObject(txResponse);
        }

        public static string processUpdateCapacity(string nameData)
        {
            RegNameAction<RegNameChangeCapacityAction>? nameAction = JsonConvert.DeserializeObject<RegNameAction<RegNameChangeCapacityAction>>(nameData);
            var nad = nameAction.data;
            var narnr = nameAction.nameRecord;

            Address pubKey = new Address(IxianHandler.getWalletStorage(narnr.nextPkHash).getPrimaryPublicKey());

            narnr.setCapacity(nad.newCapacity, narnr.sequence + 1, nad.nextPkHash, null, null, 0);
            var newChecksum = narnr.calculateChecksum();
            byte[] sig = CryptoManager.lib.getSignature(newChecksum, IxianHandler.getWalletStorage(narnr.nextPkHash).getPrimaryPrivateKey());

            IxiNumber updFee = RegisteredNamesTransactions.calculateExpectedRegistrationFee(narnr.expirationBlockHeight - IxianHandler.getHighestKnownNetworkBlockHeight(), narnr.capacity);

            var toEntry = RegisteredNamesTransactions.createChangeCapacityToEntry(nad.name,
                nad.newCapacity,
                narnr.sequence,
                pubKey,
                pubKey,
                sig,
                updFee);
            Transaction tx = createRegNameTransaction(toEntry, nameAction.feeRecipientAddress, nameAction.feeAmount);
            TransactionResponse txResponse = new TransactionResponse()
            {
                tx = Convert.ToBase64String(tx.getBytes()),
                txid = tx.getTxIdString(),
                id = nameAction.id
            };
            return JsonConvert.SerializeObject(txResponse);
        }

        public static string processSendPayment(string paymentData, List<Address> relayPeers)
        {
            SendPayment? spa = JsonConvert.DeserializeObject<SendPayment>(paymentData);
            var recipients = spa.recipients;

            IxiNumber feePerKb = ConsensusConfig.forceTransactionPrice;
            IxiNumber fee = feePerKb;
            Address from = IxianHandler.getWalletStorage().getPrimaryAddress();
            Address pubKey = new Address(IxianHandler.getWalletStorage().getPrimaryPublicKey());

            var toList = new Dictionary<Address, ToEntry>(new AddressComparer());

            foreach (var recipient in recipients)
            {
                toList.Add(recipient.Key, new ToEntry(Transaction.maxVersion, recipient.Value));
            }

            foreach (var peer in relayPeers)
            {
                toList.Add(peer, new ToEntry(Transaction.maxVersion, fee));
            }

            Transaction tx = new Transaction((int)Transaction.Type.Normal, feePerKb, toList, from, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());
            while (tx.fee != fee)
            {
                fee = tx.fee;
                foreach (var peer in relayPeers)
                {
                    toList[peer].amount = fee;
                }
                tx = new Transaction((int)Transaction.Type.Normal, feePerKb, toList, from, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());
            }

            TransactionResponse txResponse = new TransactionResponse()
            {
                tx = Convert.ToBase64String(tx.getBytes()),
                txid = tx.getTxIdString(),
                id = spa.id
            };
            return JsonConvert.SerializeObject(txResponse);
        }
    }
}
