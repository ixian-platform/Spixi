using IXICore;
using IXICore.Meta;
using Newtonsoft.Json;
using SPIXI.MiniApps;
using SPIXI.MiniApps.ActionRequestModels;
using SPIXI.Lang;
using SPIXI.Meta;
using System.Text;
using System.Web;
using IXICore.Streaming;
using SPIXI.MiniApps.ActionResponseModels;


namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MiniAppPage : SpixiContentPage
    {
        public string appId = null;
        public byte[] sessionId = null; // App session ID

        //public Address myRequestAddress = null; // which address the app request was sent to
        //public Address requestedByAddress = null; // which address sent the app request to us

        public Address hostUserAddress = null; // address of the user that initiated the app
        private Address[] userAddresses = null; // addresses of all users connected to/using the app

        public bool accepted = false;
        public long requestReceivedTimestamp = 0;


        public MiniAppPage(string app_id, Address host_user_address, Address[] user_addresses, string app_entry_point)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            // TODO randomize session id and add support for more users
            sessionId = CryptoManager.lib.sha3_512sqTrunc(UTF8Encoding.UTF8.GetBytes(app_id));

            appId = app_id;

            hostUserAddress = host_user_address;
            userAddresses = user_addresses;

            // Load the app entry point
            var source = new UrlWebViewSource();
            source.Url = "file://" + app_entry_point;
            _webView = webView;
            webView.Source = source;
            webView.Navigated += webViewNavigated;
            webView.Navigating += webViewNavigating;

            requestReceivedTimestamp = Clock.getTimestamp();

            if (user_addresses != null)
            {
                foreach (Address address in user_addresses)
                {
                    Friend friend = FriendList.getFriend(address);
                    if (friend != null)
                    {
                        StreamProcessor.fetchFriendsPresence(friend, true);
                    }
                }
            }
        }

        public override void recalculateLayout()
        {
            ForceLayout();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
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
                onBack();
            }
            else if (current_url.StartsWith("ixian:data", StringComparison.Ordinal))
            {
                // TODO TODO TODO probably a different encoding should be used for data
                sendNetworkData(UTF8Encoding.UTF8.GetBytes(current_url.Substring(10)));
            }
            else if (current_url.StartsWith("ixian:protocolData", StringComparison.Ordinal))
            {
                var prefixLen = "ixian:protocolData".Length;
                var protocolId = current_url.Substring(prefixLen, current_url.IndexOf('=') - prefixLen);
                var data = current_url.Substring(current_url.IndexOf('=') + 1);
                byte[] protocolIdBytes = null;
                if (protocolId != "null")
                {
                    protocolIdBytes = CryptoManager.lib.sha3_512Trunc(UTF8Encoding.UTF8.GetBytes(protocolId));
                }
                // TODO TODO TODO probably a different encoding should be used for data
                sendNetworkProtocolData(protocolIdBytes, UTF8Encoding.UTF8.GetBytes(data));
            }
            else if (current_url.StartsWith("ixian:getStorageData", StringComparison.Ordinal))
            {
                var key = current_url.Substring("ixian:getStorageData".Length);
                var data = Node.MiniAppStorage.getStorageData(appId, key);
                string dataStr = "null";
                if (data != null)
                {
                    dataStr = Convert.ToBase64String(data);
                }
                Utils.sendUiCommand(this, "SpixiAppSdk.onStorageData", key, dataStr);
            }
            else if (current_url.StartsWith("ixian:setStorageData", StringComparison.Ordinal))
            {
                var prefixLen = "ixian:setStorageData".Length;
                var key = current_url.Substring(prefixLen, current_url.IndexOf('=') - prefixLen);
                var value = current_url.Substring(current_url.IndexOf('=') + 1);
                byte[] valueToStore = null;
                if (value != "null")
                {
                    valueToStore = Convert.FromBase64String(value);
                }
                Node.MiniAppStorage.setStorageData(appId, key, valueToStore);
            }
            else if (current_url.StartsWith("ixian:action", StringComparison.Ordinal))
            {
                var action = current_url.Substring("ixian:action".Length);
                handleActionPageResponse(action);
            }
            else
            {
                // Otherwise it's just normal navigation
                // TODO for mini apps (possibly other stuff as well) prevent normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;
        }

        private async void handleActionPageResponse(string action)
        {
            try
            {
                MiniAppActionBase jsonResult = JsonConvert.DeserializeObject<MiniAppActionBase>(action);
                /*if (!await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), string.Format(SpixiLocalization._SL("app-action-description"), jsonResult.responseUrl, jsonResult.command), SpixiLocalization._SL("global-dialog-ok"), SpixiLocalization._SL("global-dialog-cancel")))
                {
                    return;
                }*/
                string? actionResponse = MiniAppActionHandler.processAction(jsonResult.command, action);

                if (actionResponse == null)
                {
                    await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), SpixiLocalization._SL("app-action-unknown"), SpixiLocalization._SL("global-dialog-ok"));
                    return;
                }

                if (jsonResult.command == MiniAppCommands.EXTEND_NAME
                    || jsonResult.command == MiniAppCommands.TRANSFER_NAME
                    || jsonResult.command == MiniAppCommands.REGISTER_NAME
                    || jsonResult.command == MiniAppCommands.UPDATE_CAPACITY
                    || jsonResult.command == MiniAppCommands.UPDATE_NAME
                    || jsonResult.command == MiniAppCommands.ALLOW_SUBNAMES)
                {
                    if (!await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), string.Format(SpixiLocalization._SL("app-action-response-confirmation"), actionResponse), SpixiLocalization._SL("global-dialog-ok"), SpixiLocalization._SL("global-dialog-cancel")))
                    {
                        return;
                    }
                }
                else if (jsonResult.command == MiniAppCommands.SEND_PAYMENT)
                {
                    TransactionResponse txResponse = JsonConvert.DeserializeObject<TransactionResponse>(actionResponse);
                    Transaction tx = new Transaction(Crypto.stringToHash(txResponse.tx));

                    Friend friend = FriendList.getFriend(new Address(tx.toList.Keys.First()));
                    string nick = tx.toList.Keys.First().ToString();
                    if (friend != null)
                    {
                        nick = friend.nickname + " (" + nick + ")";
                    }

                    if (!await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), string.Format(SpixiLocalization._SL("app-action-response-payment-user-confirmation"), nick, tx.toList.Values.First().amount.ToString() + " IXI"), SpixiLocalization._SL("global-dialog-ok"), SpixiLocalization._SL("global-dialog-cancel")))
                    {
                        return;
                    }
                    if (string.IsNullOrEmpty(jsonResult.responseUrl))
                    {
                        IxianHandler.addTransaction(tx, tx.toList.Keys.Skip(1).ToList(), true);

                        // Send message to recipients
                        foreach (var entry in tx.toList)
                        {
                            friend = FriendList.getFriend(entry.Key);

                            if (friend != null)
                            {
                                FriendMessage friend_message = Node.addMessageWithType(null, FriendMessageType.sentFunds, entry.Key, 0, tx.getTxIdString(), true);

                                SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.sentFunds, tx.id);

                                StreamMessage message = new StreamMessage(friend.protocolVersion);
                                message.type = StreamMessageCode.info;
                                message.recipient = friend.walletAddress;
                                message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                                message.data = spixi_message.getBytes();
                                message.id = friend_message.id;

                                CoreStreamProcessor.sendMessage(friend, message);
                            }
                        }

                        await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), SpixiLocalization._SL("app-action-sent"), SpixiLocalization._SL("global-dialog-ok"));
                        return;
                    }

                }
                else
                {
                    if (!await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), string.Format(SpixiLocalization._SL("app-action-response-confirmation"), actionResponse), SpixiLocalization._SL("global-dialog-ok"), SpixiLocalization._SL("global-dialog-cancel")))
                    {
                        return;
                    }
                }

                HttpResponseMessage? response = null;
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        HttpContent httpContent = new StringContent(actionResponse, Encoding.UTF8, "application/x-www-form-urlencoded");
                        response = client.PostAsync(jsonResult.responseUrl, httpContent).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), SpixiLocalization._SL("app-action-sent"), SpixiLocalization._SL("global-dialog-ok"));
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.error("Exception occured in handleActionPageResponse while sending response to service: " + e);
                    }
                }
                if (response != null)
                {
                    await displaySpixiAlert(SpixiLocalization._SL("app-action-error-sending-title"), string.Format(SpixiLocalization._SL("app-action-error-sending"), "(" + response.StatusCode + "): " + (await response.Content.ReadAsStringAsync())), SpixiLocalization._SL("global-dialog-ok"));
                }
                else
                {
                    await displaySpixiAlert(SpixiLocalization._SL("app-action-error-sending-title"), string.Format(SpixiLocalization._SL("app-action-error-sending"), ""), SpixiLocalization._SL("global-dialog-ok"));
                }
            }
            catch (Exception e)
            {
                Logging.error("Exception while processing Mini App action '{0}': {1}", action, e);
                await displaySpixiAlert(SpixiLocalization._SL("app-action-error-processing-title"), string.Format(SpixiLocalization._SL("app-action-error-processing"), action), SpixiLocalization._SL("global-dialog-ok"));
            }
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            if (userAddresses != null)
            {
                // Multi User App
                Utils.sendUiCommand(this, "SpixiAppSdk.onInit", Crypto.hashToString(sessionId), string.Join(',', userAddresses.Select(x => x.ToString())));
            } else
            {
                // Single User App
                Utils.sendUiCommand(this, "SpixiAppSdk.onInit");
            }

            // Execute timer-related functionality immediately
            updateScreen();
        }

        // Executed every second
        public override void updateScreen()
        {

        }

        private void sendNetworkData(byte[] data)
        {
            foreach (Address address in userAddresses)
            {
                Friend f = FriendList.getFriend(address);
                if (f != null)
                {
                    StreamProcessor.sendAppData(f, sessionId, data);
                }
                else
                {
                    Logging.error("Friend {0} does not exist in the friend list.", address.ToString());
                }
            }
        }


        private void sendNetworkProtocolData(byte[] protocolId, byte[] data)
        {
            foreach (Address address in userAddresses)
            {
                Friend f = FriendList.getFriend(address);
                if (f != null)
                {
                    StreamProcessor.sendAppProtocolData(f, protocolId, data);
                }
                else
                {
                    Logging.error("Friend {0} does not exist in the friend list.", address.ToString());
                }
            }
        }

        private void onBack()
        {
            if (userAddresses != null)
            {
                foreach (Address address in userAddresses)
                {
                    Friend f = FriendList.getFriend(address);
                    if (f != null)
                    {
                        // TODO TODO TODO probably a different encoding should be used for data
                        StreamProcessor.sendAppEndSession(f, sessionId, UTF8Encoding.UTF8.GetBytes(IxianHandler.primaryWalletAddress.ToString()));
                    }
                    else
                    {
                        Logging.error("Friend {0} does not exist in the friend list.", address.ToString());
                    }
                }
            }

            Node.MiniAppManager.removeAppPage(sessionId);
            popPageAsync();
        }

        public void networkDataReceived(Address sender_address, byte[] data)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // TODO TODO TODO probably a different encoding should be used for data
                Utils.sendUiCommand(this, "SpixiAppSdk.onNetworkData", sender_address.ToString(), UTF8Encoding.UTF8.GetString(data));
            });
        }

        public void networkProtocolDataReceived(Address sender_address, string protocol_name, byte[] data)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // TODO TODO TODO probably a different encoding should be used for data
                Utils.sendUiCommand(this, "SpixiAppSdk.onNetworkProtocolData", sender_address.ToString(), protocol_name, UTF8Encoding.UTF8.GetString(data));
            });
        }

        public void appRequestAcceptReceived(Address sender_address, byte[] data)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // TODO TODO TODO probably a different encoding should be used for data
                Utils.sendUiCommand(this, "SpixiAppSdk.onRequestAccept", sender_address.ToString(), UTF8Encoding.UTF8.GetString(data));
            });
        }

        public void appRequestRejectReceived(Address sender_address, byte[] data)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // TODO TODO TODO probably a different encoding should be used for data
                Utils.sendUiCommand(this, "SpixiAppSdk.onRequestReject", sender_address.ToString(), UTF8Encoding.UTF8.GetString(data));
            });
        }

        public void appEndSessionReceived(Address sender_address, byte[] data)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // TODO TODO TODO probably a different encoding should be used for data
                Utils.sendUiCommand(this, "SpixiAppSdk.onAppEndSession", sender_address.ToString(), UTF8Encoding.UTF8.GetString(data));
            });
        }

        public void transactionReceived(Address sender_address, IxiNumber amount, string txid, byte[] data, bool verified)
        {
            if (Node.MiniAppManager.getApp(appId).hasCapability(MiniAppCapabilities.TransactionSigning))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Utils.sendUiCommand(this, "SpixiAppSdk.onTransactionReceived", sender_address.ToString(), amount.ToString(), txid, Crypto.hashToString(data), verified.ToString());
                });
            }
        }

        public void paymentSent(Address sender_address, IxiNumber amount, string txid, byte[] data, bool verified)
        {
            if (Node.MiniAppManager.getApp(appId).hasCapability(MiniAppCapabilities.TransactionSigning))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Utils.sendUiCommand(this, "SpixiAppSdk.onPaymentSent", sender_address.ToString(), amount.ToString(), txid, Crypto.hashToString(data), verified.ToString());
                });
            }
        }

        public bool hasUser(Address user)
        {
            return userAddresses.Any(x => x.addressNoChecksum.SequenceEqual(user.addressNoChecksum));
        }

        protected override bool OnBackButtonPressed()
        {
            onBack();

            return true;
        }
    }
}