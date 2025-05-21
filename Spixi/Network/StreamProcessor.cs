using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.SpixiBot;
using IXICore.Streaming;
using Spixi;
using SPIXI.MiniApps;
using SPIXI.Lang;
using SPIXI.Meta;
using SPIXI.Network;
using SPIXI.VoIP;
using System.Text;
using IXICore.Utils;

namespace SPIXI
{    
    class StreamProcessor : CoreStreamProcessor
    {
        public StreamProcessor(PendingMessageProcessor pendingMessageProcessor) : base(pendingMessageProcessor)
        {
        }

        // Called when receiving file headers from the message recipient
        public static void handleFileHeader(Address sender, SpixiMessage data, byte[] message_id)
        {
            Friend friend = FriendList.getFriend(sender);
            if (friend != null)
            {
                FileTransfer transfer = new FileTransfer(data.data);

                string message_data = string.Format("{0}:{1}", transfer.uid, transfer.fileName);
                FriendMessage fm = Node.addMessageWithType(message_id, FriendMessageType.fileHeader, sender, data.channel, message_data);
                if (fm != null)
                {
                    fm.transferId = transfer.uid;
                    fm.filePath = transfer.fileName;
                    fm.fileSize = transfer.fileSize;
                    IxianHandler.localStorage.requestWriteMessages(friend.walletAddress, transfer.channel);
                }
            }
            else
            {
                Logging.error("Received File Header from an unknown friend.");
            }
        }

        // Called when accepting a file
        public static void handleAcceptFile(Address sender, SpixiMessage data)
        {
            Friend friend = FriendList.getFriend(sender);
            if (friend != null)
            {
                Logging.info("Received accept file");

                try
                {
                    using (MemoryStream m = new MemoryStream(data.data))
                    {
                        using (BinaryReader reader = new BinaryReader(m))
                        {
                            string uid = reader.ReadString();

                            TransferManager.receiveAcceptFile(friend, uid);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.error("Exception occured while handling accept file from bytes: " + e);
                }
            }
            else
            {
                Logging.error("Received accept file from an unknown friend.");
            }
        }

        public static void handleRequestFileData(Address sender, SpixiMessage data)
        {
            Friend friend = FriendList.getFriend(sender);
            if (friend != null)
            {
                Logging.info("Received request file data");

                try
                {
                    using (MemoryStream m = new MemoryStream(data.data))
                    {
                        using (BinaryReader reader = new BinaryReader(m))
                        {
                            string uid = reader.ReadString();
                            ulong packet_number = reader.ReadUInt64();

                            TransferManager.sendFileData(friend, uid, packet_number);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.error("Exception occured while handling request file data from bytes: " + e);
                }

            }
            else
            {
                Logging.error("Received request file data from an unknown friend.");
            }
        }

        public static void handleFileData(Address sender, SpixiMessage data)
        {
            Friend friend = FriendList.getFriend(sender);
            if (friend != null)
            {
                TransferManager.receiveFileData(data.data, sender);
            }
            else
            {
                Logging.error("Received file data from an unknown friend.");
            }
        }

        public static void handleFileFullyReceived(Address sender, SpixiMessage data)
        {
            Friend friend = FriendList.getFriend(sender);
            if (friend == null)
            {
                Logging.error("Received file fully received from an unknown friend.");
                return;
            }

            TransferManager.completeFileTransfer(sender, Crypto.hashToString(data.data));
        }

        // Called when an encryption key is received from the S2 server, as per step 4 of the WhitePaper
        /*private static void sendRsaEncryptedMessage(StreamMessage msg, string key, RemoteEndpoint endpoint)
        {
        // TODO TODO use transaction code for S2
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    writer.Write(msg.getID());
                    writer.Write(msg.recipientAddress);
                    writer.Write(msg.transactionID);
                }
            }
            Console.WriteLine("Sending encrypted message with key {0}", key);

                    using (MemoryStream m = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(m))
                        {
                            writer.Write(msg.getID());
                            writer.Write(msg.recipientAddress);
                            writer.Write(msg.transactionID);

                            byte[] encrypted_message = CryptoManager.lib.encryptDataS2(msg.data, key);
                            int encrypted_count = encrypted_message.Count();

                            writer.Write(encrypted_count);
                            writer.Write(encrypted_message);

                            byte[] ba = ProtocolMessage.prepareProtocolMessage(ProtocolMessageCode.s2data, m.ToArray());
                            socket.Send(ba, SocketFlags.None);


                            // Update the DLT transaction as well
                            Transaction transaction = new Transaction(0, msg.recipientAddress, IxianHandler.getWalletStorage().address);
                            transaction.id = msg.transactionID;
                            //transaction.data = Encoding.UTF8.GetString(checksum);
                            //ProtocolMessage.broadcastProtocolMessage(ProtocolMessageCode.updateTransaction, transaction.getBytes());

                        }
                    }
        }*/

        // Called when receiving S2 data from clients
        public override ReceiveDataResponse receiveData(byte[] bytes, RemoteEndpoint endpoint, bool fireLocalNotification = true)
        {
            ReceiveDataResponse rdr = base.receiveData(bytes, endpoint, fireLocalNotification);
            if (rdr == null)
            {
                return rdr;
            }

            StreamMessage message = rdr.streamMessage;
            SpixiMessage spixi_message = rdr.spixiMessage;
            Friend friend = rdr.friend;
            Address sender_address = rdr.senderAddress;
            Address real_sender_address = rdr.realSenderAddress;

            int channel = 0;
            try
            {
                switch (spixi_message.type)
                {
                    case SpixiMessageCode.requestFundsResponse:
                        {
                            var chat_page = Utils.getChatPage(friend);
                            if (chat_page != null)
                            {
                                string msg_id_tx_id = Encoding.UTF8.GetString(spixi_message.data);
                                string[] msg_id_tx_id_split = msg_id_tx_id.Split(':');
                                byte[] msg_id = null;
                                string tx_id = null;
                                if (msg_id_tx_id_split.Length == 2)
                                {
                                    msg_id = Crypto.stringToHash(msg_id_tx_id_split[0]);
                                    tx_id = msg_id_tx_id_split[1];
                                }
                                else
                                {
                                    msg_id = Crypto.stringToHash(msg_id_tx_id);
                                }

                                FriendMessage msg = friend.getMessages(0).Find(x => x.id.SequenceEqual(msg_id));

                                string status = SpixiLocalization._SL("chat-payment-status-pending");
                                if (tx_id != null)
                                {
                                    msg.message = ":" + tx_id;
                                }
                                else
                                {
                                    tx_id = "";
                                    status = SpixiLocalization._SL("chat-payment-status-declined");
                                    msg.message = "::" + msg.message; // declined
                                }

                                byte[]? b_tx_id = !string.IsNullOrEmpty(tx_id) ? Transaction.txIdLegacyToV8(tx_id) : null;
                                chat_page.updateRequestFundsStatus(msg_id, b_tx_id, status);
                            }
                        }
                        break;

                    case SpixiMessageCode.fileHeader:
                        {
                            handleFileHeader(sender_address, spixi_message, message.id);
                        }
                        break;

                    case SpixiMessageCode.acceptFile:
                        {
                            handleAcceptFile(sender_address, spixi_message);
                            break;
                        }

                    case SpixiMessageCode.requestFileData:
                        {
                            handleRequestFileData(sender_address, spixi_message);
                            // don't send confirmation back, so just return
                            return rdr;
                        }

                    case SpixiMessageCode.fileData:
                        {
                            handleFileData(sender_address, spixi_message);
                            // don't send confirmation back, so just return
                            return rdr;
                        }

                    case SpixiMessageCode.fileFullyReceived:
                        {
                            handleFileFullyReceived(sender_address, spixi_message);
                            return rdr;
                        }

                    case SpixiMessageCode.appData:
                        {
                            // app data received, find the session id of the app and forward the data to it
                            handleAppData(sender_address, spixi_message.data);
                            return rdr;
                        }

                    case SpixiMessageCode.appRequest:
                        {
                            // app request received
                            handleAppRequest(sender_address, message.recipient, spixi_message.data);
                            break;
                        }

                    case SpixiMessageCode.appRequestAccept:
                        {
                            handleAppRequestAccept(sender_address, spixi_message.data);
                            break;
                        }

                    case SpixiMessageCode.appRequestReject:
                        {
                            handleAppRequestReject(sender_address, spixi_message.data);
                            break;
                        }
                    case SpixiMessageCode.appEndSession:
                        {
                            handleAppEndSession(sender_address, spixi_message.data);
                            break;
                        }

                    case SpixiMessageCode.requestAdd:
                        {
                            if (friend.approved)
                            {
                                Node.addMessageWithType(new byte[] { 1 }, FriendMessageType.standard, friend.walletAddress, 0, string.Format(SpixiLocalization._SL("global-friend-request-accepted"), false, friend.nickname));
                            }
                            else
                            {
                                Node.addMessageWithType(message.id, FriendMessageType.requestAdd, sender_address, 0, "");
                            }

                            UIHelpers.shouldRefreshContacts = true;

                            ProtocolMessage.resubscribeEvents();
                        }
                        break;

                    case SpixiMessageCode.acceptAdd:
                        {
                            Node.addMessageWithType(new byte[] { 1 }, FriendMessageType.standard, friend.walletAddress, 0, string.Format(SpixiLocalization._SL("global-friend-request-accepted"), false, friend.nickname));
                            ProtocolMessage.resubscribeEvents();
                        }
                        break;

                    case SpixiMessageCode.acceptAddBot:
                        {
                            Node.addMessageWithType(new byte[] { 1 }, FriendMessageType.standard, friend.walletAddress, 0, string.Format(SpixiLocalization._SL("global-friend-request-accepted"), false, friend.nickname));
                            var chat_page = Utils.getChatPage(friend);
                            if (chat_page != null)
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    chat_page.convertToBot();
                                });
                            }
                        }
                        break;

                    case SpixiMessageCode.botAction:
                        onBotAction(spixi_message.data, friend, channel);
                        break;

                    case SpixiMessageCode.msgTyping:
                        handleFriendIsTyping(friend);
                        return rdr;

                    case SpixiMessageCode.avatar:
                        if (spixi_message.data != null && spixi_message.data.Length < 500000)
                        {
                            byte[] resized_avatar = SFilePicker.ResizeImage(spixi_message.data, 128, 128, 100);
                            FriendList.setAvatar(sender_address, spixi_message.data, resized_avatar, real_sender_address);
                            UIHelpers.shouldRefreshContacts = true;
                        }
                        else
                        {
                            //FriendList.setAvatar(sender_address, null, null, real_sender_address);
                        }
                        break;

                    case SpixiMessageCode.requestFunds:
                        Node.addMessageWithType(message.id, FriendMessageType.requestFunds, sender_address, 0, UTF8Encoding.UTF8.GetString(spixi_message.data));
                        break;

                    case SpixiMessageCode.sentFunds:
                        Node.addMessageWithType(message.id, FriendMessageType.sentFunds, sender_address, 0, Transaction.getTxIdString(spixi_message.data));
                        break;

                    case SpixiMessageCode.chat:
                        Node.addMessageWithType(message.id, FriendMessageType.standard, sender_address, spixi_message.channel, Encoding.UTF8.GetString(spixi_message.data), false, real_sender_address, message.timestamp, fireLocalNotification);
                        break;

                    case SpixiMessageCode.msgReceived:
                        {
                            UIHelpers.shouldRefreshContacts = true;
                            var fm = friend.getMessage(channel, spixi_message.data);
                            if (fm != null)
                            {
                                UIHelpers.updateMessage(friend, channel, fm);
                            }
                        }
                        break;
                            
                    case SpixiMessageCode.msgRead:
                        {
                            UIHelpers.shouldRefreshContacts = true;
                            var fm = friend.getMessage(channel, spixi_message.data);
                            if (fm != null)
                            {
                                UIHelpers.updateMessage(friend, channel, fm);
                            }
                        }
                        break;

                    case SpixiMessageCode.msgDelete:
                        UIHelpers.deleteMessage(friend, channel, spixi_message.data);
                        break;

                    case SpixiMessageCode.msgReaction:
                        var reaction = new SpixiMessageReaction(spixi_message.data);
                        UIHelpers.updateReactions(friend, channel, reaction.msgId);
                        break;

                    case SpixiMessageCode.leaveConfirmed:
                        UIHelpers.shouldRefreshContacts = true;
                        break;

                    case SpixiMessageCode.nick:
                        if (friend.bot && real_sender_address != null)
                        {
                            // update UI with the new nick
                            Logging.info("Updating group chat nicks");
                            var nick = friend.users.getUser(real_sender_address).getNick();
                            UIHelpers.updateGroupChatNicks(friend, real_sender_address, nick);
                        }else
                        {
                            UIHelpers.shouldRefreshContacts = true;
                        }
                        break;
                }
            }catch(Exception e)
            {
                Logging.error("Exception occured in StreamProcessor.receiveData: " + e);
            }
            return rdr;
        }

        protected void handleFriendIsTyping(Friend friend)
        {
            friend.isTyping = true;
            UIHelpers.shouldRefreshContacts = true;

            Timer? timer = null;
            timer = new(_ =>
            {
                friend.isTyping = false;
                UIHelpers.shouldRefreshContacts = true;
                _typingTimers.Remove(_typingTimers.FirstOrDefault());
            }, timer, 5000, Timeout.Infinite);

            _typingTimers.Add(timer);
            Utils.getChatPage(friend)?.showTyping();
        }

        private static void handleAppData(Address sender_address, byte[] app_data_raw)
        {
            // TODO use channels and drop SpixiAppData
            SpixiAppData app_data = new SpixiAppData(app_data_raw);
            if (VoIPManager.hasSession(app_data.sessionId))
            {
                VoIPManager.onData(app_data.sessionId, app_data.data);
                return;
            }
            MiniAppPage app_page = Node.MiniAppManager.getAppPage(app_data.sessionId);
            if(app_page == null)
            {
                Logging.error("App with session id: {0} does not exist.", Crypto.hashToString(app_data.sessionId));
                return;
            }
            app_page.networkDataReceived(sender_address, app_data.data);
        }

        public static void sendAppRequest(Friend friend, string app_id, byte[] session_id, byte[] data)
        {
            // TODO use channels and drop SpixiAppData
            string app_install_url = Node.MiniAppManager.getAppInstallURL(app_id);
            string app_name = Node.MiniAppManager.getAppName(app_id);
            string app_info = $"{app_id}||{app_install_url}||{app_name}"; // TODO pack this information better

            SpixiMessage spixi_msg = new SpixiMessage();
            spixi_msg.type = SpixiMessageCode.appRequest;
            spixi_msg.data = new SpixiAppData(session_id, data, app_info).getBytes();

            StreamMessage new_msg = new StreamMessage();
            new_msg.type = StreamMessageCode.data;
            new_msg.recipient = friend.walletAddress;
            new_msg.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            new_msg.data = spixi_msg.getBytes();

            CoreStreamProcessor.sendMessage(friend, new_msg);
        }

        public static void sendAppRequestAccept(Friend friend, byte[] session_id, byte[] data = null)
        {
            // TODO use channels and drop SpixiAppData
            SpixiMessage spixi_msg = new SpixiMessage();
            spixi_msg.type = SpixiMessageCode.appRequestAccept;
            spixi_msg.data = new SpixiAppData(session_id, data).getBytes();

            StreamMessage new_msg = new StreamMessage();
            new_msg.type = StreamMessageCode.data;
            new_msg.recipient = friend.walletAddress;
            new_msg.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            new_msg.data = spixi_msg.getBytes();

            CoreStreamProcessor.sendMessage(friend, new_msg, true, false, false);
        }

        public static void sendAppRequestReject(Friend friend, byte[] session_id, byte[] data = null)
        {
            // TODO use channels and drop SpixiAppData
            SpixiMessage spixi_msg = new SpixiMessage();
            spixi_msg.type = SpixiMessageCode.appRequestReject;
            spixi_msg.data = new SpixiAppData(session_id, data).getBytes();

            StreamMessage new_msg = new StreamMessage();
            new_msg.type = StreamMessageCode.data;
            new_msg.recipient = friend.walletAddress;
            new_msg.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            new_msg.data = spixi_msg.getBytes();

            CoreStreamProcessor.sendMessage(friend, new_msg);
        }

        public static void sendAppData(Friend friend, byte[] session_id, byte[] data)
        {
            // TODO use channels and drop SpixiAppData
            SpixiMessage spixi_msg = new SpixiMessage();
            spixi_msg.type = SpixiMessageCode.appData;
            spixi_msg.data = (new SpixiAppData(session_id, data)).getBytes();

            StreamMessage msg = new StreamMessage();
            msg.type = StreamMessageCode.data;
            msg.recipient = friend.walletAddress;
            msg.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            msg.data = spixi_msg.getBytes();

            CoreStreamProcessor.sendMessage(friend, msg, false, false, false);
        }

        public static void sendAppEndSession(Friend friend, byte[] session_id, byte[] data = null)
        {
            // TODO use channels and drop SpixiAppData
            SpixiMessage spixi_msg = new SpixiMessage();
            spixi_msg.type = SpixiMessageCode.appEndSession;
            spixi_msg.data = (new SpixiAppData(session_id, data)).getBytes();

            if (friend == null)
            {
                return;
            }

            StreamMessage msg = new StreamMessage();
            msg.type = StreamMessageCode.data;
            msg.recipient = friend.walletAddress;
            msg.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            msg.data = spixi_msg.getBytes();

            CoreStreamProcessor.sendMessage(friend, msg, true, true, false);
        }

        private static void handleAppRequest(Address sender_address, Address recipient_address, byte[] app_data_raw)
        {
            MiniAppManager am = Node.MiniAppManager;

            Friend friend = FriendList.getFriend(sender_address);
            if (friend == null)
            {
                Logging.error("Received app request from an unknown contact.");
                return;
            }

            if (!IxianHandler.getWalletStorage().isMyAddress(recipient_address))
            {
                return;
            }

            // TODO use channels and drop SpixiAppData
            SpixiAppData app_data = new SpixiAppData(app_data_raw);
            
            if(app_data.sessionId == null)
            {
                Logging.error("App session id is null.");
                return;
            }

            MiniAppPage app_page = am.getAppPage(app_data.sessionId);
            if (app_page != null)
            {
                Logging.error("App with session id: {0} already exists.", Crypto.hashToString(app_data.sessionId));
                return;
            }

            if (string.IsNullOrEmpty(app_data.appId))
            {
                Logging.error("App with session id: {0} has no provided app id.", Crypto.hashToString(app_data.sessionId));
                return;
            }

            string[] appid_data = app_data.appId.Split("||");
            string app_id = appid_data[0];
            string? app_install_url = appid_data.Length > 1 ? appid_data[1] : null;


            app_page = am.getAppPage(sender_address, app_id);
            if (app_page != null)
            {
                // TODO, maybe kill the old session and restart instead
                Logging.warn("App with sender: {0} already exists, updating session id.", sender_address.ToString());
                app_page.sessionId = app_data.sessionId;
                return;
            }
            
            Address[] user_addresses = new Address[] { sender_address };
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MiniApp app = am.getApp(app_id);
                if (app == null)
                {
                    if (app_id == "spixi.voip")
                    {
                        if (!friend.hasMessage(0, app_data.sessionId))
                        {
                            if (VoIPManager.onReceivedCall(friend, app_data.sessionId, app_data.data))
                            {
                                Node.addMessageWithType(app_data.sessionId, FriendMessageType.voiceCall, sender_address, 0, "");
                            }
                            UIHelpers.refreshAppRequests = true;
                        }
                        return;
                    }else
                    {
                        // app doesn't exist
                        Logging.error("App with id {0} is not installed.", app_id);
                    }
                }
                Node.addMessageWithType(app_data.sessionId, FriendMessageType.appSession, sender_address, 0, app_data.appId);

            });
        }

        private static void handleAppRequestAccept(Address sender_address, byte[] app_data_raw)
        {
            // TODO use channels and drop SpixiAppData
            SpixiAppData app_data = new SpixiAppData(app_data_raw);

            if (VoIPManager.hasSession(app_data.sessionId))
            {
                VoIPManager.onAcceptedCall(app_data.sessionId, app_data.data);
                UIHelpers.refreshAppRequests = true;
                return;
            }

            MiniAppPage page = Node.MiniAppManager.getAppPage(app_data.sessionId);
            if(page == null)
            {
                Logging.info("App session does not exist.");
                return;
            }

            page.accepted = true;

            page.appRequestAcceptReceived(sender_address, app_data.data);

            UIHelpers.refreshAppRequests = true;
        }

        public static void handleAppRequestReject(Address sender_address, byte[] app_data_raw)
        {
            // TODO use channels and drop SpixiAppData
            SpixiAppData app_data = new SpixiAppData(app_data_raw);
            byte[] session_id = app_data.sessionId;

            if (VoIPManager.hasSession(session_id))
            {
                VoIPManager.onRejectedCall(session_id);
                UIHelpers.refreshAppRequests = true;
                return;
            }

            MiniAppPage page = Node.MiniAppManager.getAppPage(session_id);
            if (page == null)
            {
                Logging.info("App session does not exist.");
                return;
            }

            page.appRequestRejectReceived(sender_address, app_data.data);

            UIHelpers.refreshAppRequests = true;
        }

        public static void handleAppEndSession(Address sender_address, byte[] app_data_raw)
        {
            // TODO use channels and drop SpixiAppData
            SpixiAppData app_data = new SpixiAppData(app_data_raw);
            byte[] session_id = app_data.sessionId;

            if (VoIPManager.hasSession(session_id))
            {
                VoIPManager.onHangupCall(session_id);
                UIHelpers.refreshAppRequests = true;
                return;
            }

            MiniAppPage page = Node.MiniAppManager.getAppPage(session_id);
            if (page == null)
            {
                Logging.info("App session does not exist.");
                return;
            }

            page.appEndSessionReceived(sender_address, app_data.data);
            UIHelpers.refreshAppRequests = true;
        }

        public static void onBotAction(byte[] action_data, Friend bot, int channel_id)
        {
            if(!bot.bot)
            {
                Logging.warn("Received onBotAction for a non-bot");
                return;
            }
            SpixiBotAction sba = new SpixiBotAction(action_data);
            switch (sba.action)
            {
                case SpixiBotActionCode.kickUser:
                    Node.addMessageWithType(null, FriendMessageType.kicked, bot.walletAddress, 0, SpixiLocalization._SL("chat-kicked"));
                    break;

                case SpixiBotActionCode.banUser:
                    Node.addMessageWithType(null, FriendMessageType.banned, bot.walletAddress, 0, SpixiLocalization._SL("chat-banned"));
                    break;
            }
        }

        public static void fetchAllFriendsPresences(int maxCount)
        {
            var friends = FriendList.friends.OrderBy(x => x.metaData.lastMessage.timestamp);
            int count = 0;
            foreach (var friend in friends)
            {
                if (count > maxCount)
                {
                    break;
                }

                if (Clock.getNetworkTimestamp() - friend.updatedStreamingNodes > Config.contactSectorNodeIntervalSeconds)
                {
                    continue;
                }

                fetchFriendsPresence(friend);
                count++;
            }
        }

        public static void fetchAllFriendsPresencesInSector(Address address)
        {
            var friends = FriendList.friends;
            foreach (var friend in friends)
            {
                if (friend.sectorNodes.Find(x => x.walletAddress.SequenceEqual(address)) == null)
                {
                    continue;
                }

                if (Clock.getNetworkTimestamp() - friend.updatedStreamingNodes > Config.contactSectorNodeIntervalSeconds)
                {
                    continue;
                }

                fetchFriendsPresence(friend);
            }
        }

        public static void fetchFriendsPresence(Friend friend)
        {
            if (friend.sectorNodes.Count() == 0)
            {
                CoreProtocolMessage.fetchSectorNodes(friend.walletAddress, Config.maxRelaySectorNodesToRequest);
                return;
            }

            using (MemoryStream mw = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(mw))
                {
                    writer.WriteIxiVarInt(friend.walletAddress.addressNoChecksum.Length);
                    writer.Write(friend.walletAddress.addressNoChecksum);
                }

                if (!StreamClientManager.sendToClient(friend.sectorNodes, ProtocolMessageCode.getPresence2, mw.ToArray(), null))
                {
                    // Not connected to contact's sector node

                    var rnd = new Random();
                    var sn = friend.sectorNodes[rnd.Next(friend.sectorNodes.Count - 1)];
                    StreamClientManager.connectTo(sn.hostname, sn.walletAddress);
                }
            }
        }
    }
}