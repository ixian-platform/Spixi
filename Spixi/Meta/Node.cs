using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.RegNames;
using IXICore.Storage;
using IXICore.Streaming;
using IXICore.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spixi;
using SPIXI.CustomApps;
using SPIXI.Network;
using System.Text;
using static IXICore.Transaction;

namespace SPIXI.Meta
{
    class Node : IxianNode
    {
        // Used to force reloading of some homescreen elements
        public static bool changedSettings = false;

        public static IxiNumber fiatPrice = 0;  // Stores the last known ixi fiat value

        public static int startCounter = 0;

        public static bool refreshAppRequests = true;

        public static TransactionInclusion tiv = null;

        public static CustomAppManager customAppManager = null;

        public static StreamProcessor streamProcessor = null;

        public static bool generatedNewWallet = false;

        // Private data

        private static Thread mainLoopThread;

        public static Node Instance = null;

        private static bool running = false;

        private static long lastPriceUpdate = 0;

        public Node()
        {
#if DEBUG
            Logging.warn("Testing language files");
            //  Lang.SpixiLocalization.testLanguageFiles("en-us");
#endif
            Logging.info("Initing node constructor");
            Instance = this;

            SPushService.initialize();

            CoreConfig.simultaneousConnectedNeighbors = 6;

            IxianHandler.init(Config.version, this, Config.networkType);

            PeerStorage.init(Config.spixiUserFolder);

            // Init TIV
            tiv = new TransactionInclusion(new SpixiTransactionInclusionCallbacks());

            Logging.info("Initing local storage");

            // Prepare the local storage
            IxianHandler.localStorage = new LocalStorage(Config.spixiUserFolder, new SpixiLocalStorageCallbacks());

            customAppManager = new CustomAppManager(Config.spixiUserFolder);

            FriendList.init(Config.spixiUserFolder);

            // Prepare the stream processor
            streamProcessor = new StreamProcessor(new SpixiPendingMessageProcessor(Config.spixiUserFolder, Config.enablePushNotifications));

            OfflinePushMessages.init(Config.pushServiceUrl, streamProcessor);

            Logging.info("Node init done");

            string backup_file_name = Path.Combine(Config.spixiUserFolder, "spixi.account.backup.ixi");
            if (File.Exists(backup_file_name))
            {
                File.Delete(backup_file_name);
            }
        }

        static public void preStart()
        {
            // Start local storage
            IxianHandler.localStorage.start();

            FriendList.loadContacts();

            ProtocolMessage.resubscribeEvents();
        }

        static public void start()
        {
            if (running)
            {
                return;
            }
            Logging.info("Starting node");

            running = true;

            UpdateVerify.init(Config.checkVersionUrl, Config.checkVersionSeconds);
            UpdateVerify.start();

            ulong block_height = 0;
            byte[] block_checksum = null;

            string headers_path;
            if (IxianHandler.isTestNet)
            {
                headers_path = Path.Combine(Config.spixiUserFolder, "testnet-headers");
            }
            else
            {
                headers_path = Path.Combine(Config.spixiUserFolder, "headers");
                if (generatedNewWallet || !checkForExistingWallet())
                {
                    generatedNewWallet = false;
                    block_height = CoreConfig.bakedBlockHeight;
                    block_checksum = CoreConfig.bakedBlockChecksum;
                }
                else
                {
                    block_height = Config.bakedRecoveryBlockHeight;
                    block_checksum = Config.bakedRecoveryBlockChecksum;
                }
            }

            // TODO: replace the TIV with a liteclient-optimized implementation
            // Start TIV
            //tiv.start(headers_path, block_height, block_checksum, true);
            
            // Generate presence list
            PresenceList.init(IxianHandler.publicIP, 0, 'C', CoreConfig.clientKeepAliveInterval);

            // Start the network queue
            NetworkQueue.start();

            // Start the keepalive thread
            PresenceList.startKeepAlive();

            // Start the transfer manager
            TransferManager.start();

            customAppManager.start();

            startCounter++;

            if (mainLoopThread != null)
            {
                mainLoopThread.Interrupt();
                mainLoopThread.Join();
                mainLoopThread = null;
            }

            mainLoopThread = new Thread(mainLoop);
            mainLoopThread.Name = "Main_Loop_Thread";
            mainLoopThread.Start();

            // Init push service
            string tag = IxianHandler.getWalletStorage().getPrimaryAddress().ToString();          
            SPushService.setTag(tag);
            SPushService.clearNotifications();

            Logging.info("Node started");
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
            // Start the network client manager
            NetworkClientManager.start(2);

            // Start the s2 client manager
            StreamClientManager.start();
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
                if (bot_entry.relayIP != null)
                {
                    StreamClientManager.connectTo(bot_entry.searchForRelay(), bot_entry.walletAddress);
                }
            }
        }

        // Handle timer routines
        static public void mainLoop()
        {
            byte[] primaryAddress = IxianHandler.getWalletStorage().getPrimaryAddress().addressWithChecksum;
            if (primaryAddress == null)
                return;

            byte[] getBalanceBytes;
            using (MemoryStream mw = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(mw))
                {
                    writer.WriteIxiVarInt(primaryAddress.Length);
                    writer.Write(primaryAddress);
                }
                getBalanceBytes = mw.ToArray();
            }

            while (running)
            {
                try
                {
                    if (Config.enablePushNotifications)
                        OfflinePushMessages.fetchPushMessages();

                    // Update the friendlist
                    updateFriendStatuses();

                    // Cleanup the presence list
                    // TODO: optimize this by using a different thread perhaps
                    PresenceList.performCleanup();

                    Balance balance = IxianHandler.balances.First();
                    // Request initial wallet balance
                    if (balance.blockHeight == 0 || balance.lastUpdate + 300 < Clock.getTimestamp())
                    {
                        CoreProtocolMessage.broadcastProtocolMessage(new char[] { 'M', 'H' }, ProtocolMessageCode.getBalance2, getBalanceBytes, null);
                    }

                    // Check price if enough time passed
                    if (lastPriceUpdate + Config.checkPriceSeconds < Clock.getTimestamp())
                    {
                        checkPrice();
                    }

                    connectToBotNodes();
                }
                catch (Exception e)
                {
                    Logging.error("Exception occured in mainLoop: " + e);
                }
                Thread.Sleep(2500);
            }
        }

        static public void updateFriendStatuses()
        {
            lock (FriendList.friends)
            {
                // Go through each friend and check for the pubkey in the PL
                foreach (Friend friend in FriendList.friends)
                {
                    Presence presence = null;

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
                        if (friend.online == false)
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

        static public void stop()
        {
            if (!running)
            {
                Logging.stop();
                IxianHandler.status = NodeStatus.stopped;
                return;
            }

            Logging.info("Stopping node...");
            running = false;

            // Stop the stream processor
            streamProcessor.uninitialize();

            IxianHandler.localStorage.stop();

            customAppManager.stop();

            // Stop TIV
            tiv.stop();

            // Stop the transfer manager
            TransferManager.stop();

            // Stop the keepalive thread
            PresenceList.stopKeepAlive();

            // Stop the network queue
            NetworkQueue.stop();

            NetworkClientManager.stop();
            StreamClientManager.stop();

            UpdateVerify.stop();

            IxianHandler.status = NodeStatus.stopped;

            Logging.info("Node stopped");

            Logging.stop();
        }

        public override bool isAcceptingConnections()
        {
            // TODO TODO TODO TODO implement this properly
            return false;
        }


        public override void shutdown()
        {
            stop();
        }

        public override ulong getLastBlockHeight()
        {
            if (tiv.getLastBlockHeader() == null)
            {
                return 0;
            }
            return tiv.getLastBlockHeader().blockNum;
        }

        public override ulong getHighestKnownNetworkBlockHeight()
        {
            ulong bh = getLastBlockHeight();
            ulong netBlockNum = CoreProtocolMessage.determineHighestNetworkBlockNum();
            if (bh < netBlockNum)
            {
                bh = netBlockNum;
            }

            return bh;
        }

        public override int getLastBlockVersion()
        {
            if (tiv.getLastBlockHeader() == null
                || tiv.getLastBlockHeader().version < Block.maxVersion)
            {
                // TODO Omega force to v10 after upgrade
                return Block.maxVersion - 1;
            }
            return tiv.getLastBlockHeader().version;
        }

        public override bool addTransaction(Transaction tx, List<Address> relayNodeAddresses, bool force_broadcast)
        {
            // TODO Send to peer if directly connectable
            foreach (var address in relayNodeAddresses)
            {
                NetworkClientManager.sendToClient(address, ProtocolMessageCode.transactionData2, tx.getBytes(true, true), null);
            }
            //CoreProtocolMessage.broadcastProtocolMessage(new char[] { 'R' }, ProtocolMessageCode.transactionData2, tx.getBytes(true, true), null);
            PendingTransactions.addPendingLocalTransaction(tx, relayNodeAddresses);
            return true;
        }

        public override Block getLastBlock()
        {
            return tiv.getLastBlockHeader();
        }

        public override Wallet getWallet(Address id)
        {
            // TODO Properly implement this for multiple addresses
            Balance balance = IxianHandler.balances.First();
            if (balance.address != null && id.SequenceEqual(balance.address))
            {
                return new Wallet(balance.address, balance.balance);
            }
            return new Wallet(id, 0);
        }

        public override IxiNumber getWalletBalance(Address id)
        {
            // TODO Properly implement this for multiple addresses
            Balance balance = IxianHandler.balances.First();
            if (balance.address != null && id.SequenceEqual(balance.address))
            {
                return balance.balance;
            }
            return 0;
        }

        // Returns the current wallet's usable balance
        public static IxiNumber getAvailableBalance()
        {
            Balance balance = IxianHandler.balances.First();
            IxiNumber currentBalance = balance.balance;
            currentBalance -= TransactionCache.getPendingSentTransactionsAmount();

            return currentBalance;
        }

        public override void parseProtocolMessage(ProtocolMessageCode code, byte[] data, RemoteEndpoint endpoint)
        {
            ProtocolMessage.parseProtocolMessage(code, data, endpoint);
        }

        public static void processPendingTransactions()
        {
            // TODO TODO improve to include failed transactions
            ulong last_block_height = IxianHandler.getLastBlockHeight();
            lock (PendingTransactions.pendingTransactions)
            {
                long cur_time = Clock.getTimestamp();
                List<PendingTransaction> tmp_pending_transactions = new List<PendingTransaction>(PendingTransactions.pendingTransactions);
                int idx = 0;
                foreach (var entry in tmp_pending_transactions)
                {
                    Transaction t = entry.transaction;
                    long tx_time = entry.addedTimestamp;

                    if (t.applied != 0)
                    {
                        PendingTransactions.pendingTransactions.RemoveAll(x => x.transaction.id.SequenceEqual(t.id));
                        continue;
                    }

                    // if transaction expired, remove it from pending transactions
                    if (last_block_height > ConsensusConfig.getRedactedWindowSize() && t.blockHeight < last_block_height - ConsensusConfig.getRedactedWindowSize())
                    {
                        Logging.error("Error sending the transaction {0}", t.getTxIdString());
                        PendingTransactions.pendingTransactions.RemoveAll(x => x.transaction.id.SequenceEqual(t.id));
                        continue;
                    }

                    if (cur_time - tx_time > 40) // if the transaction is pending for over 40 seconds, resend
                    {
                        CoreProtocolMessage.broadcastProtocolMessage(new char[] { 'M', 'H' }, ProtocolMessageCode.transactionData2, t.getBytes(true, true), null);
                        entry.addedTimestamp = cur_time;
                        entry.confirmedNodeList.Clear();
                    }

                    if (entry.confirmedNodeList.Count() > 3) // already received 3+ feedback
                    {
                        continue;
                    }

                    if (cur_time - tx_time > 20) // if the transaction is pending for over 20 seconds, send inquiry
                    {
                        CoreProtocolMessage.broadcastGetTransaction(t.id, 0);
                    }

                    idx++;
                }
            }
        }

        public void onLowMemory()
        {
            var pages = Utils.getChatPages();
            List<Address> excludeAddresses = new();
            foreach (var p in pages)
            {
                excludeAddresses.Add(p.friend.walletAddress);
            }
            FriendList.onLowMemory(excludeAddresses);
        }

        public override Block getBlockHeader(ulong blockNum)
        {
            return BlockHeaderStorage.getBlockHeader(blockNum);
        }

        public override IxiNumber getMinSignerPowDifficulty(ulong blockNum, int curBlockVersion, long curBlockTimestamp)
        {
            // TODO TODO implement this properly
            return ConsensusConfig.minBlockSignerPowDifficulty;
        }

        private static void checkPrice()
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

        public override byte[] getBlockHash(ulong blockNum)
        {
            throw new NotImplementedException();
        }

        public static FriendMessage addMessageWithType(byte[] id, FriendMessageType type, Address wallet_address, int channel, string message, bool local_sender = false, Address sender_address = null, long timestamp = 0, bool fire_local_notification = true, int payable_data_len = 0)
        {
            FriendMessage friend_message = FriendList.addMessageWithType(id, type, wallet_address, channel, message, local_sender, sender_address, timestamp, fire_local_notification, payable_data_len);
            if (friend_message != null)
            {
                bool oldMessage = false;

                Friend friend = FriendList.getFriend(wallet_address);

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
                                    SPushService.showLocalNotification("Spixi", "New Message", friend.walletAddress.ToString());
                                }
                            }
                        }
                    }

                    SSystemAlert.flash();
                }
            }
            return friend_message;
        }

        public override void triggerSignerPowSolutionFound()
        {
            throw new NotImplementedException();
        }

        static public Transaction sendTransaction(Address address, IxiNumber amount)
        {
            // TODO add support for sending funds from multiple addreses automatically based on remaining balance
            Balance address_balance = IxianHandler.balances.First();
            var from = address_balance.address;
            return sendTransactionFrom(from, address, amount);
        }

        static public (Transaction transaction, List<Address> relayNodeAddresses) prepareTransactionFrom(Address fromAddress, Address toAddress, IxiNumber amount)
        {
            IxiNumber fee = ConsensusConfig.forceTransactionPrice;
            SortedDictionary<Address, ToEntry> to_list = new(new AddressComparer());
            Balance address_balance = IxianHandler.balances.FirstOrDefault(addr => addr.address.addressNoChecksum.SequenceEqual(fromAddress.addressNoChecksum));
            Address pubKey = new(IxianHandler.getWalletStorage().getPrimaryPublicKey());

            if (!IxianHandler.getWalletStorage().isMyAddress(fromAddress))
            {
                Logging.info("From address is not my address.");
                return (null, null);
            }

            SortedDictionary<byte[], IxiNumber> from_list = new(new ByteArrayComparer())
            {
                { IxianHandler.getWalletStorage().getAddress(fromAddress).nonce, amount }
            };

            to_list.AddOrReplace(toAddress, new ToEntry(Transaction.getExpectedVersion(IxianHandler.getLastBlockVersion()), amount));

            List<Address> relayNodeAddresses = NetworkClientManager.getRandomConnectedClientAddresses(2);
            IxiNumber relayFee = 0;
            foreach (Address relayNodeAddress in relayNodeAddresses)
            {
                var tmpFee = fee > ConsensusConfig.transactionDustLimit ? fee : ConsensusConfig.transactionDustLimit;
                to_list.AddOrReplace(relayNodeAddress, new ToEntry(Transaction.getExpectedVersion(IxianHandler.getLastBlockVersion()), tmpFee));
                relayFee += tmpFee;
            }


            // Prepare transaction to calculate fee
            Transaction transaction = new((int)Transaction.Type.Normal, fee, to_list, from_list, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());

            relayFee = 0;
            foreach (Address relayNodeAddress in relayNodeAddresses)
            {
                var tmpFee = transaction.fee > ConsensusConfig.transactionDustLimit ? transaction.fee : ConsensusConfig.transactionDustLimit;
                to_list[relayNodeAddress].amount = tmpFee;
                relayFee += tmpFee;
            }

            byte[] first_address = from_list.Keys.First();
            from_list[first_address] = from_list[first_address] + relayFee + transaction.fee;
            IxiNumber wal_bal = IxianHandler.getWalletBalance(new Address(transaction.pubKey.addressNoChecksum, first_address));
            if (from_list[first_address] > wal_bal)
            {
                IxiNumber maxAmount = wal_bal - transaction.fee;

                if (maxAmount < 0)
                    maxAmount = 0;

                Logging.info("Insufficient funds to cover amount and transaction fee.\nMaximum amount you can send is {0} IXI.\n", maxAmount);
                return (null, null);
            }
            // Prepare transaction with updated "from" amount to cover fee
            transaction = new((int)Transaction.Type.Normal, fee, to_list, from_list, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());
            return (transaction, relayNodeAddresses);
        }

        static public Transaction sendTransactionFrom(Address fromAddress, Address toAddress, IxiNumber amount)
        {
            var prepTx = prepareTransactionFrom(fromAddress, toAddress, amount);
            var transaction = prepTx.transaction;
            var relayNodeAddresses = prepTx.relayNodeAddresses;
            // Send the transaction
            if (IxianHandler.addTransaction(transaction, relayNodeAddresses, true))
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