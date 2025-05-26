using IXICore;
using IXICore.Meta;
using IXICore.Network;
using SPIXI.MiniApps;
using SPIXI.Interfaces;
using SPIXI.Meta;
using SPIXI.Storage;
using SPIXI.VoIP;
using System.Text;
using System.Web;
using SPIXI.Lang;
using IXICore.SpixiBot;
using Spixi;
using System.Net;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SingleChatPage : SpixiContentPage
    {
        private Friend friend = null;

        private uint messagesToShow = Config.messagesToLoad;

        private int lastMessageCount = 0;

        private int selectedChannel = 0;

        private bool _waitingForContactConfirmation = false;

        private HomePage? homePage;

        private bool reload = false;

        public SingleChatPage(Friend fr) : this(fr, null)
        {
        }

        public SingleChatPage(Friend fr, HomePage? home)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            webView.Opacity = 0;
            Content.BackgroundColor = ThemeManager.getBackgroundColor();

            friend = fr;
            friend.chat_page = this;
            Title = friend.nickname;
            selectedChannel = friend.metaData.lastMessageChannel;

            loadPage(webView, "chat.html");

            homePage = home;
        }

        public override void recalculateLayout()
        {
            ForceLayout();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (friend != null)
            {
                friend.chat_page = this;
            }

            if (reload)
            {
                reload = false;

                loadApps();
                loadMessages();
            }

        }


        protected override void OnDisappearing()
        {
            webView = null;
            base.OnDisappearing();
        }

        private void onNavigating(object sender, WebNavigatingEventArgs e)
        {
            string current_url = HttpUtility.UrlDecode(e.Url);
            e.Cancel = true;

            if (onNavigatingGlobal(current_url))
            {
                return;
            }

            if (current_url.Equals("ixian:onload", StringComparison.Ordinal))
            {
                onLoad();
            }
            else if (current_url.Equals("ixian:back", StringComparison.Ordinal))
            {
                friend.chat_page = null;

                if (Navigation.NavigationStack.Count > 1)
                {
                    try
                    {
                        Navigation.PopToRootAsync(false);
                    }
                    catch (Exception ex)
                    {
                        Logging.error($"Error during navigation: {ex.Message}");
                        return;
                    }
                }

            }
            else if (current_url.Equals("ixian:request", StringComparison.Ordinal))
            {
                onRequestIxi();
            }
            else if (current_url.Equals("ixian:details", StringComparison.Ordinal))
            {
                onContactDetails();
            }
            else if (current_url.Equals("ixian:send", StringComparison.Ordinal))
            {
                onSendIxi();
            }
            else if (current_url.Equals("ixian:accept", StringComparison.Ordinal))
            {
                onAcceptFriendRequest();
            }
            else if (current_url.Equals("ixian:loadmore", StringComparison.Ordinal))
            {
                onLoadMore();
            }
            else if (current_url.Equals("ixian:call", StringComparison.Ordinal))
            {
                if (VoIPManager.isInitiated())
                {
                    VoIPManager.hangupCall(null);
                }
                else
                {
                    VoIPManager.initiateCall(friend);
                }

            }
            else if (current_url.Equals("ixian:sendfile", StringComparison.Ordinal))
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                onSendFile();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            else if (current_url.StartsWith("ixian:acceptfile:"))
            {
                string id = current_url.Substring("ixian:acceptfile:".Length);

                FriendMessage fm = friend.getMessages(selectedChannel).Find(x => x.transferId == id);
                if (fm != null)
                {
                    onAcceptFile(selectedChannel, fm);
                }
                else
                {
                    Logging.error("Cannot find message with transfer id: {0}", id);
                }

            }
            else if (current_url.StartsWith("ixian:openfile:"))
            {
                string id = current_url.Substring("ixian:openfile:".Length);

                FriendMessage fm = friend.getMessages(selectedChannel).Find(x => x.transferId == id);

                // Open file in default app. May not work, check https://forums.xamarin.com/discussion/103042/how-to-open-pdf-or-txt-file-in-default-app-on-xamarin-forms
                //Device.OpenUri(new Uri(transfer.filePath));
                if (File.Exists(fm.filePath))
                {
                    SFileOperations.open(fm.filePath);
                }
                else
                {
                    // Handle special case for iOS
                    string filename = Path.GetFileName(fm.filePath);
                    string path = Path.Combine(TransferManager.downloadsPath, filename);
                    if (File.Exists(path))
                    {
                        SFileOperations.open(path);
                    }
                }
            }
            else if (current_url.StartsWith("ixian:chat:"))
            {
                string msg = current_url.Substring("ixian:chat:".Length);
                onSend(msg);
            }
            else if (current_url.StartsWith("ixian:viewPayment:"))
            {
                string tx_id = current_url.Substring("ixian:viewPayment:".Length);
                onViewPayment(tx_id);
            }
            else if (current_url.StartsWith("ixian:app:"))
            {
                string app_id = current_url.Substring("ixian:app:".Length);
                onApp(app_id);
            }
            else if (current_url.StartsWith("ixian:installApp:"))
            {
                string app_url = current_url.Substring("ixian:installApp:".Length);
                onInstallApp(app_url);
            }
            else if (current_url.StartsWith("ixian:joinApp:"))
            {
                string app_id = current_url.Substring("ixian:joinApp:".Length);
                onJoinApp(app_id);
            }
            else if (current_url.StartsWith("ixian:loadContacts"))
            {
                loadContacts();
            }
            else if (current_url.StartsWith("ixian:populateChannelSelector"))
            {
                populateChannelSelector();
            }
            else if (current_url.StartsWith("ixian:selectChannel:"))
            {
                int sel_channel = Int32.Parse(current_url.Substring("ixian:selectChannel:".Length));
                BotChannel channel = friend.channels.getChannel(sel_channel);
                if (channel != null)
                {
                    Utils.sendUiCommand(this, "setSelectedChannel", channel.index.ToString(), "fa-globe-africa", channel.channelName);
                    selectedChannel = sel_channel;
                    loadMessages();
                }
            }
            else if (current_url.StartsWith("ixian:contextAction:"))
            {
                string action = current_url.Substring("ixian:contextAction:".Length);
                action = action.Substring(0, action.IndexOf(':'));

                string msg_id = current_url.Substring("ixian:contextAction:".Length + action.Length + 1);
                onContextAction(action, msg_id);
            }
            else if (current_url.StartsWith("ixian:enableNotifications"))
            {
                friend.metaData.botInfo.sendNotification = true;
                friend.saveMetaData();
                StreamProcessor.sendBotAction(friend, SpixiBotActionCode.enableNotifications, new byte[1] { 1 }, 0, true);
            }
            else if (current_url.StartsWith("ixian:disableNotifications"))
            {
                friend.metaData.botInfo.sendNotification = false;
                friend.saveMetaData();
                StreamProcessor.sendBotAction(friend, SpixiBotActionCode.enableNotifications, new byte[1] { 0 }, 0, true);
            }
            else if (current_url.StartsWith("ixian:sendContactRequest:"))
            {
                Address address = new Address(current_url.Substring("ixian:sendContactRequest:".Length));
                Friend new_friend = FriendList.addFriend(FriendState.RequestSent, address, null, address.ToString(), null, null, 0);
                if (new_friend != null)
                {
                    new_friend.save();

                    StreamProcessor.sendContactRequest(new_friend);
                }
            }
            else if (current_url.StartsWith("ixian:kick:"))
            {
                string str_address = current_url.Substring("ixian:kick:".Length);
                Address address = new Address(str_address);
                onKickUser(address);
            }
            else if (current_url.StartsWith("ixian:ban:"))
            {
                string str_address = current_url.Substring("ixian:ban:".Length);
                Address address = new Address(current_url.Substring("ixian:ban:".Length));
                onBanUser(address);
            }
            else if (current_url.StartsWith("ixian:typing"))
            {
                StreamProcessor.sendTyping(friend);
            }
            else if(current_url.StartsWith("ixian:leave"))
            {
                if(friend.bot)
                {
                    friend.pendingDeletion = true;
                    friend.save();
                    Node.shouldRefreshContacts = true;
                    StreamProcessor.sendLeave(friend, null);
                    displaySpixiAlert(SpixiLocalization._SL("contact-details-removedcontact-title"), SpixiLocalization._SL("contact-details-removedcontact-text"), SpixiLocalization._SL("global-dialog-ok"));
                    Navigation.PopAsync();
                    homePage?.removeDetailContent();
                }
            }
            else if (current_url.StartsWith("ixian:openLink:", StringComparison.Ordinal))
            {
                string link = current_url.Substring("ixian:openLink:".Length);
                if (!link.Contains("://"))
                {
                    link = "http://" + link;
                }

                try
                {
                    string decoded_link = WebUtility.HtmlDecode(link);
#pragma warning disable CS0618 // Type or member is obsolete
                    Browser.Default.OpenAsync(new Uri(decoded_link));
#pragma warning restore CS0618 // Type or member is obsolete
                }catch(Exception ex)
                {
                    Logging.error("Exception occured while trying to open URL '{0}': {1}",  link, ex);
                }
            }
            else if (current_url.StartsWith("ixian:undorequest"))
            {
                // Remove friend from list and go back to the main screen
                FriendList.removeFriend(friend);
                Navigation.PopAsync();
                homePage?.removeDetailContent();

                // TODO: send a notification to the other party
            }
            else
            {               
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;
        }

        private void onLoadMore()
        {
            messagesToShow += Config.messagesToLoad;
            loadMessages();

        }

        private void onContactDetails()
        {
            if (homePage != null)
            {
                homePage.onContactDetails(friend);
                return;
            }

            Navigation.PushAsync(new ContactDetails(friend, true), Config.defaultXamarinAnimations);
        }

        private void onSendIxi()
        {
            if(homePage != null)
            {
                homePage.onSendIxi(friend.walletAddress);
                return;
            }

            Navigation.PushAsync(new WalletSendPage(friend.walletAddress), Config.defaultXamarinAnimations);
        }

        private void onRequestIxi()
        {
            if (homePage != null)
            {
                homePage.onReceiveIxi(friend);
                return;
            }

            Navigation.PushAsync(new WalletReceivePage(friend), Config.defaultXamarinAnimations);
        }

        private void populateChannelSelector()
        {
            var channels = friend.channels.channels;
            lock(channels)
            {
                foreach(var channel in channels.Values)
                {
                    string icon = "fa-globe-africa";
                    bool unread = false;
                    var messages = friend.getMessages(channel.index);
                    if (messages != null && messages.Count() > 0 && !messages.Last().localSender && !messages.Last().read)
                    {
                        unread = true;
                    }
                    Utils.sendUiCommand(this, "addChannelToSelector", channel.index.ToString(), channel.channelName, icon, unread.ToString());
                }
            }
        }

        private void setChannelSelectorUnread()
        {
            if(!friend.bot)
            {
                return;
            }

            var channels = friend.channels.channels;
            lock (channels)
            {
                foreach (var channel in channels.Values)
                {
                    bool unread = false;
                    var messages = friend.getMessages(channel.index);
                    if (messages != null && messages.Count() > 0 && !messages.Last().localSender && !messages.Last().read)
                    {
                        unread = true;
                    }
                    if(unread)
                    {
                        Utils.sendUiCommand(this, "setChannelSelectorStatus", "");
                    }
                }
            }
        }

        private void loadContacts()
        {
            var contacts = friend.users.contacts;
            lock (contacts)
            {
                foreach (var contact in contacts)
                {
                    string address = contact.Key.ToString();
                    string avatar = Node.localStorage.getAvatarPath(address);
                    if (avatar == null)
                    {
                        avatar = "img/spixiavatar.png";
                    }
                    int role = contact.Value.getPrimaryRole();
                    Utils.sendUiCommand(this, "addContact",  address, contact.Value.getNick(), avatar, role.ToString());
                }
            }
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            Utils.sendUiCommand(this, "onChatScreenReady", friend.walletAddress.ToString());

            if(homePage != null)
            {
                Utils.sendUiCommand(this, "hideBackButton");
            }

            if (friend.bot)
            {
                int sleep_cnt = 0;
                while (friend.metaData.botInfo == null || !friend.channels.hasChannel(friend.metaData.botInfo.defaultChannel))
                {
                    if (sleep_cnt >= 50)
                    {
                        Navigation.PopAsync(Config.defaultXamarinAnimations);
                        DisplayAlert(SpixiLocalization._SL("chat-bot-not-ready-title"), SpixiLocalization._SL("chat-bot-not-ready-body"), SpixiLocalization._SL("global-dialog-ok"));
                        return;
                    }
                    Thread.Sleep(100);
                    sleep_cnt++;
                }

                string cost_text = String.Format(SpixiLocalization._SL("chat-message-cost-bar"), friend.metaData.botInfo.cost.ToString() + " IXI");
                bool send_notification = friend.metaData.botInfo.sendNotification;
                    
                Utils.sendUiCommand(this, "setBotMode", friend.bot.ToString(), friend.metaData.botInfo.cost.ToString(), cost_text, friend.metaData.botInfo.admin.ToString(), friend.metaData.botInfo.serverDescription, send_notification.ToString());
                setChannelSelectorUnread();

                selectedChannel = 0; // TODO: remove this after groupchat UI improvements

                if (selectedChannel == 0 && friend.channels.channels.Count > 0)
                {
                    selectedChannel = friend.metaData.botInfo.defaultChannel;
                }
                if (selectedChannel != 0)
                {
                    BotChannel channel = friend.channels.getChannel(selectedChannel);
                    if (channel != null)
                    {
                        Utils.sendUiCommand(this, "setSelectedChannel", channel.index.ToString(), "fa-globe-africa", channel.channelName);
                    }
                }
                else
                {
                    selectedChannel = 0;
                }
            }else
            {
                Utils.sendUiCommand(this, "setBotMode", "False", "0.00000000", "", "False");
            }
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                if (SSpixiCodecInfo.getSupportedAudioCodecs().Count > 0 && friend.state == FriendState.Approved)
                {
                    Utils.sendUiCommand(this, "showCallButton", "");
                }

                loadApps();
            }).Start();

            // Execute timer-related functionality immediately
            updateScreen();

            loadMessages();

            Utils.sendUiCommand(this, "onChatScreenLoaded");

            if (FriendList.getUnreadMessageCount() == 0)
            {
                SPushService.clearNotifications();
            }

            if (!friend.bot)
            {
                if (friend.state == FriendState.RequestSent)
                {
                    _waitingForContactConfirmation = true;
                    Utils.sendUiCommand(this, "showRequestSentModal", "1");
                }
                else if (friend.state == FriendState.RequestReceived)
                {
                    Utils.sendUiCommand(this, "showContactRequest", "1");
                }
            }

            if (!Preferences.Default.ContainsKey("rating_action"))
            {
                Preferences.Default.Set("rating_action", "show");
            }

            Node.refreshAppRequests = true;

            webView.FadeTo(1, 150);

            webView.Focus();
        }


        public async void onSend(string str)
        {
            if (str.Length < 1)
            {
                return;
            }

            if(friend.bot)
            {
                if (friend.metaData.botInfo.cost > 0)
                {
                    IxiNumber message_cost = friend.getMessagePrice(str.Length);
                    if (message_cost > 0)
                    {
                        Transaction tx = new Transaction((int)Transaction.Type.Normal, message_cost, ConsensusConfig.forceTransactionPrice, friend.walletAddress, IxianHandler.getWalletStorage().getPrimaryAddress(), null, new Address(IxianHandler.getWalletStorage().getPrimaryPublicKey()), IxianHandler.getHighestKnownNetworkBlockHeight());
                        IxiNumber balance = IxianHandler.getWalletBalance(IxianHandler.getWalletStorage().getPrimaryAddress());
                        if (tx.amount + tx.fee > balance)
                        {
                            string alert_body = String.Format(SpixiLocalization._SL("wallet-error-balance-text"), tx.amount + tx.fee, balance);
                            await displaySpixiAlert(SpixiLocalization._SL("wallet-error-balance-title"), alert_body, SpixiLocalization._SL("global-dialog-ok"));
                            return;
                        }
                    }
                }
            }

            // Send the message
            SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.chat, Encoding.UTF8.GetBytes(str), selectedChannel);
            byte[] spixi_msg_bytes = spixi_message.getBytes();

            // store the message and display it
            FriendMessage friend_message = FriendList.addMessageWithType(null, FriendMessageType.standard, friend.walletAddress, selectedChannel, str, true, null, 0, true, spixi_msg_bytes.Length);

            // Finally, clear the input field
            Utils.sendUiCommand(this, "clearInput");


            StreamMessage message = new StreamMessage();
            message.type = StreamMessageCode.data;
            message.recipient = friend.walletAddress;
            message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            message.data = spixi_msg_bytes;
            message.id = friend_message.id;

            if (friend.bot)
            {
                message.encryptionType = StreamMessageEncryptionCode.none;
                message.sign(IxianHandler.getWalletStorage().getPrimaryPrivateKey());
            }

            StreamProcessor.sendMessage(friend, message);
        }

        public async Task onSendFile()
        {
            // Show file picker and send the file
            try
            {
                Stream stream = null;
                string fileName = null;
                string filePath = null;

                SpixiImageData spixi_img_data;
                if (Device.RuntimePlatform == Device.iOS)
                {
                    spixi_img_data = await SFilePicker.PickImageAsync();
                }
                else
                {
                    spixi_img_data = await SFilePicker.PickFileAsync();
                }

                if (spixi_img_data == null)
                {
                    return;
                }

                stream = spixi_img_data.stream;

                if (stream == null)
                {
                    return;
                }

                fileName = spixi_img_data.name;
                filePath = spixi_img_data.path;

                FileTransfer transfer = TransferManager.prepareFileTransfer(fileName, stream, filePath);
                transfer.channel = selectedChannel;
                Logging.info("File Transfer uid: " + transfer.uid);

                SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.fileHeader, transfer.getBytes(), selectedChannel);

                StreamMessage message = new StreamMessage();
                message.type = StreamMessageCode.data;
                message.recipient = friend.walletAddress;
                message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                message.data = spixi_message.getBytes();

                StreamProcessor.sendMessage(friend, message);


                string message_data = string.Format("{0}:{1}", transfer.uid, transfer.fileName);

                // store the message and display it
                FriendMessage friend_message = FriendList.addMessageWithType(message.id, FriendMessageType.fileHeader, friend.walletAddress, selectedChannel, message_data, true);

                friend_message.transferId = transfer.uid;
                friend_message.filePath = transfer.filePath;

                Node.localStorage.requestWriteMessages(friend.walletAddress, selectedChannel);
            }
            catch (Exception ex)
            {
                Logging.error("Exception choosing file: " + ex.ToString());
            }
        }

        public void onAcceptFile(int selected_channel, FriendMessage message)
        {
            if (TransferManager.getIncomingTransfer(message.transferId) != null)
            {
                Logging.warn("Incoming file transfer {0} already prepared.", message.transferId);
                return;
            }

            //displaySpixiAlert("File", uid, "Ok");
            string file_name = System.IO.Path.GetFileName(message.filePath);

            var ft = new FileTransfer();
            ft.fileName = file_name;
            ft.fileSize = message.fileSize;
            ft.uid = message.transferId;
            ft.channel = selected_channel;

            ft = TransferManager.prepareIncomingFileTransfer(ft.getBytes(), friend.walletAddress);

            if (ft != null)
            {
                TransferManager.acceptFile(friend, ft.uid);
                updateFile(ft.uid, "0", false);
            }
        }

        public void onAcceptFriendRequest()
        {
            friend.approved = true;

            friend.handshakePushed = false;

            Node.shouldRefreshContacts = true;

            StreamProcessor.sendAcceptAdd(friend, true);
        }

        public void onViewPayment(string msg_id)
        {
            FriendMessage msg = friend.getMessages(selectedChannel).Find(x => x.id.SequenceEqual(Crypto.stringToHash(msg_id)));

            if(msg.type == FriendMessageType.sentFunds || msg.message.StartsWith(":"))
            {
                string id = msg.message;
                if(id.StartsWith(":"))
                {
                    id = id.Substring(1);
                }
                byte[] b_id = Transaction.txIdLegacyToV8(id);

                Transaction transaction = TransactionCache.getTransaction(b_id);
                if (transaction == null)
                {
                    transaction = TransactionCache.getUnconfirmedTransaction(b_id);
                    if (transaction == null)
                    {
                        return;
                    }
                }

                if (homePage != null)
                {
                    homePage.onTransaction(b_id, null);
                    return;
                }

                Navigation.PushAsync(new WalletSentPage(transaction), Config.defaultXamarinAnimations);

                return;
            }

            if(msg.type == FriendMessageType.requestFunds && !msg.localSender)
            {
                onConfirmPaymentRequest(msg, msg.message);
            }
        }

        public void onConfirmPaymentRequest(FriendMessage msg, string amount)
        {
            // TODO: extract the date from the corresponding message
            DateTime dt = DateTime.Now;
            string date_text = String.Format("{0:t}", dt);

            if (homePage != null)
            {
                homePage.onConfirmPaymentRequest(msg, friend, amount, date_text);
                return;
            }

            Navigation.PushAsync(new WalletContactRequestPage(msg, friend, amount, date_text), Config.defaultXamarinAnimations);
        }

        public void onApp(string app_id)
        {
            Address[] user_addresses = new Address[] { friend.walletAddress };

            byte[]? session_id = null;
            if (homePage != null)
            {
                session_id = homePage.onJoinApp(app_id, user_addresses);
            }
            else
            {
                MiniAppPage custom_app_page = new MiniAppPage(app_id, IxianHandler.getWalletStorage().getPrimaryAddress(), user_addresses, Node.MiniAppManager.getAppEntryPoint(app_id));
                custom_app_page.accepted = true;
                Node.MiniAppManager.addAppPage(custom_app_page);
                session_id = custom_app_page.sessionId;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Navigation.PushAsync(custom_app_page, Config.defaultXamarinAnimations);
                });
            }


            if(session_id == null)
            {
                return;
            }

            var msg = StreamProcessor.sendAppRequest(friend, app_id, session_id, null);
            var app_info = Node.MiniAppManager.getAppInfo(app_id);
            FriendList.addMessageWithType(msg.id, FriendMessageType.appSession, friend.walletAddress, 0, app_info, true, null, 0, false);
        }

        public void onJoinApp(string app_id)
        {
            
            Address[] user_addresses = new Address[] { friend.walletAddress };
            if (homePage != null)
            {
                homePage.onJoinApp(app_id, user_addresses);
                return;
            }

            MiniAppPage miniAppPage = new MiniAppPage(app_id, IxianHandler.getWalletStorage().getPrimaryAddress(), user_addresses, Node.MiniAppManager.getAppEntryPoint(app_id));
            miniAppPage.accepted = true;
            Node.MiniAppManager.addAppPage(miniAppPage);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Navigation.PushAsync(miniAppPage, Config.defaultXamarinAnimations);
            });

        }

        public async void onInstallApp(string app_url)
        {
            if (homePage != null)
            {
                homePage.onInstallApp(app_url);
                return;
            }

            MiniApp? app = await Node.MiniAppManager.fetch(app_url);
            if (app == null)
            {
                return;
            }

            app.url = app_url;

            reload = true;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Navigation.PushAsync(new AppDetailsPage(app, true), Config.defaultXamarinAnimations);
            });
        }

        private void onKickUser(Address address)
        {
            string str_address = address.ToString();
            StreamProcessor.sendBotAction(friend, SpixiBotActionCode.kickUser, address.addressWithChecksum, 0, true);
            string modal_title = String.Format(SpixiLocalization._SL("chat-modal-kicked-title"), str_address);
            string modal_body = String.Format(SpixiLocalization._SL("chat-modal-kicked-body"), str_address);
            displaySpixiAlert(modal_title, modal_body, SpixiLocalization._SL("global-dialog-ok"));

        }

        private void onBanUser(Address address)
        {
            string str_address = address.ToString();
            StreamProcessor.sendBotAction(friend, SpixiBotActionCode.banUser, address.addressWithChecksum, 0, true);
            string modal_title = String.Format(SpixiLocalization._SL("chat-modal-banned-title"), str_address);
            string modal_body = String.Format(SpixiLocalization._SL("chat-modal-banned-body"), str_address);
            displaySpixiAlert(modal_title, modal_body, SpixiLocalization._SL("global-dialog-ok"));
        }


        private void onContextAction(string action, string msg_id_hex)
        {
            string data = "";
            if (msg_id_hex.Contains(':'))
            {
                int sep_offset = msg_id_hex.IndexOf(':');
                data = msg_id_hex.Substring(sep_offset + 1);
                msg_id_hex = msg_id_hex.Substring(0, sep_offset);
            }
            byte[] msg_id = Crypto.stringToHash(msg_id_hex);
            switch(action)
            {
                case "tip":
                    FriendMessage msg = friend.getMessages(selectedChannel).Find(x => x.id.SequenceEqual(msg_id));
                    Address sender_address = msg.senderAddress;
                    if(!friend.bot)
                    {
                        sender_address = friend.walletAddress;
                    }
                    IxiNumber amount = new IxiNumber(data);
                    Transaction tx = new Transaction((int)Transaction.Type.Normal, amount, ConsensusConfig.forceTransactionPrice, sender_address, IxianHandler.getWalletStorage().getPrimaryAddress(), null, new Address(IxianHandler.getWalletStorage().getPrimaryPublicKey()), IxianHandler.getHighestKnownNetworkBlockHeight());
                    IxiNumber balance = IxianHandler.getWalletBalance(IxianHandler.getWalletStorage().getPrimaryAddress());
                    if(tx.amount <= 0)
                    {
                        displaySpixiAlert(SpixiLocalization._SL("wallet-error-amount-title"), SpixiLocalization._SL("wallet-error-amount-text"), SpixiLocalization._SL("global-dialog-ok"));
                        return;
                    }
                    if (tx.amount + tx.fee > balance)
                    {
                        string alert_body = String.Format(SpixiLocalization._SL("wallet-error-balance-text"), tx.amount + tx.fee, balance);
                        displaySpixiAlert(SpixiLocalization._SL("wallet-error-balance-title"), alert_body, SpixiLocalization._SL("global-dialog-ok"));
                    }
                    else
                    {
                        string nick = friend.nickname;
                        if (friend.bot)
                        {
                            nick = friend.users.getUser(sender_address).getNick();
                        }
                        string modal_title = String.Format(SpixiLocalization._SL("chat-modal-tip-title"), nick);
                        if (friend.addReaction(IxianHandler.getWalletStorage().getPrimaryAddress(), new SpixiMessageReaction(msg_id, "tip:" + tx.id), selectedChannel))
                        {
                            StreamProcessor.sendReaction(friend, msg_id, "tip:" + tx.id, selectedChannel);
                            IxianHandler.addTransaction(tx, true);
                            TransactionCache.addUnconfirmedTransaction(tx);
                            string modal_body = String.Format(SpixiLocalization._SL("chat-modal-tip-confirmed-body"), nick, amount.ToString() + " IXI");
                            displaySpixiAlert(modal_title, modal_body, SpixiLocalization._SL("global-dialog-ok"));
                        }
                        else
                        {
                            displaySpixiAlert(modal_title, SpixiLocalization._SL("chat-modal-tip-error-body"), SpixiLocalization._SL("global-dialog-ok"));
                        }
                    }
                    break;

                case "sendContactRequest":
                    Address new_friend_address = friend.getMessages(selectedChannel).Find(x => x.id.SequenceEqual(msg_id)).senderAddress;
                    Friend new_friend = FriendList.addFriend(FriendState.RequestSent, new_friend_address, null, new_friend_address.ToString(), null, null, 0);
                    if (new_friend != null)
                    {
                        new_friend.save();

                        StreamProcessor.sendContactRequest(new_friend);
                    }
                    break;

                case "kickUser":
                    onKickUser(friend.getMessages(selectedChannel).Find(x => x.id.SequenceEqual(msg_id)).senderAddress);
                    break;

                case "banUser":
                    onBanUser(friend.getMessages(selectedChannel).Find(x => x.id.SequenceEqual(msg_id)).senderAddress);
                    break;

                case "report":
                    if (friend.bot)
                    {
                        StreamProcessor.sendMsgReport(friend, msg_id, selectedChannel);
                        friend.deleteMessage(msg_id, selectedChannel);
                    }
                    break;

                case "deleteMessage":
                    StreamProcessor.sendMsgDelete(friend, msg_id, selectedChannel);
                    if (!friend.bot)
                    {
                        friend.deleteMessage(msg_id, selectedChannel);
                    }
                    break;

                case "like":
                    if (friend.addReaction(IxianHandler.getWalletStorage().getPrimaryAddress(), new SpixiMessageReaction(msg_id, "like:"), selectedChannel))
                    {
                        StreamProcessor.sendReaction(friend, msg_id, "like:", selectedChannel);
                    }
                    break;
            }
        }

        private void onEntryCompleted(object sender, EventArgs e)
        {

        }

        public void loadApps()
        {
            Utils.sendUiCommand(this, "clearApps");
            var apps = Node.MiniAppManager.getInstalledApps();
            lock (apps)
            {
                foreach (MiniApp app in apps.Values)
                {
                    try
                    {
                        if (!app.hasCapability(MiniAppCapabilities.MultiUser))
                        {
                            continue;
                        }

                        string icon = Node.MiniAppManager.getAppIconPath(app.id);
                        if (icon == null)
                        {
                            icon = "";
                        }
                        Utils.sendUiCommand(this, "addApp", app.id, app.name, icon, app.publisher);
                    }
                    catch (Exception e)
                    {
                        Logging.error("Exception while loading app '{0}': {1}", app.id, e);
                    }
                }
            }
        }

        public void loadMessages()
        {
            var messages = friend.getMessages(selectedChannel, (int)messagesToShow);
            string show_more = "true";
            if (messages.Count < messagesToShow)
                show_more = "false";
            Utils.sendUiCommand(this, "clearMessages", show_more);
            if (friend.handshakeStatus < 2)
            {
                if (friend.handshakeStatus == 1 && friend.state == FriendState.Approved)
                {
                    Utils.sendUiCommand(this, "showContactRequest", "1");
                }
                return;
            }

            
            lock (messages)
            {
                int skip_messages = 0;
                if(messages.Count > messagesToShow)
                {
                    skip_messages = messages.Count() - (int)messagesToShow;
                }
                foreach (FriendMessage message in messages)
                {
                    if(skip_messages > 0)
                    {
                        skip_messages--;
                        continue;
                    }
                    try
                    {
                        insertMessage(message, selectedChannel);
                    }catch(Exception e)
                    {
                        Logging.error("Error loading message: {0}", e);
                    }
                    updateReactions(message);
                }
            }
        }

        public void insertMessage(FriendMessage message, int channel)
        {
            if(channel != selectedChannel)
            {
                return;
            }
            if(friend.state != FriendState.Approved)
            {
                if (message.type == FriendMessageType.requestAdd)
                {

                    // Call webview methods on the main UI thread only
                    friend.state = FriendState.RequestReceived;
                    Utils.sendUiCommand(this, "showContactRequest", "1");
                    return;
                }
            }
            else
            {
                // Don't show if the friend is already approved
                if (message.type == FriendMessageType.requestAdd)
                    return;
            }

            bool paid = false;
            if (message.transactionId != "")
            {
                paid = true;
            }
            string prefix = "addMe";
            string avatar = "";
            string address = friend.nickname;
            if(address == "")
            {
                address = message.senderAddress.ToString();
            }
            string nick = "";
            if (!message.localSender)
            {
                if (friend.bot)
                {
                    if (message.senderAddress != null)
                    {
                        address = message.senderAddress.ToString();
                    }

                    nick = message.senderNick;
                    if (nick == "")
                    {
                        if (message.senderAddress != null && friend.users.hasUser(message.senderAddress))
                        {
                            nick = friend.users.getUser(message.senderAddress).getNick();
                        }
                    }

                    if (nick == "")
                    {
                        nick = address;
                    }
                }

                prefix = "addThem";
                if(message.senderAddress != null)
                {
                    avatar = Node.localStorage.getAvatarPath(message.senderAddress.ToString());
                }else
                {
                    avatar = Node.localStorage.getAvatarPath(friend.walletAddress.ToString());
                }
                if (avatar == null)
                {
                    avatar = "img/spixiavatar.png";
                }
            }

            if (message.type == FriendMessageType.requestFunds)
            {
                string status = SpixiLocalization._SL("chat-payment-status-waiting-confirmation");
                string status_icon = "fa-clock";

                string amount = message.message.Trim(':');

                string txid = "";

                bool enableView = false;

                if(!message.localSender)
                {
                    enableView = true;
                }

                if (message.message.StartsWith("::"))
                {
                    status = SpixiLocalization._SL("chat-payment-status-declined");
                    status_icon = "fa-exclamation-circle";
                    txid = Crypto.hashToString(message.id);
                    enableView = false;
                }else if(message.message.StartsWith(":"))
                {
                    status = SpixiLocalization._SL("chat-payment-status-pending");
                    txid = message.message.Substring(1);
                    byte[] b_txid = Transaction.txIdLegacyToV8(txid);

                    bool confirmed = true;
                    Transaction transaction = TransactionCache.getTransaction(b_txid);
                    if (transaction == null)
                    {
                        transaction = TransactionCache.getUnconfirmedTransaction(b_txid);
                        confirmed = false;
                    }

                    amount = "?";

                    if (transaction != null)
                    {
                        amount = transaction.amount.ToString();

                        if (confirmed)
                        {
                            status = SpixiLocalization._SL("chat-payment-status-confirmed");
                            status_icon = "fa-check-circle";
                        }
                    }
                    else
                    {
                        // TODO think about how to make this more private
                        CoreProtocolMessage.broadcastGetTransaction(Transaction.txIdLegacyToV8(txid), 0, null);
                    }
                    enableView = true;
                }


                if (message.localSender)
                {
                    Utils.sendUiCommand(this, "addPaymentRequest", Crypto.hashToString(message.id), txid, address, nick, avatar, SpixiLocalization._SL("chat-payment-request-sent"), amount, status, status_icon, message.timestamp.ToString(), message.localSender.ToString(), message.confirmed.ToString(), message.read.ToString(), enableView.ToString());
                }
                else
                {
                    Utils.sendUiCommand(this, "addPaymentRequest", Crypto.hashToString(message.id), txid, address, nick, avatar, SpixiLocalization._SL("chat-payment-request-received"), amount, status, status_icon, message.timestamp.ToString(), "", message.confirmed.ToString(), message.read.ToString(), enableView.ToString());
                }
            }

            if (message.type == FriendMessageType.sentFunds)
            {
                bool confirmed = true;
                byte[] b_txid = Transaction.txIdLegacyToV8(message.message);
                Transaction transaction = TransactionCache.getTransaction(b_txid);
                if (transaction == null)
                {
                    transaction = TransactionCache.getUnconfirmedTransaction(b_txid);
                    confirmed = false;
                }

                string status = SpixiLocalization._SL("chat-payment-status-pending");
                string status_icon = "fa-clock";

                string amount = "?";

                if (transaction != null)
                {
                    if (Node.networkBlockHeight > transaction.blockHeight + Config.txConfirmationBlocks)
                    {
                        transaction.applied = transaction.blockHeight + Config.txConfirmationBlocks;
                        confirmed = true;
                    }

                    if (confirmed)
                    {
                        status = SpixiLocalization._SL("chat-payment-status-confirmed");
                        status_icon = "fa-check-circle";
                    }
                    if(message.localSender)
                    {
                        amount = transaction.amount.ToString();
                    }else
                    {
                        amount = HomePage.calculateReceivedAmount(transaction).ToString();
                    }
                }
                else
                {
                    // TODO think about how to make this more private
                    CoreProtocolMessage.broadcastGetTransaction(Transaction.txIdLegacyToV8(message.message), 0, null);
                }

                // Call webview methods on the main UI thread only
                if (message.localSender)
                {
                    Utils.sendUiCommand(this, "addPaymentRequest", Crypto.hashToString(message.id), message.message, address, nick, avatar, SpixiLocalization._SL("chat-payment-sent"), amount, status, status_icon, message.timestamp.ToString(), message.localSender.ToString(), message.confirmed.ToString(), message.read.ToString(), "True");
                }
                else
                {
                    Utils.sendUiCommand(this, "addPaymentRequest", Crypto.hashToString(message.id), message.message, address, nick, avatar, SpixiLocalization._SL("chat-payment-received"), amount, status, status_icon, message.timestamp.ToString(), "", message.confirmed.ToString(), message.read.ToString(), "True");
                }
            }


            if (message.type == FriendMessageType.fileHeader)
            {
                string[] split = message.message.Split(new string[] { ":" }, StringSplitOptions.None);
                if (split != null && split.Length > 1)
                {
                    string uid = split[0];
                    string name = split[1];

                    string progress = "0";
                    if(message.completed)
                    {
                        progress = "100";
                    }
                    Utils.sendUiCommand(this, "addFile", Crypto.hashToString(message.id), address, nick, avatar, uid, name, message.timestamp.ToString(), message.localSender.ToString(), message.confirmed.ToString(), message.read.ToString(), progress, message.completed.ToString(), paid.ToString());
                }
            }

            if (message.type == FriendMessageType.appSession)
            {
                MiniAppManager am = Node.MiniAppManager;

                string app_id;
                string app_install_url = "";
                string app_name = "";
                string app_image = "img/app-noicon.jpg";
                if (message.message.Contains("||"))
                {
                    string[] app_id_data = message.message.Split(new[] { "||" }, StringSplitOptions.None);
                    app_id = app_id_data[0];
                    app_install_url = app_id_data.Length > 1 ? app_id_data[1] : "";
                    app_name = app_id_data.Length > 2 ? app_id_data[2] : "";
                }
                else
                {
                    app_id = message.message;
                }


                MiniApp app = am.getApp(app_id);
                string app_state = "";
                
                if (app == null)
                {
                    app_state = "Missing";
                }
                else
                {
                    app_name = app.name;
                    app_image = $"Apps/{app_id}/icon.png";
                }


                if (message.localSender)
                {
                    Utils.sendUiCommand(this, "addAppRequest", Crypto.hashToString(message.id), app_id, app_name, app_image, address, nick, avatar, message.timestamp.ToString(), message.localSender.ToString(), message.confirmed.ToString(), message.read.ToString(), app_state, app_install_url);
                }
                else
                {
                    Utils.sendUiCommand(this, "addAppRequest", Crypto.hashToString(message.id), app_id, app_name, app_image, address, nick, avatar, message.timestamp.ToString(), message.localSender.ToString(), message.confirmed.ToString(), message.read.ToString(), app_state, app_install_url);
                }
            }

            if (message.type == FriendMessageType.standard)
            {
                // Normal chat message
                // Call webview methods on the main UI thread only
                Utils.sendUiCommand(this, prefix, Crypto.hashToString(message.id), address, nick, avatar, message.message, message.timestamp.ToString(), message.sent.ToString(), message.confirmed.ToString(), message.read.ToString(), paid.ToString());
            }

            if(message.type == FriendMessageType.voiceCall || message.type == FriendMessageType.voiceCallEnd)
            {
                string text;
                if(message.localSender)
                {
                    text = SpixiLocalization._SL("chat-call-outgoing");
                }else
                {
                    text = SpixiLocalization._SL("chat-call-incoming");
                }
                bool declined = false;
                if(message.message == "")
                {
                    if(message.type == FriendMessageType.voiceCallEnd || !VoIPManager.hasSession(message.id))
                    {
                        declined = true;
                        if (message.localSender)
                        {
                            text = SpixiLocalization._SL("chat-call-no-answer");
                        }
                        else
                        {
                            text = SpixiLocalization._SL("chat-call-missed");
                        }
                    }
                }else if(message.type == FriendMessageType.voiceCallEnd)
                {
                    long seconds = Int32.Parse(message.message);
                    long minutes = seconds / 60;
                    seconds = seconds % 60;
                    text = string.Format("{0} ({1}:{2})", text, minutes, seconds < 10 ? "0" + seconds : seconds.ToString());
                }
                Utils.sendUiCommand(this, "addCall", Crypto.hashToString(message.id), text, declined.ToString(), message.timestamp.ToString());
            }

            updateMessageReadStatus(message, channel);
        }

        private void updateMessageReadStatus(FriendMessage message, int channel)
        {
            if (App.isInForeground && friend.metaData.unreadMessageCount > 0)
            {
                // TODO improve this by reducing the number of unread messages by unread message
                // TODO make sure to handle edge cases like deleted message
                friend.metaData.unreadMessageCount = 0;
                friend.saveMetaData();
            }
            if (!message.read && !message.localSender && App.isInForeground && message.type != FriendMessageType.requestAdd)
            {
                message.read = true;

                Node.localStorage.requestWriteMessages(friend.walletAddress, channel);

                UIHelpers.setContactStatus(friend.walletAddress, friend.online, friend.getUnreadMessageCount(), "", 0);

                if (!friend.bot)
                {
                    // Send read confirmation
                    StreamMessage msg_received = new StreamMessage();
                    msg_received.type = StreamMessageCode.info;
                    msg_received.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                    msg_received.recipient = friend.walletAddress;
                    msg_received.data = new SpixiMessage(SpixiMessageCode.msgRead, message.id, selectedChannel).getBytes();

                    StreamProcessor.sendMessage(friend, msg_received, true, true, false, true);
                }
            }
        }

        public void updateMessagesReadStatus()
        {
            if(friend == null)
            {
                return;
            }
            if(friend.metaData.lastMessageChannel == selectedChannel)
            {
                if (!friend.metaData.lastMessage.read && !friend.metaData.lastMessage.localSender && App.isInForeground)
                {
                    friend.metaData.lastMessage.read = true;
                    friend.saveMetaData();
                }
            }
            var messages = friend.getMessages(selectedChannel);
            lock (messages)
            {
                int max_msg_count = 0;
                if (messages.Count > 50)
                {
                    max_msg_count = messages.Count - 50;
                }

                for (int i = messages.Count - 1; i >= max_msg_count; i--)
                {
                    FriendMessage msg = messages[i];
                    updateMessageReadStatus(msg, selectedChannel);
                }
            }
            if (friend.metaData.unreadMessageCount > 0)
            {
                friend.metaData.unreadMessageCount = 0;
                friend.saveMetaData();
            }
        }

        public void deleteMessage(byte[] msg_id, int channel)
        {
            if (channel == selectedChannel)
            {
                Utils.sendUiCommand(this, "deleteMessage", Crypto.hashToString(msg_id));
            }
        }

        public void showTyping()
        {
            Utils.sendUiCommand(this, "showUserTyping");
        }

        public void updateReactions(byte[] msg_id, int channel)
        {
            if (channel == selectedChannel)
            {
                FriendMessage fm = friend.getMessages(channel).Find(x => x.id.SequenceEqual(msg_id));
                if (fm != null)
                {
                    updateReactions(fm);
                }
            }
        }

        private void updateReactions(FriendMessage fm)
        {
            var reactions_str = "";
            foreach (var reaction in fm.reactions)
            {
                reactions_str += reaction.Key + ":" + reaction.Value.Count() + ";";
            }
            Utils.sendUiCommand(this, "addReactions", Crypto.hashToString(fm.id), reactions_str);
        }

        public void updateMessage(FriendMessage message)
        {
            bool paid = false;
            if(message.transactionId != "")
            {
                paid = true;
            }
            Utils.sendUiCommand(this, "updateMessage", Crypto.hashToString(message.id), message.message, message.sent.ToString(), message.confirmed.ToString(), message.read.ToString(), paid.ToString());
        }

        public void updateFile(string uid, string progress, bool complete)
        {
            Utils.sendUiCommand(this, "updateFile", uid, progress, complete.ToString());
        }

        public void updateGroupChatNicks(Address address, string nick)
        {
            Utils.sendUiCommand(this, "updateGroupChatNicks", address.ToString(), nick);
        }

        public void updateTransactionStatus(string txid, bool verified)
        {
            string status = SpixiLocalization._SL("chat-payment-status-pending");
            string status_icon = "fa-clock";

            if (verified)
            {
                status = SpixiLocalization._SL("chat-payment-status-confirmed");
                status_icon = "fa-check-circle";
            }

            Utils.sendUiCommand(this, "updateTransactionStatus", txid, status, status_icon);
        }

        public void updateRequestFundsStatus(byte[] msg_id, byte[]? txid, string status)
        {
            string status_icon = "fa-clock";
            bool enableView = true;
            if(status == SpixiLocalization._SL("chat-payment-status-declined"))
            {
                status_icon = "fa-exclamation-circle";
                enableView = false;
            }

            string txid_string = "";
            if (txid != null)
                txid_string = Transaction.getTxIdString(txid);

            Utils.sendUiCommand(this, "updatePaymentRequestStatus", Crypto.hashToString(msg_id), txid_string, status, status_icon, enableView.ToString());
        }

        public void convertToBot()
        {
            if (homePage != null)
            {
                Navigation.PopToRootAsync();
                friend.chat_page = null;
                homePage.removeDetailContent();
                homePage.onChat(friend.walletAddress.ToString(), null);
            }
        }
        
        // Executed every second
        public override void updateScreen()
        {
            base.updateScreen();

            Utils.sendUiCommand(this, "setNickname", friend.nickname);

            if(friend.bot)
            {
                long userCount = 0;
                if(friend.metaData != null && friend.metaData.botInfo != null)
                {
                    userCount = friend.metaData.botInfo.userCount;
                }
                Utils.sendUiCommand(this, "setOnlineStatus", String.Format(SpixiLocalization._SL("chat-member-count"), userCount));
            }
            else
            {
                if (friend.state == FriendState.Approved)
                {
                    if (friend.online)
                    {
                        Utils.sendUiCommand(this, "setOnlineStatus", SpixiLocalization._SL("chat-online"));
                    }
                    else
                    {
                        Utils.sendUiCommand(this, "setOnlineStatus", SpixiLocalization._SL("chat-offline"));
                    }

                    if(_waitingForContactConfirmation)
                    {
                        _waitingForContactConfirmation = false;
                        Utils.sendUiCommand(this, "showRequestSentModal", "0");
                    }
                }
                else if(friend.state == FriendState.RequestSent || friend.state == FriendState.RequestReceived)
                {
                    Utils.sendUiCommand(this, "setOnlineStatus", SpixiLocalization._SL("chat-waiting-for-response"));
                }

            }

            // Show connectivity warning bar
            if (NetworkClientManager.getConnectedClients(true).Count() > 0)
            {
                if (!Config.enablePushNotifications && (friend.relayIP == null || StreamClientManager.isConnectedTo(friend.relayIP, true) == null))
                {
                    Utils.sendUiCommand(this, "showWarning", SpixiLocalization._SL("global-connecting-s2"));
                }
                else
                {
                    Utils.sendUiCommand(this, "showWarning", "");
                }
            }
            else
            {
                Utils.sendUiCommand(this, "showWarning", SpixiLocalization._SL("global-connecting-dlt"));
            }
            
                
            // Show the messages indicator
            int msgCount = FriendList.getUnreadMessageCount();
            //if(msgCount != lastMessageCount)
            {
                lastMessageCount = msgCount;
                Utils.sendUiCommand(this, "setUnreadIndicator", string.Format("{0}", lastMessageCount));
            }
        }

        protected override bool OnBackButtonPressed()
        {
            friend.chat_page = null;

            Navigation.PopAsync(Config.defaultXamarinAnimations);

            return true;
        }

        public override void onResume()
        {
            base.onResume();

            if(FriendList.getUnreadMessageCount() == 0)
            {
                SPushService.clearNotifications();
            }

            updateMessagesReadStatus();
        }
    }
}