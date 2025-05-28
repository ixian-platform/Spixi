using IXICore;
using IXICore.Meta;
using IXICore.Network;
using Spixi;
using SPIXI.Lang;
using SPIXI.Meta;
using SPIXI.MiniApps;
using SPIXI.Storage;
using System.IO.Compression;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class HomePage : SpixiContentPage
	{
        private static HomePage? _singletonInstance;

        private SpixiContentPage? detailContent = null;
        private SpixiContentPage defaultDetailContent = new EmptyDetail();

        public static HomePage Instance(bool force_new = false)
        {
            if(force_new)
            {
                if (_singletonInstance != null)
                {
                    _singletonInstance.stop();
                    _singletonInstance = null;
                }
            }
            if (_singletonInstance == null)
            {
                _singletonInstance = new HomePage();
            }
            return _singletonInstance;
        }

        // Temporary optimizations for native->js data transfer
        private ulong lastTransactionChange = 9999;
        private ushort transactionFilter = 0; // 0-All 1-Sent 2-Received

        private string currentTab = "tab1";
        private bool hideBalance = false;

        private bool running = false;

        private bool fromSettings = false;
        private bool fromChat = false;

        // Interal cache object to store contact status items
        private struct contactStatusCacheItem
        {
            public Address address;
            public bool online;
            public int unread;
            public string excerpt;
            public long timestamp;
        }
        private static List<contactStatusCacheItem> contactStatusCache = new List<contactStatusCacheItem>();


        private HomePage ()
		{
            Node.preStart();

            InitializeComponent();
            NavigationPage.SetHasBackButton(this, false);
            NavigationPage.SetHasNavigationBar(this, false);
            this.Title = "SPIXI";
            webView.Opacity = 0;

            if (Preferences.Default.ContainsKey("hidebalance"))
            {
                hideBalance = (bool)Preferences.Default.Get("hidebalance", false);
            }

            loadPage(webView, "index.html");

            rightContent.Content = defaultDetailContent.Content;

            this.SizeChanged += OnPageSizeChanged;

            if (!running)
            {
                running = true;

                new Thread(() =>
                {
                    try
                    {
                        Node.start();
                        Node.connectToNetwork();
                    }catch(Exception e)
                    {
                        Logging.error("Fatal error has occured: " + e);
                        displaySpixiAlert("Fatal exception", "Fatal exception has occured, please send the log files to the developers." + e.Message, "OK");
                    }
                }).Start();

                // Setup a timer to handle UI updates
                IDispatcherTimer timer = Dispatcher.CreateTimer();
                timer.Interval = TimeSpan.FromMilliseconds(2000);
                timer.Tick += (s, e) =>
                {
                    onUpdateUI();
                    if (!running)
                        timer.Stop();
                };
                timer.Start();
            }
        }

        private void OnPageSizeChanged(object sender, EventArgs e)
        {
            if (Width < 700)
            {
                // Show only main pane
                mainGrid.ColumnDefinitions[0].Width = GridLength.Star;
                //mainGrid.ColumnDefinitions[1].Width = new GridLength(0);
                mainGrid.ColumnDefinitions[1].Width = new GridLength(0);
                rightContent.IsVisible = false;
                removeDetailContent();
            }
            else
            {
                // Show both panes
                mainGrid.ColumnDefinitions[0].Width = new GridLength(400);
                //mainGrid.ColumnDefinitions[1].Width = new GridLength(2);
                mainGrid.ColumnDefinitions[1].Width = GridLength.Star;
                rightContent.IsVisible = true;
            }
        }

        public void stop()
        {
            running = false;
        }

        private void onNavigating(object sender, WebNavigatingEventArgs e)
        {
            string current_url = HttpUtility.UrlDecode(e.Url);

            if(onNavigatingGlobal(current_url))
            {
                e.Cancel = true;
                return;
            }

            if (current_url.Equals("ixian:onload", StringComparison.Ordinal))
            {
                onLoaded();
            }
            else if (current_url.Equals("ixian:wallet", StringComparison.Ordinal))
            {
                // Deprecated
            }
            else if (current_url.Equals("ixian:quickscan", StringComparison.Ordinal))
            {
                quickScan(); 
                e.Cancel = true;
                return;
            }
            else if (current_url.Contains("ixian:qrresult:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:qrresult:" }, StringSplitOptions.None);
                string result = split[1];
                processQRResult(result);
                e.Cancel = true;
                return;
            }
            else if (current_url.Contains("ixian:filter:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:filter:" }, StringSplitOptions.None);
                string result = split[1];
                filterTransactions(result);
                e.Cancel = true;
                return;
            }
            else if (current_url.Contains("ixian:balance:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:balance:" }, StringSplitOptions.None);
                if (split.Length > 1)
                {
                    hideBalance = split[1] == "hide";
                }
                Preferences.Default.Set("hidebalance", hideBalance);
                e.Cancel = true;
                return;
            }
            else if (current_url.Equals("ixian:newchat", StringComparison.Ordinal))
            {
                newChat();
            }
            else if (current_url.Equals("ixian:newcontact", StringComparison.Ordinal))
            {
                Navigation.PushAsync(new ContactNewPage(), Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:newapp", StringComparison.Ordinal))
            {
                Navigation.PushAsync(new AppNewPage(), Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:sendixi", StringComparison.Ordinal))
            {
                onSendIxi(null);
            }
            else if (current_url.Equals("ixian:receiveixi", StringComparison.Ordinal))
            {
                onReceiveIxi(null);
            }
            else if (current_url.Equals("ixian:avatar", StringComparison.Ordinal))
            {
                //onChangeAvatarAsync(sender, e);
            }
            else if (current_url.Equals("ixian:settings", StringComparison.Ordinal))
            {
                onSettings(sender, e);
            }
            else if (current_url.Equals("ixian:lock", StringComparison.Ordinal))
            {
                //   prepBackground();
                Navigation.PushAsync(new SetLockPage(), Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:activity", StringComparison.Ordinal))
            {
                // TODO show wallet activity screen
            }
            else if (current_url.Equals("ixian:about", StringComparison.Ordinal))
            {
                Browser.Default.OpenAsync(new Uri(Config.aboutUrl));
            }
            else if (current_url.Equals("ixian:guide", StringComparison.Ordinal))
            {
                Browser.Default.OpenAsync(new Uri(Config.guideUrl));
            }
            else if (current_url.Equals("ixian:backup", StringComparison.Ordinal))
            {
                Navigation.PushAsync(new BackupPage(), Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:encpass", StringComparison.Ordinal))
            {
                Navigation.PushAsync(new EncryptionPassword(), Config.defaultXamarinAnimations);
            }
            else if (current_url.Contains("ixian:chat:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:chat:" }, StringSplitOptions.None);
                string id = split[1];
                onChat(id, e);
            }
            else if (current_url.Contains("ixian:details:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:details:" }, StringSplitOptions.None);
                string id = split[1];
                // TODO: handle exceptions

                Friend friend = FriendList.getFriend(new Address(id));

                if (friend == null)
                {
                    e.Cancel = true;
                    return;
                }

                Navigation.PushAsync(new ContactDetails(friend), Config.defaultXamarinAnimations);
            }
            else if (current_url.Contains("ixian:txdetails:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:txdetails:" }, StringSplitOptions.None);
                string id = split[1];
                byte[] b_txid = Transaction.txIdLegacyToV8(id);

                onTransaction(b_txid, e);
            }
            else if (current_url.Contains("ixian:tab:"))
            {
                currentTab = current_url.Split(new string[] { "ixian:tab:" }, StringSplitOptions.None)[1];
            }
            else if (current_url.Equals("ixian:downloads", StringComparison.Ordinal))
            {
                Navigation.PushModalAsync(new DownloadsPage());
            }
            else if (current_url.Equals("ixian:contributors", StringComparison.Ordinal))
            {
                Navigation.PushModalAsync(new ContributorsPage());
            }
            else if (current_url.Equals("ixian:share", StringComparison.Ordinal))
            {
                Share.RequestAsync(new ShareTextRequest
                {
                    Text = IxianHandler.getWalletStorage().getPrimaryAddress().ToString(),
                });
            }
            else if (current_url.Contains("ixian:rating:"))
            {
                string result = current_url.Split(new string[] { "ixian:rating:" }, StringSplitOptions.None)[1];
                string? action_url = null;

                if (result.Equals("yes", StringComparison.Ordinal))
                {
                    if (DeviceInfo.Platform == DevicePlatform.Android)
                    {
                        action_url = Config.ratingAndroidUrl;
                    }
                    else if (DeviceInfo.Platform == DevicePlatform.iOS)
                    {
                        action_url = Config.ratingiOSUrl;
                    }
                }
                else if (result.Equals("no", StringComparison.Ordinal))
                {
                    action_url = Config.supportEmailUrl;
                }

                if (action_url != null)
                {
                    Preferences.Default.Set("rating_action", "done");
                    Browser.Default.OpenAsync(new Uri(action_url));
                }

                e.Cancel = true;
                return;
            }
            else if (current_url.Equals("ixian:copy", StringComparison.Ordinal))
            {
            }
            else if(current_url.StartsWith("ixian:viewLog"))
            {
                // TODO perhaps move this whole functionality to Logging class and delete spixi.log.zip on start if exists

                if (File.Exists(Path.Combine(Config.spixiUserFolder, "spixi.log.zip")))
                {
                    File.Delete(Path.Combine(Config.spixiUserFolder, "spixi.log.zip"));
                }

                if (File.Exists(Path.Combine(Config.spixiUserFolder, "ixian.log.tmp")))
                {
                    File.Delete(Path.Combine(Config.spixiUserFolder, "ixian.log.tmp"));
                }

                File.Copy(Path.Combine(Config.spixiUserFolder, "ixian.log"), Path.Combine(Config.spixiUserFolder, "ixian.log.tmp"));

                using (ZipArchive archive = ZipFile.Open(Path.Combine(Config.spixiUserFolder, "spixi.log.zip"), ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(Path.Combine(Config.spixiUserFolder, "ixian.log.tmp"), "ixian.log");
                    if(File.Exists(Path.Combine(Config.spixiUserFolder, "ixian.0.log")))
                    {
                        archive.CreateEntryFromFile(Path.Combine(Config.spixiUserFolder, "ixian.0.log"), "ixian.0.log");
                    }
                }

                if (File.Exists(Path.Combine(Config.spixiUserFolder, "ixian.log.tmp")))
                {
                    File.Delete(Path.Combine(Config.spixiUserFolder, "ixian.log.tmp"));
                }

                SFileOperations.share(Path.Combine(Config.spixiUserFolder, "spixi.log.zip"), "Share Spixi Log File");
            }
            else if(current_url.StartsWith("ixian:onboardingComplete"))
            {
                completeOnboard();
            }
            else if(current_url.StartsWith("ixian:joinBot"))
            {
                joinBot();
            }
            else if (current_url.StartsWith("ixian:startApp:", StringComparison.Ordinal))
            {
                string appId = current_url.Substring("ixian:startApp:".Length);
                onStartApp(appId);
            }
            else if (current_url.StartsWith("ixian:startAppMulti", StringComparison.Ordinal))
            {
                string appId = current_url.Substring("ixian:startAppMulti:".Length);
                onStartAppMulti(appId);
            }
            else if (current_url.StartsWith("ixian:appDetails"))
            {
                string appId = current_url.Substring("ixian:appDetails:".Length);
                onAppDetails(appId);
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;
        }

        public void onSendIxi(Address? wallet)
        {
            if (wallet == null)
            {
                Navigation.PushAsync(new WalletSendPage(), Config.defaultXamarinAnimations);
                return;
            }
            Navigation.PushAsync(new WalletSendPage(wallet), Config.defaultXamarinAnimations);
        }

        public void onReceiveIxi(Friend? friend)
        {
            if (friend == null)
            {
                Navigation.PushAsync(new WalletReceivePage(), Config.defaultXamarinAnimations);
                return;
            }

            Navigation.PushAsync(new WalletReceivePage(friend), Config.defaultXamarinAnimations);
        }

        public void onContactDetails(Friend friend)
        {
            Navigation.PushAsync(new ContactDetails(friend, true), Config.defaultXamarinAnimations);
        }

        public void onConfirmPaymentRequest(FriendMessage msg, Friend friend, string amount, string date_text)
        {
            Navigation.PushAsync(new WalletContactRequestPage(msg, friend, amount, date_text), Config.defaultXamarinAnimations);
        }

        public async void quickScan()
        {
            var scanPage = new ScanPage();
            scanPage.scanSucceeded += HandleScanSucceeded;
            await Navigation.PushAsync(scanPage, Config.defaultXamarinAnimations);
        }
        private void HandleScanSucceeded(object sender, SPIXI.EventArgs<string> e)
        {
            processQRResult(e.Value);       
        }

        public void processQRResult(string result)
        {
            Navigation.PopAsync(Config.defaultXamarinAnimations);

            // Check for add contact
            string[] split = result.Split(new string[] { ":send" }, StringSplitOptions.None);
            if (split.Count() > 1)
            {

                try
                {
                    Address wallet_to_send = new Address(split[0]);
                    Navigation.PushAsync(new WalletSendPage(wallet_to_send), Config.defaultXamarinAnimations);
                }catch(Exception)
                {

                }
                return;
            }

            string id_to_add = split[0];
            Navigation.PushAsync(new ContactNewPage(id_to_add), Config.defaultXamarinAnimations);
            return;
        }

        // Show the recipientpage
        public void newChat()
        {
            var recipientPage = new WalletRecipientPage();
            recipientPage.pickSucceeded += HandlePickSucceeded;
            Navigation.PushAsync(recipientPage, Config.defaultXamarinAnimations);
        }

        private async void HandlePickSucceeded(object sender, SPIXI.EventArgs<string> e)
        {
            string id = e.Value;
            Address id_bytes = new Address(id);
            Friend friend = FriendList.getFriend(id_bytes);

            if (friend == null)
            {
                return;
            }

            try
            {
                await Navigation.PopAsync(Config.defaultXamarinAnimations);
                onChat(id, null);
            }
            catch (Exception ex)
            {
                Logging.error("Navigation failed: " + ex.Message);
            }
        }

        private void joinBot()
        {
            Friend friend = FriendList.addFriend(FriendState.RequestSent, new Address("419jmKRKVFcsjmwpDF1XSZ7j1fez6KWaekpiawHvrpyZ8TPVmH1v6bhT2wFc1uddV"), null, "Spixi Group Chat", null, null, 0);
            if (friend != null)
            {
                friend.save();

                StreamProcessor.sendContactRequest(friend);
            }
        }

        private void completeOnboard()
        {
            Preferences.Default.Set("onboardingComplete", true);

            SpixiLocalization.addCustomString("OnboardingComplete", "true");
            generatePage("index.html");
        }

        // Workaround for Android - sometimes the order of the screens isn't correct
        private void setAsRoot()
        {
            try
            {
                foreach (var page in Navigation.NavigationStack.ToList())
                {
                    if (page == this)
                    {
                        continue;
                    }
                    Navigation.RemovePage(page);
                }
            }
            catch(Exception e)
            {
                Logging.error("Exception occured while setting HomePage as root: {0}", e);
            }
        }
        private void handleOnboardDone(object sender, SPIXI.EventArgs<bool> e)
        {
            // Join official groupchat if specified
            if (e.Value)
                joinBot();
            
            completeOnboard();
            Navigation.PopModalAsync();
        }
        private void onLoaded()
        {
            if (!Preferences.Default.ContainsKey("onboardingComplete"))
            {
                // Show onboarding screen
                var onboardPage = new OnboardPage();
                onboardPage.onboardDone += handleOnboardDone;
                Navigation.PushModalAsync(onboardPage);
            }

            setAsRoot();

            Node.shouldRefreshContacts = true;
            Node.refreshAppRequests = true;
            Node.shouldRefreshApps = true;
            lastTransactionChange = 0;

            Utils.sendUiCommand(this, "selectTab", currentTab);

            Utils.sendUiCommand(this, "loadAvatar", Node.localStorage.getOwnAvatarPath());

            Utils.sendUiCommand(this, "setVersion", Config.version + " BETA (" + Node.startCounter + ")");

            string address_string = IxianHandler.getWalletStorage().getPrimaryAddress().ToString();
            Utils.sendUiCommand(this, "setAddress", address_string);

            Utils.sendUiCommand(this, "setHideBalance", hideBalance.ToString());

            try
            {
                updateScreen();
            }catch(Exception ex)
            {
                Logging.error("Exception occured in updateScreen call from onLoaded: {0}", ex);
            }

            if (App.startingScreen != "")
            {
                onChat(App.startingScreen, null);
                App.startingScreen = "";
            }

            webView.FadeTo(1, 250);
            checkForRating();
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
        }


        public void onSend(object sender, EventArgs e)
        {
            Navigation.PushAsync(new WalletSendPage(), Config.defaultXamarinAnimations);
        }

        public void onReceive(object sender, EventArgs e)
        {
            Navigation.PushAsync(new WalletReceivePage(), Config.defaultXamarinAnimations);
        }

        public void onSettings(object sender, EventArgs e)
        {
            fromSettings = true;
            Navigation.PushAsync(new SettingsPage());
        }

        public async void onTransaction(byte[] txid, WebNavigatingEventArgs e)
        {

            Transaction transaction = TransactionCache.getTransaction(txid);
            if (transaction == null)
            {
                transaction = TransactionCache.getUnconfirmedTransaction(txid);

                if (transaction == null)
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (rightContent.IsVisible)
            {
                await rightContent.Content.FadeTo(0, 50);
                removeDetailContent();
                detailContent = new WalletSentPage(transaction, true, this);
                rightContent.Content.BackgroundColor = ThemeManager.getBackgroundColor();

                rightContent.Content.Opacity = 0;
                rightContent.Content = detailContent.Content;
                await rightContent.Content.FadeTo(1, 150);

                Utils.sendUiCommand(this, "selectTx", transaction.getTxIdString());
                return;
            }


            Navigation.PushAsync(new WalletSentPage(transaction), Config.defaultXamarinAnimations);
        }

        public async void onChat(string friend_address, WebNavigatingEventArgs e)
        {
            Address id_bytes = new Address(friend_address);

            Friend friend = FriendList.getFriend(id_bytes);

            if (friend == null)
            {
                if (e != null)
                {
                    e.Cancel = true;
                }
                return;
            }


            if (rightContent.IsVisible)
            {        
                
                await rightContent.Content.FadeTo(0, 50);
                removeDetailContent();
                detailContent = new SingleChatPage(friend, this);
                rightContent.Content.BackgroundColor = ThemeManager.getBackgroundColor();

                rightContent.Content.Opacity = 0;
                rightContent.Content = detailContent.Content;               
                await rightContent.Content.FadeTo(1, 150);

                Utils.sendUiCommand(this, "selectChat", friend.walletAddress.ToString());
                return;
            }


            clearChatPages();

            fromChat = true;
            bool animated = e != null && Config.defaultXamarinAnimations;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Navigation.NavigationStack.Count > 1)
                {
                    await Navigation.PopToRootAsync(false);
                }

                // TODO
                /*if (IXICore.Platform.onWindows())
                {
                    Window secondWindow = new Window(new SingleChatPage(friend));
                    secondWindow.Title = friend.nickname;
                    Application.Current.OpenWindow(secondWindow);
                }
                else*/
                {
                    await Navigation.PushAsync(new SingleChatPage(friend), animated);
                }
            });
        }

        // Load the contact list
        // TODO: optimize this
        public void loadContacts()
        {
            if (!Node.shouldRefreshContacts)
            {
                //  No changes detected, stop here
                return;
            }

            // Clear everything
            Utils.sendUiCommand(this, "clearContacts");

            // Add contacts one-by-one
            foreach(Friend friend in FriendList.friends)
            {
                string str_online = "false";
                if (friend.online)
                    str_online = "true";

                string avatar = Node.localStorage.getAvatarPath(friend.walletAddress.ToString());
                if (avatar == null)
                {
                    avatar = "img/spixiavatar.png";
                }

                Utils.sendUiCommand(this, "addContact", friend.walletAddress.ToString(), friend.nickname, avatar, str_online, friend.getUnreadMessageCount().ToString());
            }
        }

        public void loadChats()
        {
            List<Friend> friends;
            lock(FriendList.friends)
            {
                friends = new List<Friend>(FriendList.friends);
            }
            // Check if there are any changes from last time first
            int unread = 0;
            foreach (Friend friend in friends)
            {
                int umc = friend.getUnreadMessageCount();
                if(umc > 0)
                {
                    unread += umc;
                }
            }

            if(unread > 0)
            {
                Utils.sendUiCommand(this, "setUnreadIndicator", unread.ToString());
            }else
            {
                Utils.sendUiCommand(this, "setUnreadIndicator", "0");
            }

            if (!Node.shouldRefreshContacts)
            {
                //  No changes detected, stop here
                return;
            }

            Utils.sendUiCommand(this, "clearChats");
            //Utils.sendUiCommand(webView, "clearUnreadActivity");

            // Prepare a list of message helpers, to facilitate sorting and communication with the UI
            List<FriendMessageHelper> helper_msgs = new List<FriendMessageHelper>();

            foreach (Friend friend in friends)
            {
                if(friend.pendingDeletion)
                {
                    continue;
                }

                FriendMessage? lastmsg = null;// TODO friend.metaData.lastMessage;
                if(friend.getMessages(0).Count > 0)
                {
                    lastmsg = friend.getMessages(0).Last();
                }
                
                if (lastmsg != null)
                {
                    string str_online = "false";
                    if (friend.online)
                        str_online = "true";

                    // Generate the excerpt depending on message type
                    string excerpt = lastmsg.message;

                    if (friend.state != FriendState.Approved)
                    {
                        if (friend.bot == false)
                        {
                            excerpt = SpixiLocalization._SL("chat-waiting-for-response");
                        }
                    }
                    else
                    {
                        if (lastmsg.type == FriendMessageType.requestFunds)
                        {
                            if (lastmsg.localSender)
                            {
                                excerpt = SpixiLocalization._SL("index-excerpt-payment-request-sent");
                            }
                            else
                            {
                                excerpt = SpixiLocalization._SL("index-excerpt-payment-request-received");
                            }
                        }
                        else if (lastmsg.type == FriendMessageType.sentFunds)
                        {
                            if (lastmsg.localSender)
                            {
                                excerpt = SpixiLocalization._SL("index-excerpt-payment-sent");
                            }
                            else
                            {
                                excerpt = SpixiLocalization._SL("index-excerpt-payment-received");
                            }
                        }
                        else if (lastmsg.type == FriendMessageType.appSession)
                        {
                            if (lastmsg.localSender)
                            {
                                excerpt = SpixiLocalization._SL("chat-app-invite-sent");
                            }
                            else
                            {
                                excerpt = SpixiLocalization._SL("chat-app-invite-received");
                            }
                        }
                        else if (lastmsg.type == FriendMessageType.requestAdd)
                        {
                            if (friend.approved)
                            {
                                excerpt = SpixiLocalization._SL("index-excerpt-contact-accepted");
                            }
                            else
                            {
                                excerpt = SpixiLocalization._SL("index-excerpt-contact-request");
                            }
                        }
                        else if (lastmsg.type == FriendMessageType.fileHeader)
                        {
                            excerpt = SpixiLocalization._SL("index-excerpt-file");
                        }
                        else if (lastmsg.type == FriendMessageType.voiceCall || lastmsg.type == FriendMessageType.voiceCallEnd)
                        {
                            excerpt = SpixiLocalization._SL("index-excerpt-voice-call");
                        }

                        if (lastmsg.localSender)
                        {
                            excerpt = SpixiLocalization._SL("index-excerpt-self") + " " + excerpt;
                        }
                    }

                    string avatar = Node.localStorage.getAvatarPath(friend.walletAddress.ToString());
                    if(avatar == null)
                    {
                        avatar = "img/spixiavatar.png";
                    }

                    string type = "";

                    if (friend.isTyping)
                    {
                        excerpt = SpixiLocalization._SL("index-excerpt-typing");
                        type = "typing";
                    }
                    else if (lastmsg.localSender)
                    {
                        if (lastmsg.read)
                        {
                            type = "read";
                        }
                        else if (lastmsg.confirmed)
                        {
                            type = "confirmed";
                        }
                        else if (lastmsg.sent)
                        {
                            type = "pending";
                        }
                        else
                        {
                            type = "default";
                        }
                    }

                    FriendMessageHelper helper_msg = new(friend.walletAddress.ToString(), friend.nickname, lastmsg.timestamp, avatar, str_online, excerpt, type, friend.getUnreadMessageCount());
                    helper_msgs.Add(helper_msg);
                }
            }

            // Sort the helper messages
            List<FriendMessageHelper> sorted_msgs = helper_msgs.OrderByDescending(x => x.timestamp).ToList();

            // Add the messages visually
            foreach(FriendMessageHelper helper_msg in sorted_msgs)
            {
                if(helper_msg.unreadCount > 0)
                {
                    Utils.sendUiCommand(this, "addUnreadActivity", helper_msg.walletAddress, helper_msg.nickname, helper_msg.timestamp.ToString(), helper_msg.avatar, helper_msg.onlineString, helper_msg.excerpt);

                }
                Utils.sendUiCommand(this, "addChat", helper_msg.walletAddress, helper_msg.nickname, helper_msg.timestamp.ToString(), helper_msg.avatar, helper_msg.onlineString, helper_msg.excerpt, helper_msg.type, helper_msg.unreadCount.ToString());
            }

            // Clear the lists so they will be collected by the GC
            helper_msgs = null;
            sorted_msgs = null;

            Utils.sendUiCommand(this, "clearChatsDone");
        }

        public static IxiNumber calculateReceivedAmount(Transaction tx)
        {
            IxiNumber amount = 0;
            foreach (var entry in tx.toList)
            {
                if (IxianHandler.getWalletStorage().isMyAddress(entry.Key))
                {
                    amount += entry.Value.amount;
                }
            }
            return amount;
        }

        public void filterTransactions(string filter)
        {
            switch(filter)
            {

                case "sent":
                    {
                        transactionFilter = 1;
                        break;
                    }
                case "received":
                    {
                        transactionFilter = 2;
                        break;
                    }
                default:
                case "all":
                    {
                        transactionFilter = 0;
                        break;
                    }

            }
            lastTransactionChange++;
            loadTransactions();
        }
        public string filterToString(int filter)
        {
            switch(filter)
            {
                case 1:
                    return "sent";
                case 2:
                    return "received";
                case 0:
                default:
                    return "all";
            }
        }

        public void loadTransactions()
        {
            // Check if there are any changes
            if(lastTransactionChange == TransactionCache.lastChange)
            {
                return;
            }
            lastTransactionChange = TransactionCache.lastChange;

            Utils.sendUiCommand(this, "clearPaymentActivity", filterToString(transactionFilter));

            void addPaymentActivity(Transaction tx)
            {
                string received = "1";
                string tx_text = tx.pubKey.ToString();
                IxiNumber amount = tx.amount;
                if (IxianHandler.getWalletStorage().isMyAddress(tx.pubKey))
                {
                    tx_text = tx.toList.First().Key.ToString();
                    Friend friend = FriendList.getFriend(tx.toList.First().Key);
                    if (friend != null)
                    {
                        tx_text = friend.nickname;
                    }

                    received = "0";
                    if (transactionFilter == 2)
                        return;
                }
                else
                {
                    Friend friend = FriendList.getFriend(tx.pubKey);
                    if (friend != null)
                    {
                        tx_text = friend.nickname;
                    }
                    amount = calculateReceivedAmount(tx);
                    if (transactionFilter == 1)
                        return;
                }
                string amount_string = Utils.amountToHumanFormatString(amount);
                string fiat_amount_string = Utils.amountToHumanFormatString(amount * Node.fiatPrice);

                string confirmed = "false";
                if(Node.networkBlockHeight > tx.blockHeight + Config.txConfirmationBlocks)
                {
                    tx.applied = tx.blockHeight + Config.txConfirmationBlocks;
                    confirmed = "true";
                }


                string time = Utils.unixTimeStampToHumanFormatString(Convert.ToDouble(tx.timeStamp));
                Utils.sendUiCommand(this, "addPaymentActivity", tx.getTxIdString(), received, tx_text, time, amount_string, fiat_amount_string, confirmed);
            }

            lock (TransactionCache.unconfirmedTransactions)
            {
                for (int i = TransactionCache.unconfirmedTransactions.Count - 1; i >= 0; i--)
                {
                    addPaymentActivity(TransactionCache.unconfirmedTransactions[i].transaction);
                }
            }

            int max_tx_count = 0;
            if(TransactionCache.transactions.Count > 50)
            {
                max_tx_count = TransactionCache.transactions.Count - 50;
            }

            for (int i = TransactionCache.transactions.Count - 1; i >= max_tx_count; i--)
            {
                addPaymentActivity(TransactionCache.transactions[i].transaction);                
            }
        }

        private void displayBackupReminder()
        {
            if (!Preferences.Default.ContainsKey("backupReminderTimestamp")
                || Clock.getTimestamp() - long.Parse(Preferences.Default.Get("backupReminderTimestamp", "").ToString()) > Config.backupReminder)
            {
                Utils.sendUiCommand(this, "toggleAnimatedSlider", "backup-prompt");
                Preferences.Default.Set("backupReminderTimestamp", Clock.getTimestamp().ToString());
            }
        }

        // Executed every second
        public override void updateScreen()
        {
            base.updateScreen();

            displayBackupReminder();

            loadApps();
            loadChats();
            loadContacts();
            Node.shouldRefreshContacts = false;

            updateContactStatus();
            loadTransactions();

            try
            {
                string new_version = checkForUpdate();
                if (!new_version.StartsWith("(") && Version.Parse(new_version.Substring(new_version.IndexOf('-') + 1)).CompareTo(Version.Parse(Config.version.Substring(Config.version.IndexOf('-') + 1))) > 0)
                {
                    Utils.sendUiCommand(this, "showWarning", String.Format(SpixiLocalization._SL("global-update-available"), new_version));
                }
                else
                {
                    // Check the ixian dlt
                    if (NetworkClientManager.getConnectedClients(true).Count() > 0)
                    {
                        Utils.sendUiCommand(this, "showWarning", "");
                    }
                    else
                    {
                        Utils.sendUiCommand(this, "showWarning", SpixiLocalization._SL("global-connecting-dlt"));
                    }

                }
            }catch (Exception e)
            {
                Logging.error("Exception occurred in HomePage.UpdateScreen: " + e);
            }
            IxiNumber availableBalance = Node.getAvailableBalance();
            string balance = Utils.amountToHumanFormatString(availableBalance);
            string fiatBalance = Utils.amountToHumanFormatString(Node.fiatPrice * availableBalance);
            Utils.sendUiCommand(this, "setBalance", balance, fiatBalance, Node.localStorage.nickname);

            // Check if we should reload certain elements
            if(Node.changedSettings == true)
            {
                Utils.sendUiCommand(this, "loadAvatar", Node.localStorage.getOwnAvatarPath());
                Node.changedSettings = false;
            }

            SPushService.clearNotifications();
        }

        private void onUpdateUI(/*object source, ElapsedEventArgs e*/)
        {
            try
            {
                if (!webView.IsEnabled || !App.isInForeground)
                {
                    return;
                }
                Page page = Navigation.NavigationStack.Last();
                if (page is not null and SpixiContentPage)
                {
                    ((SpixiContentPage)page).updateScreen();
                }
            }
            catch(Exception ex)
            {
                Logging.error("Exception occured in onUpdateUI: {0}", ex);
            }
        }

        protected override void OnAppearing()
        {
            if(fromSettings)
            {
                fromSettings = false;
                loadPage(webView, "index.html");
            }
            else if(fromChat)
            {
                fromChat = false;
                checkForRating();
            }
            base.OnAppearing();
        }

        private void clearChatPages()
        {
            lock (FriendList.friends)
            {
                var tmp_list = FriendList.friends.FindAll(x => x.chat_page != null);
                foreach (var friend in tmp_list)
                {
                    if (friend.chat_page != null)
                    {
                        friend.chat_page = null;
                    }
                }
            }
        }

        private string checkForUpdate()
        {
            if (!UpdateVerify.ready && !UpdateVerify.error) return "(checking)";
            if (UpdateVerify.ready)
            {
                if (UpdateVerify.error) return "(error)";
                return UpdateVerify.serverVersion;
            }
            return "(not checked)";
        }

        private void checkForRating()
        {
            if (DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS)
            {
                if (Preferences.Default.ContainsKey("rating_action"))
                {
                    string resp = Preferences.Default.Get("rating_action", "show");
                    if (resp.Equals("show", StringComparison.Ordinal))
                    {
                        Utils.sendUiCommand(this, "showRatingPrompt");
                    }
                }
            }
        }

        // Adds and filters a new contact status to the cache
        // Can be called from any thread
        public void setContactStatus(Address address, bool online, int unread, string excerpt, long timestamp)
        {
            // Cache and filter contact status changes to reduce cpu usage with many notifications
            lock (contactStatusCache)
            {
                bool _alreadyInCache = false;
                int i = 0;
                for(i = 0; i < contactStatusCache.Count; i++)
                {
                    contactStatusCacheItem cacheItem = contactStatusCache[i];
                    if (cacheItem.address.SequenceEqual(address))
                    {
                        // Update the cached status to the latest message
                        if(timestamp > cacheItem.timestamp)
                        {
                            cacheItem.timestamp = timestamp;
                            cacheItem.unread = unread;
                            cacheItem.online = online;
                            cacheItem.excerpt = excerpt;
                        }
                        // Already in cache, break to minimize processing
                        _alreadyInCache = true;
                        break;
                    }
                }

                // If not found in cache, add this message
                if(!_alreadyInCache)
                {
                    contactStatusCacheItem cacheItem = new contactStatusCacheItem();
                    cacheItem.address = address;
                    cacheItem.online = online;
                    cacheItem.unread = unread;
                    cacheItem.excerpt = excerpt;
                    cacheItem.timestamp = timestamp;
                    contactStatusCache.Add(cacheItem);
                }
            }

        }
        // Updates the status for all entries in the contact status cache
        // Called from a UI thread
        public void updateContactStatus()
        {
            lock (contactStatusCache)
            {
                // Go through each cache item and perform the status update
                foreach (contactStatusCacheItem cacheItem in contactStatusCache)
                {
                    Utils.sendUiCommand(this, "setContactStatus", cacheItem.address.ToString(), 
                        cacheItem.online.ToString(), cacheItem.unread.ToString(), cacheItem.excerpt, cacheItem.timestamp.ToString());
                }
                // Clear the contact status cache
                contactStatusCache.Clear();
            }
        }

        public override void reload()
        {
            base.reload();
            removeDetailContent();
        }

        public void removeDetailContent()
        {
            detailContent = null;
            defaultDetailContent = new EmptyDetail();
            rightContent.Content = defaultDetailContent.Content;

            Utils.sendUiCommand(this, "selectChat", "");
            clearChatPages();

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // Spixi Mini Apps logic

        private void loadApps()
        {
            if(!Node.shouldRefreshApps)
            {
                return;
            }
            Node.shouldRefreshApps = false;

            Utils.sendUiCommand(this, "clearApps");

            var apps = Node.MiniAppManager.getInstalledApps();
            lock (apps)
            {
                foreach (var app_arr in apps)
                {
                    MiniApp app = app_arr.Value;
                    string icon = Node.MiniAppManager.getAppIconPath(app.id);
                    if (icon == null)
                    {
                        icon = "";
                    }
                    Utils.sendUiCommand(this, "addApp", app.id, app.name, icon, app.publisher, app.hasCapability(MiniAppCapabilities.SingleUser).ToString(), app.hasCapability(MiniAppCapabilities.MultiUser).ToString());
                }
            }
        }

        private void onStartApp(string appId)
        {
            MiniAppPage miniAppPage = new MiniAppPage(appId, IxianHandler.getWalletStorage().getPrimaryAddress(), null, Node.MiniAppManager.getAppEntryPoint(appId));
            miniAppPage.accepted = true;
            Node.MiniAppManager.addAppPage(miniAppPage);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Navigation.PushAsync(miniAppPage, Config.defaultXamarinAnimations);
            });
        }

        private void onStartAppMulti(string appId)
        {
            var recipientPage = new WalletRecipientPage();
            recipientPage.pickSucceeded += (sender, e) =>
            {
                HandlePickAppMultiUserSucceeded(sender, e, appId);
            };

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Navigation.PushAsync(recipientPage, Config.defaultXamarinAnimations);
            });
        }

        private async void HandlePickAppMultiUserSucceeded(object sender, SPIXI.EventArgs<string> e, string appId)
        {
            string id = e.Value;
            Address id_bytes = new Address(id);
            Friend friend = FriendList.getFriend(id_bytes);

            if (friend == null)
            {
                return;
            }

            try
            {
                await Navigation.PopAsync(Config.defaultXamarinAnimations);

                byte[] session_id = onJoinApp(appId, new Address[] { id_bytes });
                
                var msg = StreamProcessor.sendAppRequest(friend, appId, session_id, null);
                var app_info = Node.MiniAppManager.getAppInfo(appId);
                FriendList.addMessageWithType(msg.id, FriendMessageType.appSession, friend.walletAddress, 0, app_info, true, null, 0, false);
            }
            catch (Exception ex)
            {
                Logging.error("Navigation failed: " + ex.Message);
            }
        }

        public byte[] onJoinApp(string appId, Address[] userAddresses)
        {
            MiniAppPage miniAppPage = new MiniAppPage(appId, IxianHandler.getWalletStorage().getPrimaryAddress(), userAddresses, Node.MiniAppManager.getAppEntryPoint(appId));
            miniAppPage.accepted = true;
            Node.MiniAppManager.addAppPage(miniAppPage);

            MainThread.BeginInvokeOnMainThread(async() =>
            {
                await Task.Delay(200); // WinUI Crash fix
                await Navigation.PushAsync(miniAppPage, Config.defaultXamarinAnimations);
            });

            return miniAppPage.sessionId;
        }

        public async void onInstallApp(string appUrl)
        {
            MiniApp? app = await Node.MiniAppManager.fetch(appUrl);
            if (app == null)
            {
                return;
            }

            app.url = appUrl;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Navigation.PushAsync(new AppDetailsPage(app), Config.defaultXamarinAnimations);
            });
        }
        private void onAppDetails(string appId)
        {
            Navigation.PushAsync(new AppDetailsPage(appId), Config.defaultXamarinAnimations);
        }

    }
}