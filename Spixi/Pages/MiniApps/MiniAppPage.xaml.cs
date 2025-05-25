using IXICore;
using IXICore.Meta;
using Newtonsoft.Json;
using SPIXI.MiniApps;
using SPIXI.MiniApps.ActionRequestModels;
using SPIXI.Lang;
using SPIXI.Meta;
using System.Text;
using System.Web;


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
                sendNetworkData(current_url.Substring(10));
            }
            else if (current_url.StartsWith("ixian:getStorageData", StringComparison.Ordinal))
            {
                var key = current_url.Substring("ixian:getStorageData".Length);
                var data = Node.MiniAppStorage.getStorageData(appId, key);
                Utils.sendUiCommand(this, "SpixiAppSdk.onStorageData", key, Crypto.hashToString(data));
            }
            else if (current_url.StartsWith("ixian:setStorageData", StringComparison.Ordinal))
            {
                var key = current_url.Substring("ixian:setStorageData".Length, current_url.IndexOf('='));
                var value = current_url.Substring(current_url.IndexOf('='));
                Node.MiniAppStorage.setStorageData(appId, key, Crypto.stringToHash(value));
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
                if (await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), string.Format(SpixiLocalization._SL("app-action-description"), jsonResult.responseUrl, jsonResult.command), SpixiLocalization._SL("global-dialog-ok"), SpixiLocalization._SL("global-dialog-cancel")))
                {
                    string? actionResponse = MiniAppActionHandler.processAction(jsonResult.command, action);

                    if (actionResponse == null)
                    {
                        await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), SpixiLocalization._SL("app-action-unknown"), SpixiLocalization._SL("global-dialog-ok"));
                        return;
                    }
                    HttpResponseMessage? response = null;
                    using (HttpClient client = new HttpClient())
                    {
                        try
                        {
                            if (await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), string.Format(SpixiLocalization._SL("app-action-response-confirmation"), actionResponse), SpixiLocalization._SL("global-dialog-ok"), SpixiLocalization._SL("global-dialog-cancel")))
                            {
                                HttpContent httpContent = new StringContent(actionResponse, Encoding.UTF8, "application/x-www-form-urlencoded");
                                response = client.PostAsync(jsonResult.responseUrl, httpContent).Result;
                                if (response.IsSuccessStatusCode)
                                {
                                    await displaySpixiAlert(SpixiLocalization._SL("app-action-title"), SpixiLocalization._SL("app-action-sent"), SpixiLocalization._SL("global-dialog-ok"));
                                    return;
                                }
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
            // Execute timer-related functionality immediately
            updateScreen();
        }

        // Executed every second
        public override void updateScreen()
        {

        }

        private void sendNetworkData(string data)
        {
            foreach (Address address in userAddresses)
            {
                Friend f = FriendList.getFriend(address);
                if (f != null)
                {
                    // TODO TODO TODO probably a different encoding should be used for data
                    StreamProcessor.sendAppData(f, sessionId, UTF8Encoding.UTF8.GetBytes(data));
                }
                else
                {
                    Logging.error("Friend {0} does not exist in the friend list.", address.ToString());
                }
            }
        }

        private void onBack()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Navigation.PopAsync(Config.defaultXamarinAnimations);
                Node.MiniAppManager.removeAppPage(sessionId);
            });
        }

        public void networkDataReceived(Address sender_address, byte[] data)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // TODO TODO TODO probably a different encoding should be used for data
                Utils.sendUiCommand(this, "SpixiAppSdk.onNetworkData", sender_address.ToString(), UTF8Encoding.UTF8.GetString(data));
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

        public bool hasUser(Address user)
        {
            if (userAddresses.Select(x => x.addressNoChecksum.SequenceEqual(user.addressNoChecksum)) != null)
            {
                return true;
            }
            return false;
        }

        protected override bool OnBackButtonPressed()
        {
            onBack();

            return true;
        }
    }
}