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

namespace SPIXI.MiniApps
{
    public static class MiniAppCommands
    {
        public const string AUTH = "auth";
        public const string REGISTER_NAME = "registerName";
        public const string UPDATE_NAME = "updateName";
        public const string EXTEND_NAME = "extendName";
        public const string UPDATE_CAPACITY = "updateCapacity";
        public const string ALLOW_SUBNAMES = "allowSubnames";
        public const string TRANSFER_NAME = "transferName";
        public const string SEND_PAYMENT = "sendPayment";
    }

    public static class MiniAppActionHandler
    {
        public static string? processAction(string command, string actionData)
        {
            string? resp = null;
            switch (command)
            {
                case MiniAppCommands.AUTH:
                    resp = processAuth(actionData);
                    break;

                case MiniAppCommands.REGISTER_NAME:
                    resp = processRegisterName(actionData);
                    break;

                case MiniAppCommands.UPDATE_NAME:
                    resp = processUpdateName(actionData);
                    break;

                case MiniAppCommands.EXTEND_NAME:
                    resp = processExtendName(actionData);
                    break;

                case MiniAppCommands.UPDATE_CAPACITY:
                    resp = processUpdateCapacity(actionData);
                    break;

                /*case MiniAppCommands.ALLOW_SUBNAMES:
                    resp = processAllowSubnames(actionData);
                    break;

                case MiniAppCommands.TRANSFER_NAME:
                    resp = processTransferName(actionData);
                    break;*/

                case MiniAppCommands.SEND_PAYMENT:
                    resp = processSendPayment(actionData, NetworkClientManager.getRandomConnectedClientAddresses(3));
                    break;
            }
            return resp;
        }

        public static string processAuth(string authData)
        {
            AuthAction authAction = JsonConvert.DeserializeObject<AuthAction>(authData);
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
                requestId = authAction.data.challenge
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
            RegNameAction<RegNameRegisterAction> nameAction = JsonConvert.DeserializeObject<RegNameAction<RegNameRegisterAction>>(nameData);
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
                tx = Crypto.hashToString(tx.getBytes()),
                requestId = nameAction.requestId
            };
            return JsonConvert.SerializeObject(txResponse);
        }

        public static string processUpdateName(string nameData)
        {
            RegNameAction<RegNameUpdateRecordsAction> nameAction = JsonConvert.DeserializeObject<RegNameAction<RegNameUpdateRecordsAction>>(nameData);
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
                tx = Crypto.hashToString(tx.getBytes()),
                requestId = nameAction.requestId
            };
            return JsonConvert.SerializeObject(txResponse);
        }

        public static string processExtendName(string nameData)
        {
            RegNameAction<RegNameExtendAction> nameAction = JsonConvert.DeserializeObject<RegNameAction<RegNameExtendAction>>(nameData);
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
                tx = Crypto.hashToString(tx.getBytes()),
                requestId = nameAction.requestId
            };
            return JsonConvert.SerializeObject(txResponse);
        }

        public static string processUpdateCapacity(string nameData)
        {
            RegNameAction<RegNameChangeCapacityAction> nameAction = JsonConvert.DeserializeObject<RegNameAction<RegNameChangeCapacityAction>>(nameData);
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
                tx = Crypto.hashToString(tx.getBytes()),
                requestId = nameAction.requestId
            };
            return JsonConvert.SerializeObject(txResponse);
        }

        /*public static string processAllowSubnames(string nameData)
        {
            RegNameAction nameAction = JsonConvert.DeserializeObject<RegNameAction>(nameData);
            TransactionResponse txResponse = new TransactionResponse()
            {
                tx = ""
            };
            return JsonConvert.SerializeObject(txResponse);
        }

        public static string processTransferName(string nameData)
        {
            RegNameAction nameAction = JsonConvert.DeserializeObject<RegNameAction>(nameData);
            TransactionResponse txResponse = new TransactionResponse()
            {
                tx = ""
            };
            return JsonConvert.SerializeObject(txResponse);
        }*/

        public static string processSendPayment(string paymentData, List<Address> relayPeers)
        {
            SendPayment sendTx = JsonConvert.DeserializeObject<SendPayment>(paymentData);
            var recipients = sendTx.recipients;

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
                tx = Crypto.hashToString(tx.getBytes()),
                requestId = sendTx.requestId
            };
            return JsonConvert.SerializeObject(txResponse);
        }
    }
}
