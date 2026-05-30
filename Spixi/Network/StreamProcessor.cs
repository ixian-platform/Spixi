using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.SpixiBot;
using IXICore.Streaming;
using Spixi;
using SPIXI.MiniApps;
using SPIXI.Lang;
using SPIXI.Meta;
using SPIXI.VoIP;
using System.Text;
using IXICore.Streaming.Models;
using System.IO;
using System;
using Microsoft.Maui.ApplicationModel;
using System.Threading;
using System.Linq;

namespace SPIXI
{    
    class StreamProcessor : CoreStreamProcessor
    {
        public StreamProcessor(PendingMessageProcessor pendingMessageProcessor, StreamCapabilities streamCapabilities) : base(pendingMessageProcessor, streamCapabilities)
        {
        }

        // Called when receiving file headers from the message recipient
        public static void handleFileHeader(Address sender, SpixiMessage data, byte[] message_id, Address? group_sender_address)
        {
            Friend friend = FriendList.getFriend(sender);
            if (friend != null)
            {
                FileTransfer transfer = new FileTransfer(data.data);

                string message_data = string.Format("{0}:{1}:{2}", transfer.uid, transfer.fileName, transfer.fileSize);
                FriendMessage fm = Node.addMessageWithType(message_id, FriendMessageType.fileHeader, sender, data.channel, message_data, false, group_sender_address);
                if (fm != null)
                {
                    // TODO this can probably be removed now
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
            Friend? friend = FriendList.getFriend(sender);
            if (friend != null)
            {
                Logging.trace("Received request file data");

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
            var uid = Crypto.hashToString(data.data);
            var ft = TransferManager.getOutgoingTransfer(uid);
            if (ft != null && ft.groupAddress != null)
            {
                Friend? friend = FriendList.getFriend(ft.groupAddress);
                if (friend != null && friend.type == FriendType.Group)
                {
                    var chat_message = friend.getMessages(ft.channel)?.Find(x => x.transferId == uid);
                    if (chat_message != null)
                    {
                        friend.addReaction(sender, new ReactionMessage(chat_message.id, "fileReceived:"), ft.channel);
                        if (chat_message.completed)
                        {
                            TransferManager.completeFileTransfer(sender, uid);
                        }
                    }
                }

                return;
            }

            TransferManager.completeFileTransfer(sender, uid);
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
        public override ReceiveDataResponse? receiveData(byte[] bytes, RemoteEndpoint endpoint, bool fireLocalNotification = true, bool alert = true)
        {
            ReceiveDataResponse? rdr = base.receiveData(bytes, endpoint);
            if (rdr == null)
            {
                return rdr;
            }

            StreamMessage message = rdr.streamMessage;
            SpixiMessage spixi_message = rdr.spixiMessage;
            Friend? friend = rdr.friend;
            Address sender_address = rdr.senderAddress;
            Address? group_sender_address = rdr.groupSenderAddress;

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
                                byte[] msg_id;
                                string? tx_id = null;
                                if (msg_id_tx_id_split.Length == 2)
                                {
                                    msg_id = Crypto.stringToHash(msg_id_tx_id_split[0]);
                                    tx_id = msg_id_tx_id_split[1];
                                }
                                else
                                {
                                    msg_id = Crypto.stringToHash(msg_id_tx_id);
                                }

                                FriendMessage? msg = friend.getMessages(0).Find(x => x.id != null && x.id.SequenceEqual(msg_id));

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
                            handleFileHeader(sender_address, spixi_message, message.id, group_sender_address);
                        }
                        break;

                    case SpixiMessageCode.acceptFile:
                        {
                            handleAcceptFile(sender_address, spixi_message);
                        }
                        break;

                    case SpixiMessageCode.requestFileData:
                        {
                            handleRequestFileData(sender_address, spixi_message);
                        }
                        break;

                    case SpixiMessageCode.fileData:
                        {
                            handleFileData(sender_address, spixi_message);
                        }
                        break;

                    case SpixiMessageCode.fileFullyReceived:
                        {
                            handleFileFullyReceived(sender_address, spixi_message);
                        }
                        break;

                    case SpixiMessageCode.appData:
                        {
                            // app data received, find the session id of the app and forward the data to it
                            handleAppData(sender_address, spixi_message.data, group_sender_address);
                        }
                        break;

                    case SpixiMessageCode.appRequest:
                        {
                            // app request received
                            handleAppRequest(message.id, sender_address, message.recipient, spixi_message.data);
                        }
                        break;

                    case SpixiMessageCode.appRequestAccept:
                        {
                            handleAppRequestAccept(sender_address, spixi_message.data, group_sender_address);
                        }
                        break;

                    case SpixiMessageCode.appRequestReject:
                        {
                            handleAppRequestReject(sender_address, spixi_message.data, group_sender_address);
                        }
                        break;

                    case SpixiMessageCode.appEndSession:
                        {
                            handleAppEndSession(sender_address, spixi_message.data, group_sender_address);
                        }
                        break;

                    case SpixiMessageCode.requestAdd:
                    case SpixiMessageCode.requestAdd2:
                        {
                            if (friend.approved)
                            {
                                Node.addMessageWithType(new byte[] { 1 }, FriendMessageType.standard, friend.walletAddress, 0, string.Format(SpixiLocalization._SL("global-friend-request-accepted"), friend.nickname));
                            }
                            else
                            {
                                Node.addMessageWithType(message.id, FriendMessageType.requestAdd, sender_address, 0, "");
                            }

                            UIHelpers.shouldRefreshContacts = true;

                            CoreProtocolMessage.resubscribeEvents();
                            CoreStreamProcessor.fetchFriendsPresence(friend, true);
                        }
                        break;

                    case SpixiMessageCode.acceptAdd:
                    case SpixiMessageCode.acceptAdd2:
                        {
                            Node.addMessageWithType(new byte[] { 1 }, FriendMessageType.standard, friend.walletAddress, 0, string.Format(SpixiLocalization._SL("global-friend-request-accepted"), friend.nickname));
                            CoreProtocolMessage.resubscribeEvents();
                            CoreStreamProcessor.fetchFriendsPresence(friend, true);
                        }
                        break;

                    case SpixiMessageCode.keys2:
                        {
                            CoreStreamProcessor.fetchFriendsPresence(friend, true);
                        }
                        break;

                    case SpixiMessageCode.acceptAddBot:
                        {
                            Node.addMessageWithType(new byte[] { 1 }, FriendMessageType.standard, friend.walletAddress, 0, string.Format(SpixiLocalization._SL("global-friend-request-accepted"), friend.nickname));
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
                        break;

                    case SpixiMessageCode.avatar:
                        if (spixi_message.data != null && spixi_message.data.Length < 500000)
                        {
                            byte[] resized_avatar = SFilePicker.ResizeImage(spixi_message.data, 128, 128, 100);
                            FriendList.setAvatar(sender_address, spixi_message.data, resized_avatar, group_sender_address);
                            UIHelpers.shouldRefreshContacts = true;
                        }
                        else
                        {
                            //FriendList.setAvatar(sender_address, null, null, group_sender_address);
                        }
                        break;

                    case SpixiMessageCode.requestFunds:
                        Node.addMessageWithType(message.id, FriendMessageType.requestFunds, sender_address, 0, UTF8Encoding.UTF8.GetString(spixi_message.data));
                        break;

                    case SpixiMessageCode.sentFunds:
                        CoreProtocolMessage.broadcastGetTransaction(spixi_message.data, 0, endpoint);
                        Node.addMessageWithType(message.id, FriendMessageType.sentFunds, sender_address, 0, Transaction.getTxIdString(spixi_message.data));
                        break;

                    case SpixiMessageCode.chat:
                        Node.addMessageWithType(message.id, FriendMessageType.standard, sender_address, spixi_message.channel, Encoding.UTF8.GetString(spixi_message.data), false, group_sender_address, message.timestamp, fireLocalNotification, alert, 0);
                        if (friend != null && !friend.bot)
                        {
                            sendReceivedConfirmation(friend, sender_address, message.id, channel);
                        }
                        break;

                    case SpixiMessageCode.chatStream:
                        {
                            var csm = new ChatStreamMessage(spixi_message.data);
                            var fm = Node.addMessageWithType(FriendMessageType.standard, sender_address, spixi_message.channel, csm, false, group_sender_address, message.timestamp, fireLocalNotification, alert, 0);
                            if (fm == null)
                            {
                                fm = friend.getMessage(spixi_message.channel, csm.MessageId);
                                if (fm == null
                                    || fm.sequence >= csm.Sequence)
                                {
                                    // already have this message or a newer one, ignore
                                    sendReceivedConfirmation(friend, sender_address, message.id, channel);
                                    return null;
                                }
                            }
                            if (friend != null && !friend.bot)
                            {
                                sendReceivedConfirmation(friend, sender_address, message.id, channel);
                            }
                        }
                        break;

                    case SpixiMessageCode.msgReceived:
                    case SpixiMessageCode.msgRead:
                        {
                            var fm = friend.getMessage(channel, spixi_message.data);
                            if (fm != null)
                            {
                                UIHelpers.updateMessage(friend, channel, fm);
                            }
                            UIHelpers.shouldRefreshContacts = true;
                        }
                        break;

                    case SpixiMessageCode.msgDelete:
                        UIHelpers.deleteMessage(friend, channel, spixi_message.data);
                        UIHelpers.shouldRefreshContacts = true;
                        break;

                    case SpixiMessageCode.msgReaction:
                        var reaction = new ReactionMessage(spixi_message.data);
                        // If a chat page is not visible set unread indicator
                        if (!UIHelpers.isChatScreenDisplayed(friend))
                        {
                            friend.metaData.unreadMessageCount++;
                            friend.saveMetaData();
                        }
                        UIHelpers.updateReactions(friend, channel, reaction.msgId);
                        UIHelpers.shouldRefreshContacts = true;
                        break;

                    case SpixiMessageCode.leaveConfirmed:
                        UIHelpers.shouldRefreshContacts = true;
                        break;

                    case SpixiMessageCode.nick:
                        if (friend.bot && group_sender_address != null)
                        {
                            // update UI with the new nick
                            Logging.info("Updating group chat nicks");
                            var nick = friend.users.getUser(group_sender_address).getNick();
                            UIHelpers.updateGroupChatNicks(friend, group_sender_address, nick);
                        }else
                        {
                            UIHelpers.shouldRefreshContacts = true;
                        }
                        break;

                    case SpixiMessageCode.appProtocols:
                        handleAppProtocols(sender_address, new AppProtocolsMessage(spixi_message.data));
                        break;

                    case SpixiMessageCode.appProtocolData:
                        // app data received, find the protocol id of the app and forward the data to it
                        handleAppProtocolData(sender_address, spixi_message.data, group_sender_address);
                        break;

                    case SpixiMessageCode.transactionRequest:
                        {
                            var tr = new TransactionRequest(spixi_message.data);
                            var msgId = tr.RequestId;
                            if (tr.RequestId == null)
                            {
                                msgId = message.id;
                            }
                            Node.addMessageWithType(msgId, FriendMessageType.requestFunds, sender_address, 0, tr.Amount.ToString());
                            break;
                        }

                    case SpixiMessageCode.transactionSend:
                        {
                            var ts = new TransactionSend(spixi_message.data);
                            var msgId = ts.RequestId;
                            if (ts.RequestId == null)
                            {
                                msgId = message.id;
                            }
                            Node.addMessageWithType(msgId, FriendMessageType.sentFunds, sender_address, 0, ts.Transaction.getTxIdString());
                            UIHelpers.shouldRefreshTransactions = true;
                            break;
                        }

                    case SpixiMessageCode.createGroup:
                        {
                            UIHelpers.shouldRefreshContacts = true;
                            break;
                        }

                    case SpixiMessageCode.leave:
                        UIHelpers.shouldRefreshContacts = true;
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

        private static void handleAppProtocols(Address sender_address, AppProtocolsMessage data)
        {
            Friend? friend = FriendList.getFriend(sender_address);
            if (friend == null)
            {
                Logging.error("Received app protocols from an unknown contact.");
                return;
            }

            friend.supportedProtocols = data.protocolIds;
            friend.save();
        }

        private static void handleAppData(Address sender_address, byte[] app_data_raw, Address? group_sender_address)
        {
            if (group_sender_address == null)
            {
                group_sender_address = sender_address;
            }
            // TODO use channels and drop AppDataMessage
            AppDataMessage app_data = new AppDataMessage(app_data_raw);
            if (VoIPManager.hasSession(app_data.sessionId))
            {
                VoIPManager.onData(app_data.sessionId, app_data.data);
                return;
            }
            MiniAppPage? app_page = Node.MiniAppManager.getAppPage(group_sender_address, app_data.sessionId);
            if(app_page == null)
            {
                Logging.error("App with session id: {0} does not exist.", Crypto.hashToString(app_data.sessionId));
                return;
            }
            app_page.networkDataReceived(group_sender_address, app_data.data);
        }


        private static void handleAppProtocolData(Address sender_address, byte[] app_data_raw, Address? group_sender_address)
        {
            if (group_sender_address == null)
            {
                group_sender_address = sender_address;
            }
            // TODO use channels and drop AppDataMessage
            AppDataMessage app_data = new AppDataMessage(app_data_raw);
            MiniAppPage? app_page = Node.MiniAppManager.getAppPageByProtocol(group_sender_address, app_data.sessionId);
            if (app_page == null)
            {
                Logging.error("App with protocol id: {0} does not exist.", Crypto.hashToString(app_data.sessionId));
                return;
            }
            string protocol_name = Node.MiniAppManager.getApp(app_page.appId).getProtocolName(app_data.sessionId);
            app_page.networkProtocolDataReceived(group_sender_address, protocol_name, app_data.data);
        }

        private static byte[] sendAppRequest(Friend friend, string app_id, byte[] session_id, byte[] data)
        {
            string app_install_url = Node.MiniAppManager.getAppInstallURL(app_id);
            string app_name = Node.MiniAppManager.getAppName(app_id);
            string app_info = Node.MiniAppManager.getAppInfo(app_id);
            return sendAppRequest(friend, app_id, session_id, data, app_info);
        }

        private static void handleAppRequest(byte[] messageId, Address sender_address, Address recipient_address, byte[] app_data_raw)
        {
            MiniAppManager am = Node.MiniAppManager;

            Friend? friend = FriendList.getFriend(sender_address);
            if (friend == null)
            {
                Logging.error("Received app request from an unknown contact.");
                return;
            }

            if (!IxianHandler.getWalletStorage().isMyAddress(recipient_address))
            {
                return;
            }

            // TODO use channels and drop AppDataMessage
            AppDataMessage app_data = new AppDataMessage(app_data_raw);
            
            if(app_data.sessionId == null)
            {
                Logging.error("App session id is null.");
                return;
            }

            MiniAppPage app_page = am.getAppPage(sender_address, app_data.sessionId);
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
                Node.addMessageWithType(messageId, FriendMessageType.appSession, sender_address, 0, app_data.appId);

            });
        }

        private static void handleAppRequestAccept(Address sender_address, byte[] app_data_raw, Address? group_sender_address)
        {
            if (group_sender_address == null)
            {
                group_sender_address = sender_address;
            }
            // TODO use channels and drop AppDataMessage
            AppDataMessage app_data = new AppDataMessage(app_data_raw);

            if (VoIPManager.hasSession(app_data.sessionId))
            {
                VoIPManager.onAcceptedCall(app_data.sessionId, app_data.data);
                UIHelpers.refreshAppRequests = true;
                return;
            }

            MiniAppPage? page = Node.MiniAppManager.getAppPage(group_sender_address, app_data.sessionId);
            if(page == null)
            {
                Logging.info("App session does not exist.");
                return;
            }

            page.accepted = true;

            page.appRequestAcceptReceived(group_sender_address, app_data.data);

            UIHelpers.refreshAppRequests = true;
        }

        public static void handleAppRequestReject(Address sender_address, byte[] app_data_raw, Address? group_sender_address)
        {
            if (group_sender_address == null)
            {
                group_sender_address = sender_address;
            }
            // TODO use channels and drop AppDataMessage
            AppDataMessage app_data = new AppDataMessage(app_data_raw);
            byte[] session_id = app_data.sessionId;

            if (VoIPManager.hasSession(session_id))
            {
                VoIPManager.onRejectedCall(session_id);
                UIHelpers.refreshAppRequests = true;
                return;
            }

            MiniAppPage page = Node.MiniAppManager.getAppPage(group_sender_address, session_id);
            if (page == null)
            {
                Logging.info("App session does not exist.");
                return;
            }

            page.appRequestRejectReceived(group_sender_address, app_data.data);

            UIHelpers.refreshAppRequests = true;
        }

        public static void handleAppEndSession(Address sender_address, byte[] app_data_raw, Address? group_sender_address)
        {
            if (group_sender_address == null)
            {
                group_sender_address = sender_address;
            }
            // TODO use channels and drop SpixiAppData
            AppDataMessage app_data = new AppDataMessage(app_data_raw);
            byte[] session_id = app_data.sessionId;

            if (VoIPManager.hasSession(session_id))
            {
                VoIPManager.onHangupCall(session_id);
                UIHelpers.refreshAppRequests = true;
                return;
            }

            MiniAppPage? page = Node.MiniAppManager.getAppPage(group_sender_address, session_id);
            if (page == null)
            {
                Logging.info("App session does not exist.");
                return;
            }

            page.appEndSessionReceived(group_sender_address, app_data.data);
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
                    Node.addMessageWithType(null, FriendMessageType.kicked, bot.walletAddress, 0, SpixiLocalization._SL("chat-kicked"), true);
                    break;

                case SpixiBotActionCode.banUser:
                    Node.addMessageWithType(null, FriendMessageType.banned, bot.walletAddress, 0, SpixiLocalization._SL("chat-banned"), true);
                    break;
            }
        }
    }
}