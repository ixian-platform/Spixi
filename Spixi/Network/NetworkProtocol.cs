using IXICore;
using IXICore.Inventory;
using IXICore.Meta;
using IXICore.Network;
using IXICore.Network.Messages;
using IXICore.Storage;
using IXICore.Streaming;
using IXICore.Utils;
using SPIXI.Meta;
using System.Numerics;

namespace SPIXI.Network
{
    public class ProtocolMessage
    {
        public static void resubscribeEvents()
        {
            lock (NetworkClientManager.networkClients)
            {
                foreach (var client in NetworkClientManager.networkClients)
                {
                    if (client.isConnected() && client.helloReceived)
                    {
                        if (client.presenceAddress.type != 'M'
                            && client.presenceAddress.type != 'H'
                            && client.presenceAddress.type != 'R')
                        {
                            continue;
                        }

                        byte[] event_data = NetworkEvents.prepareEventMessageData(NetworkEvents.Type.all, new byte[0]);
                        client.sendData(ProtocolMessageCode.detachEvent, event_data);
                        subscribeToEvents(client);
                    }
                }
            }
        }

        private static void subscribeToEvents(RemoteEndpoint endpoint)
        {
            CoreProtocolMessage.subscribeToEvents(endpoint);

            byte[] friend_matcher = FriendList.getFriendCuckooFilter();
            if (friend_matcher != null)
            {
                byte[] event_data = NetworkEvents.prepareEventMessageData(NetworkEvents.Type.keepAlive, friend_matcher);
                endpoint.sendData(ProtocolMessageCode.attachEvent, event_data);
            }
        }

        // Unified protocol message parsing
        public static void parseProtocolMessage(ProtocolMessageCode code, byte[] data, RemoteEndpoint endpoint)
        {
            if (endpoint == null)
            {
                Logging.error("Endpoint was null. parseProtocolMessage");
                return;
            }
            try
            {
                switch (code)
                {
                    case ProtocolMessageCode.hello:
                        {
                            using (MemoryStream m = new MemoryStream(data))
                            {
                                using (BinaryReader reader = new BinaryReader(m))
                                {
                                    CoreProtocolMessage.processHelloMessageV6(endpoint, reader);
                                }
                            }

                        }
                        break;


                    case ProtocolMessageCode.helloData:
                       using (MemoryStream m = new MemoryStream(data))
                        {
                            using (BinaryReader reader = new BinaryReader(m))
                            {
                                if (!CoreProtocolMessage.processHelloMessageV6(endpoint, reader))
                                {
                                    return;
                                }

                                char node_type = endpoint.presenceAddress.type;

                                ulong last_block_num = reader.ReadIxiVarUInt();
                                int bcLen = (int)reader.ReadIxiVarUInt();
                                byte[] block_checksum = reader.ReadBytes(bcLen);

                                endpoint.blockHeight = last_block_num;

                                int block_version = (int)reader.ReadIxiVarUInt();

                                // TODO TODO TODO remove node_type != 'R' once highest known network block height is enforced in PL
                                if (node_type != 'C' && node_type != 'R')
                                {
                                    ulong highest_block_height = IxianHandler.getHighestKnownNetworkBlockHeight();
                                    if (last_block_num + 10 < highest_block_height)
                                    {
                                        CoreProtocolMessage.sendBye(endpoint, ProtocolByeCode.tooFarBehind, string.Format("Your node is too far behind, your block height is {0}, highest network block height is {1}.", last_block_num, highest_block_height), highest_block_height.ToString(), true);
                                        return;
                                    }
                                }

                                // Process the hello data
                                endpoint.helloReceived = true;
                                NetworkClientManager.recalculateLocalTimeDifference();

                                if (node_type == 'R')
                                {
                                    string[] connected_servers = StreamClientManager.getConnectedClients(true);
                                    if (connected_servers.Count() == 1 || !connected_servers.Contains(StreamClientManager.primaryS2Address))
                                    {
                                        // TODO set the primary s2 host more efficiently, perhaps allow for multiple s2 primary hosts
                                        StreamClientManager.primaryS2Address = endpoint.getFullAddress(true);
                                        // TODO TODO do not set if directly connectable
                                        IxianHandler.publicIP = endpoint.address;
                                        IxianHandler.publicPort = endpoint.incomingPort;
                                        PresenceList.forceSendKeepAlive = true;
                                        Logging.info("Forcing KA from networkprotocol");
                                    } else
                                    {
                                        // Announce local presence
                                        var myPresence = PresenceList.curNodePresence;
                                        if (myPresence != null)
                                        {
                                            foreach (var pa in myPresence.addresses)
                                            {
                                                byte[] hash = CryptoManager.lib.sha3_512sqTrunc(pa.getBytes());
                                                var iika = new InventoryItemKeepAlive(hash, pa.lastSeenTime, myPresence.wallet, pa.device);
                                                endpoint.addInventoryItem(iika);
                                            }
                                        }
                                    }

                                    StreamProcessor.fetchAllFriendsPresencesInSector(endpoint.presence.wallet);
                                }
                                else if (node_type == 'C')
                                {
                                    Friend f = FriendList.getFriend(endpoint.presence.wallet);
                                    if (f != null && f.bot)
                                    {
                                        CoreStreamProcessor.sendGetBotInfo(f);
                                    }
                                }

                                if (node_type == 'M'
                                    || node_type == 'H'
                                    || node_type == 'R')
                                {
                                    subscribeToEvents(endpoint);
                                }
                            }
                        }
                        break;

                    case ProtocolMessageCode.s2data:
                        {
                            Node.streamProcessor.receiveData(data, endpoint);
                        }
                        break;

                    case ProtocolMessageCode.getPresence2:
                        {
                            using (MemoryStream m = new MemoryStream(data))
                            {
                                using (BinaryReader reader = new BinaryReader(m))
                                {
                                    int walletLen = (int)reader.ReadIxiVarUInt();
                                    Address wallet = new Address(reader.ReadBytes(walletLen));

                                    Presence p = PresenceList.getPresenceByAddress(wallet);
                                    if (p != null)
                                    {
                                        lock (p)
                                        {
                                            byte[][] presence_chunks = p.getByteChunks();
                                            foreach (byte[] presence_chunk in presence_chunks)
                                            {
                                                endpoint.sendData(ProtocolMessageCode.updatePresence, presence_chunk, null);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // TODO blacklisting point
                                        Logging.warn(string.Format("Node has requested presence information about {0} that is not in our PL.", wallet.ToString()));
                                    }
                                }
                            }
                        }
                        break;

                    case ProtocolMessageCode.balance2:
                        {
                            using (MemoryStream m = new MemoryStream(data))
                            {
                                using (BinaryReader reader = new BinaryReader(m))
                                {
                                    int address_length = (int)reader.ReadIxiVarUInt();
                                    Address address = new Address(reader.ReadBytes(address_length));

                                    int balance_bytes_len = (int)reader.ReadIxiVarUInt();
                                    byte[] balance_bytes = reader.ReadBytes(balance_bytes_len);

                                    // Retrieve the latest balance
                                    IxiNumber ixi_balance = new IxiNumber(new BigInteger(balance_bytes));

                                    // Retrieve the blockheight for the balance
                                    ulong block_height = reader.ReadIxiVarUInt();
                                    byte[] block_checksum = reader.ReadBytes((int)reader.ReadIxiVarUInt());

                                    foreach (Balance balance in IxianHandler.balances)
                                    {
                                        if (address.addressNoChecksum.SequenceEqual(balance.address.addressNoChecksum))
                                        {
                                            if (block_height > balance.blockHeight && (balance.balance != ixi_balance || balance.blockHeight == 0))
                                            {
                                                balance.address = address;
                                                balance.balance = ixi_balance;
                                                balance.blockHeight = block_height;
                                                balance.blockChecksum = block_checksum;
                                                balance.verified = false;
                                            }

                                            balance.lastUpdate = Clock.getTimestamp();
                                        }
                                    }
                                }
                            }
                        }
                        break;


                    case ProtocolMessageCode.updatePresence:
                        handleUpdatePresence(data, endpoint);
                        break;

                    case ProtocolMessageCode.keepAlivePresence:
                        handleKeepAlivePresence(data, endpoint);
                        break;

                    case ProtocolMessageCode.compactBlockHeaders1:
                        {
                            using (MemoryStream m = new MemoryStream(data))
                            {
                                using (BinaryReader reader = new BinaryReader(m))
                                {
                                    ulong from = reader.ReadIxiVarUInt();
                                    ulong totalCount = reader.ReadIxiVarUInt();

                                    int filterLen = (int)reader.ReadIxiVarUInt();
                                    byte[] filterBytes = reader.ReadBytes(filterLen);

                                    byte[] headersBytes = new byte[reader.BaseStream.Length - reader.BaseStream.Position];
                                    Array.Copy(data, reader.BaseStream.Position, headersBytes, 0, headersBytes.Length);

                                    Node.tiv.receivedBlockHeaders3(headersBytes, endpoint);
                                }
                            }
                        }
                        break;

                    case ProtocolMessageCode.blockHeaders3:
                        {
                            // Forward the block headers to the TIV handler
                            Node.tiv.receivedBlockHeaders3(data, endpoint);
                        }
                        break;

                    case ProtocolMessageCode.pitData2:
                        {
                            Node.tiv.receivedPIT2(data, endpoint);
                        }
                        break;

                    case ProtocolMessageCode.transactionData2:
                        handleTransactionData(data, endpoint);
                        break;

                    case ProtocolMessageCode.bye:
                        CoreProtocolMessage.processBye(data, endpoint);
                        break;

                    case ProtocolMessageCode.inventory2:
                        handleInventory2(data, endpoint);
                        break;

                    case ProtocolMessageCode.sectorNodes:
                        handleSectorNodes(data, endpoint);
                        break;

                    case ProtocolMessageCode.nameRecord:
                        handleNameRecord(data, endpoint);
                        break;

                    case ProtocolMessageCode.keepAlivesChunk:
                        handleKeepAlivesChunk(data, endpoint);
                        break;

                    case ProtocolMessageCode.rejected:
                        handleRejected(data, endpoint);
                        break;

                    case ProtocolMessageCode.getKeepAlives:
                        CoreProtocolMessage.processGetKeepAlives(data, endpoint);
                        break;

                    default:
                        Logging.warn("Unknown protocol message: {0}, from {1} ({2})", code, endpoint.getFullAddress(), endpoint.serverWalletAddress);
                        break;

                }
            }
            catch (Exception e)
            {
                Logging.error("Error parsing network message. Details: {0}", e.ToString());
            }
        }


        private static void handleTransactionData(byte[] data, RemoteEndpoint endpoint)
        {
            // TODO: check for errors/exceptions
            Transaction tx = new Transaction(data, true, true);

            if (endpoint.presenceAddress.type == 'M'
                || endpoint.presenceAddress.type == 'H'
                || endpoint.presenceAddress.type == 'R')
            {
                PendingTransactions.increaseReceivedCount(tx.id, endpoint.presence.wallet);
            }

            TransactionCache.addUnconfirmedTransaction(tx);

            Node.tiv.receivedNewTransaction(tx);
        }

        public static void handleKeepAlivesChunk(byte[] data, RemoteEndpoint endpoint)
        {
            using (MemoryStream m = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    int ka_count = (int)reader.ReadIxiVarUInt();

                    int max_ka_per_chunk = CoreConfig.maximumKeepAlivesPerChunk;
                    if (ka_count > max_ka_per_chunk)
                    {
                        ka_count = max_ka_per_chunk;
                    }

                    for (int i = 0; i < ka_count; i++)
                    {
                        if (m.Position == m.Length)
                        {
                            break;
                        }

                        int ka_len = (int)reader.ReadIxiVarUInt();
                        byte[] ka_bytes = reader.ReadBytes(ka_len);

                        handleKeepAlivePresence(ka_bytes, endpoint);
                    }
                }
            }
        }

        private static void handleUpdatePresence(byte[] data, RemoteEndpoint endpoint)
        {
            
            // Parse the data and update entries in the presence list
            Presence p = PresenceList.updateFromBytes(data, 0);
            if (p == null)
            {
                return;
            }

            Logging.info("Received presence update for " + p.wallet);
            Friend f = FriendList.getFriend(p.wallet);
            if (f != null)
            {
                var pa = p.addresses[0];
                // TODO use actual wallet address once Presence hostname contains such address
                f.relayNode = new Peer(pa.address, null, pa.lastSeenTime, 0, 0, 0);
                f.updatedStreamingNodes = pa.lastSeenTime;
                if (UIHelpers.isChatScreenDisplayed(f))
                {
                    StreamClientManager.connectTo(f.relayNode.hostname, f.relayNode.walletAddress);
                }
            }
        }

        private static void handleKeepAlivePresence(byte[] data, RemoteEndpoint endpoint)
        {
            byte[] hash = CryptoManager.lib.sha3_512sqTrunc(data);

            InventoryCache.Instance.setProcessedFlag(InventoryItemTypes.keepAlive, hash);

            Address address = null;
            long last_seen = 0;
            byte[] device_id = null;
            char node_type;
            bool updated = PresenceList.receiveKeepAlive(data, out address, out last_seen, out device_id, out node_type, endpoint);

            Logging.trace("Received keepalive update for " + address);
            Presence p = PresenceList.getPresenceByAddress(address);
            if (p == null)
                return;

            Friend f = FriendList.getFriend(p.wallet);
            if (f != null)
            {
                var pa = p.addresses[0];
                // TODO use actual wallet address once Presence hostname contains such address
                f.relayNode = new Peer(pa.address, null, pa.lastSeenTime, 0, 0, 0);
                f.updatedStreamingNodes = pa.lastSeenTime;
                if (UIHelpers.isChatScreenDisplayed(f))
                {
                    StreamClientManager.connectTo(f.relayNode.hostname, f.relayNode.walletAddress);
                }
            }
        }


        static void handleNameRecord(byte[] data, RemoteEndpoint endpoint)
        {
            int offset = 0;

            var nameAndOffset = data.ReadIxiBytes(offset);
            offset += nameAndOffset.bytesRead;
            byte[] name = nameAndOffset.bytes;

            var recordCountAndOffset = data.GetIxiVarUInt(offset);
            offset += recordCountAndOffset.bytesRead;
            int recordCount = (int)recordCountAndOffset.num;

            for (int i = 0; i < recordCount; i++)
            {
                var recordAndOffset = data.ReadIxiBytes(offset);
                offset += recordAndOffset.bytesRead;
            }
        }


        static void handleSectorNodes(byte[] data, RemoteEndpoint endpoint)
        {
            int offset = 0;

            var prefixAndOffset = data.ReadIxiBytes(offset);
            offset += prefixAndOffset.bytesRead;
            byte[] prefix = prefixAndOffset.bytes;

            var nodeCountAndOffset = data.GetIxiVarUInt(offset);
            offset += nodeCountAndOffset.bytesRead;
            int nodeCount = (int)nodeCountAndOffset.num;

            for (int i = 0; i < nodeCount; i++)
            {
                var kaBytesAndOffset = data.ReadIxiBytes(offset);
                offset += kaBytesAndOffset.bytesRead;

                Presence p = PresenceList.updateFromBytes(kaBytesAndOffset.bytes, IxianHandler.getMinSignerPowDifficulty(IxianHandler.getLastBlockHeight() + 1, IxianHandler.getLastBlockVersion(), Clock.getNetworkTimestamp()));
                if (p != null)
                {
                    RelaySectors.Instance.addRelayNode(p.wallet);
                }
            }

            List<Peer> peers = new();
            var relays = RelaySectors.Instance.getSectorNodes(prefix, CoreConfig.maxRelaySectorNodesToRequest);
            foreach (var relay in relays)
            {
                var p = PresenceList.getPresenceByAddress(relay);
                if (p == null)
                {
                    continue;
                }
                var pa = p.addresses.First();
                peers.Add(new(pa.address, relay, pa.lastSeenTime, 0, 0, 0));

                PeerStorage.addPeerToPeerList(pa.address, p.wallet, pa.lastSeenTime, 0, 0, 0);
            }

            if (IxianHandler.primaryWalletAddress.sectorPrefix.SequenceEqual(prefix))
            {
                Node.networkClientManagerStatic.setClientsToConnectTo(peers);
            }

            var friends = FriendList.getFriendsBySectorPrefix(prefix);
            foreach(var friend in friends)
            {
                friend.updatedSectorNodes = Clock.getNetworkTimestamp();
                friend.sectorNodes = peers;
            }
        }

        static void handleRejected(byte[] data, RemoteEndpoint endpoint)
        {
            try
            {
                Rejected rej = new Rejected(data);
                switch (rej.code)
                {
                    case RejectedCode.TransactionInvalid:
                    case RejectedCode.TransactionInsufficientFee:
                    case RejectedCode.TransactionDust:
                        Logging.error("Transaction {0} was rejected with code: {1}", Crypto.hashToString(rej.data), rej.code);
                        PendingTransactions.remove(rej.data);
                        // TODO flag transaction as invalid
                        break;

                    case RejectedCode.TransactionDuplicate:
                        Logging.warn("Transaction {0} already sent.", Crypto.hashToString(rej.data), rej.code);
                        // All good
                        PendingTransactions.increaseReceivedCount(rej.data, endpoint.serverWalletAddress);
                        break;

                    default:
                        Logging.error("Received 'rejected' message with unknown code {0} {1}", rej.code, Crypto.hashToString(rej.data));
                        break;
                }
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Exception occured while processing 'rejected' message with code {0} {1}", data[0], Crypto.hashToString(data)), e);
            }
        }


        static void handleInventory2(byte[] data, RemoteEndpoint endpoint)
        {
            using (MemoryStream m = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    ulong item_count = reader.ReadIxiVarUInt();
                    if (item_count > (ulong)CoreConfig.maxInventoryItems)
                    {
                        Logging.warn("Received {0} inventory items, max items is {1}", item_count, CoreConfig.maxInventoryItems);
                        item_count = (ulong)CoreConfig.maxInventoryItems;
                    }

                    ulong last_accepted_block_height = IxianHandler.getLastBlockHeight();

                    ulong network_block_height = IxianHandler.getHighestKnownNetworkBlockHeight();

                    Dictionary<ulong, List<InventoryItemSignature>> sig_lists = new Dictionary<ulong, List<InventoryItemSignature>>();
                    List<InventoryItemKeepAlive> ka_list = new List<InventoryItemKeepAlive>();
                    List<byte[]> tx_list = new List<byte[]>();
                    for (ulong i = 0; i < item_count; i++)
                    {
                        ulong len = reader.ReadIxiVarUInt();
                        byte[] item_bytes = reader.ReadBytes((int)len);
                        InventoryItem item = InventoryCache.decodeInventoryItem(item_bytes);

                        // First update endpoint blockheights and pending transactions
                        switch (item.type)
                        {
                            case InventoryItemTypes.transaction:
                                PendingTransactions.increaseReceivedCount(item.hash, endpoint.presence.wallet);
                                break;

                            case InventoryItemTypes.block:
                                var iib = ((InventoryItemBlock)item);
                                if (iib.blockNum > endpoint.blockHeight)
                                {
                                    endpoint.blockHeight = iib.blockNum;
                                }
                                break;
                        }

                        PendingInventoryItem pii = InventoryCache.Instance.add(item, endpoint, false);

                        if (!pii.processed && pii.lastRequested == 0)
                        {
                            // first time we're seeing this inventory item
                            switch (item.type)
                            {
                                case InventoryItemTypes.keepAlive:
                                    var iika = (InventoryItemKeepAlive)item;
                                    if (PresenceList.getPresenceByAddress(iika.address) != null)
                                    {
                                        ka_list.Add(iika);
                                        pii.lastRequested = Clock.getTimestamp();
                                    }
                                    else
                                    {
                                        InventoryCache.Instance.processInventoryItem(pii);
                                    }
                                    break;

                                case InventoryItemTypes.transaction:
                                    tx_list.Add(item.hash);
                                    pii.lastRequested = Clock.getTimestamp();
                                    break;

                                case InventoryItemTypes.block:
                                    var iib = ((InventoryItemBlock)item);
                                    if (iib.blockNum <= last_accepted_block_height)
                                    {
                                        InventoryCache.Instance.setProcessedFlag(iib.type, iib.hash);
                                        continue;
                                    }

                                    var netBlockNum = CoreProtocolMessage.determineHighestNetworkBlockNum();
                                    if (iib.blockNum > netBlockNum)
                                    {
                                        continue;
                                    }

                                    requestNextBlock(iib.blockNum, iib.hash, endpoint);
                                    break;

                                default:
                                    Logging.warn("Unhandled inventory item {0}", item.type);
                                    InventoryCache.Instance.setProcessedFlag(item.type, item.hash);
                                    break;
                            }
                        }
                    }

                    CoreProtocolMessage.broadcastGetKeepAlives(ka_list, endpoint);

                    CoreProtocolMessage.broadcastGetTransactions(tx_list, 0, endpoint);
                }
            }
        }

        static void requestNextBlock(ulong blockNum, byte[] blockHash, RemoteEndpoint endpoint)
        {
            InventoryItemBlock iib = new InventoryItemBlock(blockHash, blockNum);
            PendingInventoryItem pii = InventoryCache.Instance.add(iib, endpoint, true);
            if (pii.lastRequested == 0)
            {
                pii.lastRequested = Clock.getTimestamp();
                InventoryCache.Instance.processInventoryItem(pii);
            }
        }
    }
}
