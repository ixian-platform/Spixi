using Force.Crc32;
using IXICore;
using IXICore.Activity;
using IXICore.Inventory;
using IXICore.Meta;
using IXICore.Network;
using IXICore.RegNames;
using IXICore.Storage;
using IXICore.Streaming;
using IXICore.Utils;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spixi;
using SPIXI.MiniApps;
using SPIXI.Network;
using SPIXI.VoIP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static IXICore.Transaction;

namespace SPIXI.Meta
{
    class Node : IxianNode
    {
        // Used to force reloading of some homescreen elements
        public static bool changedSettings = false;

        public static IxiNumber fiatPrice = 0;  // Stores the last known ixi fiat value

        public static int startCounter = 0;

        public static TransactionInclusion tiv = null;

        public static MiniAppManager MiniAppManager = null;
        public static MiniAppStorage MiniAppStorage = null;

        public static StreamProcessor streamProcessor = null;

        public static NetworkClientManagerStatic networkClientManagerStatic = null;

        public static IActivityStorage activityStorage = null;

        public static IStorage storage = null;

        // Private data

        private static CancellationTokenSource? ctsLoop;
        private static Task? mainLoopTask;

        public static Node Instance = null;

        private static bool running = false;

        private static long lastPriceUpdate = 0;

        private static GenericAPIServer? apiServer = null;

        private static object startLock = new object();

        public Node()
        {
            if (Instance != null)
            {
                throw new Exception("Node instance already exists!");
            }
            Logging.info("Initing node constructor");
            Instance = this;

            IxianHandler.init(Config.version, this, Config.networkType, false, Config.checksumLock);

            // Initialize storage
            storage = new RocksDBStorage(Config.headersFolderPath, Config.blocksDbCacheSize, CoreConfig.maxBlockHeadersPerDatabase, 3, RocksDBOptimizations.Mobiles, Config.minRequiredDiskSpace);

            activityStorage = new ActivityStorage(Config.activityFolderPath, Config.activityDbCacheSize, 0, RocksDBOptimizations.Mobiles, Config.minRequiredDiskSpace);

            PeerStorage.init(Config.spixiUserFolder);

            // Network configuration
            networkClientManagerStatic = new NetworkClientManagerStatic(Config.maxRelaySectorNodesToConnectTo);
            NetworkClientManager.init(networkClientManagerStatic);
            StreamClientManager.init(Config.maxConnectedStreamingNodes, true);

            // Prepare the stream processor
            StreamCapabilities caps = StreamCapabilities.Incoming | StreamCapabilities.Outgoing | StreamCapabilities.IPN | StreamCapabilities.Apps;
            streamProcessor = new StreamProcessor(new SpixiPendingMessageProcessor(Config.spixiUserFolder, Config.enablePushNotifications), caps);
            
            // Init TIV
            tiv = new TransactionInclusion(storage, new SpixiTransactionInclusionCallbacks(), TIVBlockVerificationMode.Minimal);

            Logging.info("Initing local storage");

            // Prepare the local storage
            IxianHandler.localStorage = new LocalStorage(Config.spixiUserFolder, new SpixiLocalStorageCallbacks());

            MiniAppManager = new MiniAppManager(Config.spixiUserFolder);
            MiniAppStorage = new MiniAppStorage(Config.spixiUserFolder);

            FriendList.init(Config.spixiUserFolder, true);

            UpdateVerify.init(Config.checkVersionUrl, Config.checkVersionSeconds);

            OfflinePushMessages.init(Config.pushServiceUrl, streamProcessor);

            string backup_file_name = Path.Combine(Config.spixiUserFolder, "spixi.account.backup.ixi");
            if (File.Exists(backup_file_name))
            {
                File.Delete(backup_file_name);
            }

            InventoryCache.init(new InventoryCacheClient(tiv));

            RelaySectors.init(CoreConfig.relaySectorLevels, null);

            Logging.info("Node init done");
        }

        static public void preStart()
        {
            lock (startLock)
            {
                if (running)
                {
                    return;
                }
                Logging.info("Pre-Starting node");
                // Start local storage
                IxianHandler.localStorage.start();

                FriendList.loadContacts();
            }
        }

        static public bool start()
        {
            lock (startLock)
            {
                if (running)
                {
                    Logging.warn("Cannot start Node, it is already running.");
                    return false;
                }
                Logging.info("Starting node");

                running = true;
                IxianHandler.status = NodeStatus.warmUp;

                UpdateVerify.start();

                if (!storage.prepareStorage(false))
                {
                    Logging.error("Error while preparing block storage! Aborting.");
                    return false;
                }

                activityStorage.prepareStorage(false);

                var pending_txs = activityStorage.getActivitiesByStatus(ActivityStatus.Pending, true);
                pending_txs.AddRange(activityStorage.getActivitiesByStatus(ActivityStatus.Reverted, true));
                // Load pending transactions
                foreach (var pending_tx in pending_txs)
                {
                    if (pending_tx.type == ActivityType.TransactionReceived)
                    {
                        PendingTransactions.addIncomingTransaction(pending_tx.transaction);
                    }
                    else if (pending_tx.type == ActivityType.TransactionSent
                            || pending_tx.type == ActivityType.IxiName)
                    {
                        PendingTransactions.addOutgoingTransaction(pending_tx.transaction, pending_tx.transaction.toList.TakeLast(2).Select(x => x.Key).ToList());
                    }
                }

                ulong block_height = 0;
                byte[]? block_checksum = null;
                if (IxianHandler.networkType == NetworkType.main)
                {
                    block_height = CoreConfig.bakedBlockHeight;
                    block_checksum = CoreConfig.bakedBlockChecksum;
                }

                // Start TIV
                tiv.start(block_height, block_checksum, true);

                // Generate presence list
                PresenceList.init(IxianHandler.publicIP, 0, 'C', CoreConfig.clientKeepAliveInterval);

                // Start the network queue
                NetworkQueue.start();

                streamProcessor.start();

                // Start the keepalive thread
                PresenceList.startKeepAlive();

                // Start the transfer manager
                TransferManager.start();

                MiniAppManager.start();

                startCounter++;

                // Init push service
                SPushService.initialize();

                string tag = IxianHandler.getWalletStorage().getPrimaryAddress().ToString();
                SPushService.setTag(tag);

                resume();

                if (Config.apiBinds.Count != 0)
                {
                    apiServer = new GenericAPIServer();
                    apiServer.start(Config.apiBinds, Config.apiUsers, Config.apiAllowedIps, activityStorage);
                }

                Logging.info("Node started");

                return true;
            }
        }


        // Checks for existing wallet file. Can also be used to handle wallet/account upgrading in the future.
        // Returns true if found, otherwise false.
        static public bool checkForExistingWallet()
        {
            if (!File.Exists(Path.Combine(Config.spixiUserFolder, Config.walletFile)))
            {
                Logging.log(LogSeverity.error, "Cannot read wallet file.");
                return false;
            }

            return true;
        }

        static public bool loadWallet()
        {
            if (Preferences.Default.ContainsKey("walletpass") == false)
                return false;

            // TODO: decrypt the password
            string password = Preferences.Default.Get("walletpass", "").ToString();


            WalletStorage walletStorage = new WalletStorage(Path.Combine(Config.spixiUserFolder, Config.walletFile));
            if (walletStorage.readWallet(password))
            {
                IxianHandler.addWallet(walletStorage);

                // Prepare the balances list
                List<Address> address_list = IxianHandler.getWalletStorage().getMyAddresses();
                foreach (Address addr in address_list)
                {
                    IxianHandler.balances.Add(addr, new Balance(addr, 0));
                }

                return true;
            }
            return false;
        }

        static public bool generateWallet(string pass)
        {
            if (IxianHandler.getWalletList().Count == 0)
            {
                WalletStorage ws = new WalletStorage(Path.Combine(Config.spixiUserFolder, Config.walletFile));
                if (ws.generateWallet(pass))
                {
                    return IxianHandler.addWallet(ws);
                }
            }
            return false;
        }

        static public void connectToNetwork()
        {
            // Start the s2 client manager
            StreamClientManager.start();

            // Start the network client manager
            NetworkClientManager.start(2);
        }

        private static void connectToBotNodes()
        {
            List<Friend> bot_list = null;
            lock (FriendList.friends)
            {
                bot_list = FriendList.friends.FindAll(x => x.bot);
            }
            foreach (var bot_entry in bot_list)
            {
                if (Clock.getNetworkTimestamp() - bot_entry.updatedStreamingNodes < CoreConfig.clientPresenceExpiration
                    && bot_entry.relayNode != null)
                {
                    StreamClientManager.connectTo(bot_entry.relayNode.hostname, bot_entry.walletAddress);
                } else
                {
                    CoreStreamProcessor.fetchFriendsPresence(bot_entry);
                }
            }
        }

        // Handle timer routines
        static public async void mainLoop(CancellationToken ct)
        {
            try
            {
                bool fireLocalNotification = OperatingSystem.IsAndroid();
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        PeerStorage.savePeersFile();

                        if (Config.enablePushNotifications)
                        {
                            OfflinePushMessages.fetchPushMessages(false, fireLocalNotification, false);
                            fireLocalNotification = false;
                        }

                        // Update the friendlist
                        updateFriendStatuses();

                        // Cleanup the presence list
                        // TODO: optimize this by using a different thread perhaps
                        PresenceList.performCleanup();

                        bool firstBalance = true;
                        foreach (var balance in IxianHandler.balances.Values)
                        {
                            // Request initial wallet balance
                            if (balance.blockHeight == 0 || balance.lastUpdate + 300 < Clock.getTimestamp())
                            {
                                CoreProtocolMessage.broadcastProtocolMessage(['M', 'H', 'R'], ProtocolMessageCode.getBalance2, balance.address.addressNoChecksum.GetIxiBytes(), null);

                                if (firstBalance)
                                {
                                    CoreProtocolMessage.fetchSectorNodes(IxianHandler.primaryWalletAddress, CoreConfig.maxRelaySectorNodesToRequest);
                                    //ProtocolMessage.fetchAllFriendsSectorNodes(10);
                                    //StreamProcessor.fetchAllFriendsPresences(10);
                                }
                            }
                            firstBalance = false;
                        }

                        // Check price if enough time passed
                        if (lastPriceUpdate + Config.checkPriceSeconds < Clock.getTimestamp())
                        {
                            updateIxiPrice();
                        }

                        connectToBotNodes();

                        if (VoIPManager.currentCallStartedTime == 0
                            && VoIPManager.currentCallInitiated != 0
                            && Clock.getTimestamp() - VoIPManager.currentCallInitiated > 60)
                        {
                            VoIPManager.hangupCall(null, true);
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.error("Exception occured in mainLoop: " + e);
                    }
                    await Task.Delay(2500, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            catch (Exception e)
            {
                Logging.error("Exception occured in mainLoop: " + e);
            }
        }

        static public void updateFriendStatuses()
        {
            lock (FriendList.friends)
            {
                // Go through each friend and check for the pubkey in the PL
                foreach (Friend friend in FriendList.friends)
                {
                    Presence? presence = null;

                    try
                    {
                        presence = PresenceList.getPresenceByAddress(friend.walletAddress);
                    }
                    catch (Exception e)
                    {
                        Logging.error("Presence Error {0}", e.Message);
                        presence = null;
                    }

                    if (presence != null)
                    {
                        if (friend.online == false
                            && friend.relayNode != null)
                        {
                            friend.online = true;
                            UIHelpers.setContactStatus(friend.walletAddress, friend.online, friend.getUnreadMessageCount(), "", 0);
                        }
                    }
                    else
                    {
                        if (friend.online == true)
                        {
                            friend.online = false;
                            UIHelpers.setContactStatus(friend.walletAddress, friend.online, friend.getUnreadMessageCount(), "", 0);
                        }
                    }
                }
            }
        }

        static private void stop()
        {
            lock (startLock)
            {
                if (!running)
                {
                    return;
                }

                Logging.info("Stopping node...");
                running = false;

                // First stop localStorage, to flush any pending chat messages to storage
                // The Node is currently in shutting down state, so no incoming messages will be processed by the message processors
                IxianHandler.localStorage.stop();

                // Stop the stream processor, it includes pending messages
                streamProcessor.stop();

                // Stop everything else storage related
                MiniAppManager.stop();

                activityStorage.stopStorage();

                TransferManager.stop();

                PeerStorage.savePeersFile(true);

                // Stop the block storage
                storage.stopStorage();


                // Stop everything else


                // Stop TIV
                tiv.stop();

                // Stop the keepalive thread
                PresenceList.stopKeepAlive();

                // Stop the API server
                if (apiServer != null)
                {
                    apiServer.stop();
                    apiServer = null;
                }

                // Stop everything network related
                NetworkQueue.stop();
                NetworkClientManager.stop();
                StreamClientManager.stop();

                UpdateVerify.stop();

                pause();

                Logging.info("Node stopped");
            }
        }

        public static void pause()
        {
            lock (startLock)
            {
                if (mainLoopTask == null)
                {
                    return;
                }

                IxianHandler.localStorage?.flush();
                storage?.sleep();
                activityStorage?.sleep();

                ctsLoop!.Cancel();
                try
                {
                    mainLoopTask.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    Logging.error("Error while pausing " + e);
                }
                finally
                {
                    ctsLoop.Dispose();
                    ctsLoop = null;
                    mainLoopTask = null;
                }
            }
        }

        public static void resume()
        {
            lock (startLock)
            {
                if (!running)
                {
                    return;
                }

                if (mainLoopTask != null)
                {
                    return;
                }

                ctsLoop = new CancellationTokenSource();
                mainLoopTask = Task.Run(() => mainLoop(ctsLoop.Token));
            }
        }

        public override bool isAcceptingConnections()
        {
            // TODO TODO TODO TODO implement this properly
            return false;
        }

        public override void shutdown()
        {
            HomePage.Instance()?.stop();
            stop();
        }

        public override ulong getLastBlockHeight()
        {
            Block? block = tiv.getLastBlockHeader();
            if (block == null)
            {
                return 0;
            }
            return block.blockNum;
        }

        public override int getLastBlockVersion()
        {
            Block? block = tiv.getLastBlockHeader();
            if (block == null
                || block.version < Block.maxVersion)
            {
                // TODO Omega force to v10 after upgrade
                return Block.maxVersion - 1;
            }
            return block.version;
        }

        public override bool addIncomingTransaction(Transaction tx)
        {
            if (tx.timeStamp == 0)
            {
                tx.timeStamp = Clock.getTimestamp();
            }
            if (IxianHandler.addTransactionToActivityStorage(activityStorage, tx))
            {
                UIHelpers.shouldRefreshTransactions = true;
                return PendingTransactions.addIncomingTransaction(tx);
            }
            return false;
        }

        public override bool addTransaction(Transaction tx, List<Address> relayNodeAddresses, List<ExtendedAddress>? extendedAddresses, byte[]? requestId, bool force_broadcast)
        {
            if (tx.timeStamp == 0)
            {
                tx.timeStamp = Clock.getTimestamp();
            }
            if (IxianHandler.addTransactionToActivityStorage(activityStorage, tx))
            {
                UIHelpers.shouldRefreshTransactions = true;
                if (PendingTransactions.addOutgoingTransaction(tx, relayNodeAddresses))
                {
                    foreach (var address in relayNodeAddresses)
                    {
                        NetworkClientManager.sendToClient(address, ProtocolMessageCode.transactionData2, tx.getBytes(true, true));
                    }
                    if (extendedAddresses != null)
                    {
                        CoreStreamProcessor.transactionSend(tx, extendedAddresses, requestId);
                    }
                    return true;
                }
            }
            return false;
        }

        public override Block? getLastBlock()
        {
            return tiv.getLastBlockHeader();
        }

        // Returns the current wallet's usable balance
        public static IxiNumber getAvailableBalance()
        {
            IxiNumber currentBalance = 0;
            foreach (var balance in IxianHandler.balances)
            {
                currentBalance += balance.Value.balance;
            }
            currentBalance -= PendingTransactions.getPendingSendingTransactionsAmount();

            return currentBalance;
        }

        public override void parseProtocolMessage(ProtocolMessageCode code, byte[] data, RemoteEndpoint endpoint)
        {
            ProtocolMessage.parseProtocolMessage(code, data, endpoint);
        }

        public static void onLowMemory()
        {
            IxianHandler.localStorage?.flush();
            storage?.sleep();
            activityStorage?.sleep();
            var pages = Utils.getChatPages();
            List<Address> excludeAddresses = new();
            foreach (var p in pages)
            {
                excludeAddresses.Add(p.friend.walletAddress);
            }
            FriendList.onLowMemory(excludeAddresses);
        }

        public override Block? getBlockHeader(ulong blockNum)
        {
            return storage.getBlock(blockNum);
        }

        public override IxiNumber getMinSignerPowDifficulty(ulong blockNum, int curBlockVersion, long curBlockTimestamp)
        {
            return tiv.getMinSignerPowDifficulty(blockNum, curBlockVersion, curBlockTimestamp);
        }

        private static void updateIxiPrice()
        {
            using (HttpClient client = new())
            {
                try
                {
                    HttpContent httpContent = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");
                    var response = client.PostAsync(Config.priceServiceUrl, httpContent).Result;
                    string body = response.Content.ReadAsStringAsync().Result;

                    dynamic obj = JsonConvert.DeserializeObject(body);
                    JObject jObject = (JObject)obj;
                    fiatPrice = new IxiNumber((string)jObject["ixicash"]["usd"]);
                }
                catch (Exception e)
                {
                    Logging.error("Exception occured in checkPrice: " + e);
                }
            }
            lastPriceUpdate = Clock.getTimestamp();
        }

        public override RegisteredNameRecord getRegName(byte[] name, bool useAbsoluteId = true)
        {
            throw new NotImplementedException();
        }

        public override byte[]? getBlockHash(ulong blockNum)
        {
            var tsd = storage.getBlockTotalSignerDifficulty(blockNum);
            return tsd.blockHash;
        }

        public static FriendMessage? addMessageWithType(byte[]? id, FriendMessageType type, Address wallet_address, int channel, string message, bool local_sender = false, Address? sender_address = null, long timestamp = 0, bool fire_local_notification = true, bool alert = true, int payable_data_len = 0)
        {
            FriendMessage? friend_message = FriendList.addMessageWithType(id, type, wallet_address, channel, message, local_sender, sender_address, timestamp, fire_local_notification, payable_data_len);
            if (friend_message != null)
            {
                bool oldMessage = false;

                Friend friend = FriendList.getFriend(wallet_address);

                if (!friend.online)
                {
                    StreamProcessor.fetchFriendsPresence(friend, true);
                }

                // Check if the message was sent before the friend was added to the contact list
                if (friend.addedTimestamp > friend_message.timestamp)
                {
                    oldMessage = true;
                }

                // If a chat page is visible, insert the message directly
                if (UIHelpers.isChatScreenDisplayed(friend))
                {
                    UIHelpers.insertMessage(friend, channel, friend_message);
                }
                else if (!friend_message.read)
                {
                    // Increase the unread counter if this is a new message
                    if (!oldMessage)
                        friend.metaData.unreadMessageCount++;

                    friend.saveMetaData();
                }

                UIHelpers.shouldRefreshContacts = true;

                // Only send alerts if this is a new message
                if (oldMessage == false)
                {
                    // Send a local push notification if Spixi is not in the foreground
                    if (fire_local_notification && !local_sender)
                    {
                        if (App.isInForeground == false || Utils.getChatPage(friend) == null)
                        {
                            // don't fire notification for nickname and avatar
                            if (!friend_message.id.SequenceEqual(new byte[] { 4 }) && !friend_message.id.SequenceEqual(new byte[] { 5 }))
                            {
                                if (friend.bot == false
                                    || (friend.metaData.botInfo != null && friend.metaData.botInfo.sendNotification))
                                {
                                    int unreadCount = FriendList.getUnreadMessageCount();
                                    SPushService.showLocalNotification((int)Crc32Algorithm.Compute(friend_message.id), "Spixi", "New Message", friend.walletAddress.ToString(), alert, unreadCount);
                                    SPushService.clearRemoteNotifications(unreadCount);
                                }
                            }
                        }
                    }

                    SSystemAlert.flash();
                }
            }
            return friend_message;
        }

        static public IxiNumber calculateTransactionFeeFromAvailableBalance(Address fromAddress, ExtendedAddress toAddress)
        {
            IxiNumber amount = getAvailableBalance();
            var prepTx = prepareTransactionFrom(fromAddress, toAddress, amount, false);
            var amountDiff = (prepTx.transaction.amount + prepTx.transaction.fee) - amount;
            prepTx = prepareTransactionFrom(fromAddress, toAddress, amount - amountDiff, false);
            amountDiff = (prepTx.transaction.amount + prepTx.transaction.fee) - (amount - amountDiff);
            return amountDiff;
        }

        static public IxiNumber calculateTransactionFee(Address fromAddress, ExtendedAddress toAddress, IxiNumber amount)
        {
            var prepTx = prepareTransactionFrom(fromAddress, toAddress, amount, false);
            var amountDiff = (prepTx.transaction.amount + prepTx.transaction.fee) - amount;
            return amountDiff;
        }

        static public (Transaction? transaction, List<Address>? relayNodeAddresses, List<ExtendedAddress>? extendedAddresses) prepareTransactionFrom(Address fromAddress, ExtendedAddress toAddress, IxiNumber amount, bool check_balance = true)
        {
            IxiNumber fee = ConsensusConfig.forceTransactionPrice;
            Dictionary<Address, ToEntry> toList = new(new AddressComparer());
            Address pubKey = new(IxianHandler.getWalletStorage().getPrimaryPublicKey());

            if (!IxianHandler.getWalletStorage().isMyAddress(fromAddress))
            {
                Logging.info("From address is not my address.");
                return (null, null, null);
            }

            Dictionary<byte[], IxiNumber> fromList = new(new ByteArrayComparer())
            {
                { IxianHandler.getWalletStorage().getAddress(fromAddress).nonce, amount }
            };

            List<ExtendedAddress> extendedAddresses = new List<ExtendedAddress>();

            toList.AddOrReplace(toAddress.PaymentAddress, new ToEntry(Transaction.getExpectedVersion(IxianHandler.getLastBlockVersion()), amount, toAddress.Tag));

            if (toAddress.Flag != AddressPaymentFlag.Primary)
            {
                extendedAddresses.Add(toAddress);
            }

            List<Address> tmpRelayNodeAddresses = NetworkClientManager.getRandomConnectedClientAddresses(2);
            List<Address> relayNodeAddresses = new List<Address>();
            IxiNumber relayFee = 0;
            foreach (Address relayNodeAddress in tmpRelayNodeAddresses)
            {
                if (toList.ContainsKey(relayNodeAddress))
                {
                    continue;
                }
                var tmpFee = fee > ConsensusConfig.transactionDustLimit ? fee : ConsensusConfig.transactionDustLimit;
                ToEntry toEntry = new ToEntry(getExpectedVersion(IxianHandler.getLastBlockVersion()),
                                              tmpFee,
                                              null,
                                              null);
                relayNodeAddresses.Add(relayNodeAddress);
                toList.Add(relayNodeAddress, toEntry);
                relayFee += tmpFee;
            }

            // Prepare transaction to calculate fee
            Transaction transaction = new((int)Transaction.Type.Normal, fee, toList, fromList, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());

            relayFee = 0;
            foreach (Address relayNodeAddress in relayNodeAddresses)
            {
                var tmpFee = transaction.fee > ConsensusConfig.transactionDustLimit ? transaction.fee : ConsensusConfig.transactionDustLimit;
                toList[relayNodeAddress].amount = tmpFee;
                relayFee += tmpFee;
            }

            byte[] first_address = fromList.Keys.First();
            fromList[first_address] = fromList[first_address] + relayFee + transaction.fee;
            if (check_balance)
            {
                IxiNumber wal_bal = IxianHandler.getWalletBalance(new Address(transaction.pubKey.addressNoChecksum, first_address));
                if (fromList[first_address] > wal_bal)
                {
                    IxiNumber maxAmount = wal_bal - transaction.fee;

                    if (maxAmount < 0)
                        maxAmount = 0;

                    Logging.info("Insufficient funds to cover amount and transaction fee.\nMaximum amount you can send is {0} IXI.\n", maxAmount);
                    return (null, null, null);
                }
            }
            // Prepare transaction with updated "from" amount to cover fee
            transaction = new((int)Transaction.Type.Normal, fee, toList, fromList, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());
            return (transaction, relayNodeAddresses, extendedAddresses);
        }

        static public Transaction sendTransactionFrom(Address fromAddress, ExtendedAddress toAddress, IxiNumber amount, byte[]? requestId)
        {
            var prepTx = prepareTransactionFrom(fromAddress, toAddress, amount);
            var transaction = prepTx.transaction;
            var relayNodeAddresses = prepTx.relayNodeAddresses;
            // Send the transaction
            if (IxianHandler.addTransaction(transaction, relayNodeAddresses, prepTx.extendedAddresses, requestId, true))
            {
                Logging.info("Sending transaction, txid: {0}", transaction.getTxIdString());
                return transaction;
            }
            else
            {
                Logging.warn("Could not send transaction, txid: {0}", transaction.getTxIdString());
            }
            return null;
        }
    }
}