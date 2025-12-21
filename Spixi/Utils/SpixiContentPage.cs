using IXICore;
using IXICore.Meta;
using SPIXI.MiniApps;
using SPIXI.Lang;
using SPIXI.Meta;
using SPIXI.VoIP;
using Spixi;
using IXICore.Streaming;

#if WINDOWS
using Microsoft.Web.WebView2.Core;
#endif

namespace SPIXI
{
    public class SpixiContentPage : ContentPage, IDisposable
    {
        public bool CancelsTouchesInView = true;
        public bool pageLoaded = false;
        private Queue<string> messageQueue = new Queue<string>();
        protected WebView? _webView = null;
        public WebView WebView
        {
            get
            {
                return _webView!;
            }
        }

        public void loadPage(WebView web_view, string html_file_name)
        {
            pageLoaded = false;
            _webView = web_view;
            _webView.Source = generatePage(html_file_name);
            _webView.Navigated += webViewNavigated;
            _webView.Navigating += webViewNavigating;
        }

        protected void webViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
#if WINDOWS
            if (_webView == null) return;
            CoreWebView2 coreWebView2 = (_webView.Handler.PlatformView as Microsoft.Maui.Platform.MauiWebView).CoreWebView2;
            coreWebView2.Settings.IsStatusBarEnabled = false;
            coreWebView2.Settings.AreDevToolsEnabled = true;

#endif
        }

        protected async void webViewNavigated(object? sender, WebNavigatedEventArgs e)
        {
            if (pageLoaded = await checkIfPageLoaded())
            {
                processMessageQueue();
            }
        }

        private void evaluateJavascript(string script)
        {
            if (_webView == null)
                return;

            MainThread.BeginInvokeOnMainThread(() => {
                try
                {
                    _webView.EvaluateJavaScriptAsync("try{ " + script + " }catch(e){  }");
                }
                catch { }
            });
        }

        private void processMessageQueue()
        {
            if (_webView == null)
                return;

            while (messageQueue.Count > 0)
            {
                var message = messageQueue.Dequeue();
                evaluateJavascript(message);
            }
        }

        public void sendMessage(string msg)
        {
            if (pageLoaded && _webView != null)
            {
                evaluateJavascript(msg);
            }
            else
            {
                messageQueue.Enqueue(msg);
            }
        }

        private async Task<bool> checkIfPageLoaded()
        {
            if (_webView == null)
                return false;

            var tcs = new TaskCompletionSource<string>();
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var result = await _webView.EvaluateJavaScriptAsync("document.readyState");
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return (await tcs.Task)?.Trim('\"') == "complete";
        }

        public virtual void reload()
        {
            if (_webView != null)
            {
                pageLoaded = false;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _webView.Reload();
                });
            }
        }

        public WebViewSource generatePage(string html_file_name)
        {
            if (OperatingSystem.IsAndroid())
            {
                var source = new HtmlWebViewSource();
                Stream stream = SPlatformUtils.getAsset(Path.Combine("html", html_file_name));
                source.BaseUrl = SPlatformUtils.getAssetsBaseUrl() + "html/";
                source.Html = SpixiLocalization.localizeHtml(stream);
                stream.Close();
                stream.Dispose();
                return source;
            }
            else
            {
                string assets_file_path = Path.Combine(SPlatformUtils.getAssetsPath(), "html", html_file_name);
                string localized_file_path = Path.Combine(SPlatformUtils.getHtmlPath(), "ll_" + html_file_name);
                SpixiLocalization.localizeHtml(assets_file_path, localized_file_path);
                return new UrlWebViewSource
                {
                    Url = SPlatformUtils.getHtmlBaseUrl() + "ll_" + html_file_name
                };
            }
        }


        public virtual void recalculateLayout()
        {

        }

        public Task<bool> displaySpixiAlert(string title, string message, string ok, string cancel)
        {
            try
            {
                var tcs = new TaskCompletionSource<bool>();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var result = await DisplayAlert(title, message, ok, cancel);
                        tcs.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        Logging.error("Exception occured in displaySpixiAlert: " + ex);
                        tcs.TrySetException(ex);
                    }
                });
                return tcs.Task;
            }
            catch (Exception e)
            {
                Logging.error("Exception occured in displaySpixiAlert: " + e);
            }
            return null;
        }

        public Task displaySpixiAlert(string title, string message, string cancel)
        {
            try
            {
                var tcs = new TaskCompletionSource<Task>();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        var result = DisplayAlert(title, message, cancel);
                        tcs.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        Logging.error("Exception occured in displaySpixiAlert: " + ex);
                        tcs.TrySetException(ex);
                    }
                });
                return tcs.Task;
            }
            catch (Exception e)
            {
                Logging.error("Exception occured in displaySpixiAlert: " + e);
            }
            return null;
        }

        public void displayCallBar(byte[] session_id, string text, long call_started_time)
        {
            if (_webView == null)
            {
                return;
            }
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Utils.sendUiCommand(this, "displayCallBar", Crypto.hashToString(session_id), text, call_started_time.ToString());
            });
        }

        public void hideCallBar()
        {
            if (_webView == null)
            {
                return;
            }
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Utils.sendUiCommand(this, "hideCallBar");
            });
        }

        public void displayAppRequests()
        {
            if (_webView == null)
            {
                return;
            }
            Utils.sendUiCommand(this, "clearAppRequests");
            var app_pages = Node.MiniAppManager.getAppPages();
            lock (app_pages)
            {
                foreach (MiniAppPage page in app_pages.Values)
                {
                    if (page.accepted)
                    {
                        continue;
                    }
                    Friend f = FriendList.getFriend(page.hostUserAddress);
                    MiniApp app = Node.MiniAppManager.getApp(page.appId);
                    string text = string.Format(SpixiLocalization._SL("global-app-wants-to-use"), f.nickname, app.name);
                    Utils.sendUiCommand(this, "addAppRequest", Crypto.hashToString(page.sessionId), text, SpixiLocalization._SL("global-app-accept"), SpixiLocalization._SL("global-app-reject"));
                }
                if (VoIPManager.isInitiated())
                {
                    if (VoIPManager.currentCallAccepted)
                    {
                        if (VoIPManager.currentCallCalleeAccepted)
                        {
                            displayCallBar(VoIPManager.currentCallSessionId, SpixiLocalization._SL("global-call-in-call") + " - " + VoIPManager.currentCallContact.nickname, VoIPManager.currentCallStartedTime);
                        }
                        else
                        {
                            displayCallBar(VoIPManager.currentCallSessionId, SpixiLocalization._SL("global-call-dialing") + " " + VoIPManager.currentCallContact.nickname + "...", 0);
                        }
                    }
                    else
                    {
                        Friend f = VoIPManager.currentCallContact;
                        string text = SpixiLocalization._SL("global-call-incoming") + " - " + f.nickname;
                        Utils.sendUiCommand(this, "addCallAppRequest", f.walletAddress.ToString(), Crypto.hashToString(VoIPManager.currentCallSessionId), text);
                    }
                }
            }
        }

        public void onAppAccept(Address sender_address, string session_id)
        {
            byte[] b_session_id = Crypto.stringToHash(session_id);
            if (VoIPManager.hasSession(b_session_id))
            {
                VoIPManager.acceptCall(b_session_id);
                return;
            }
            MiniAppPage app_page = Node.MiniAppManager.acceptAppRequest(sender_address, b_session_id);
            if (app_page != null)
            {
                Navigation.PushAsync(app_page, Config.defaultXamarinAnimations);
            }// TODO else error?
        }

        public void onAppReject(Address sender_address, string session_id)
        {
            byte[] b_session_id = Crypto.stringToHash(session_id);
            if (VoIPManager.hasSession(b_session_id))
            {
                VoIPManager.rejectCall(b_session_id);
                return;
            }
            Node.MiniAppManager.rejectAppRequest(sender_address, b_session_id);
        }

        public virtual void updateScreen()
        {
            if (UIHelpers.refreshAppRequests)
            {
                displayAppRequests();
                UIHelpers.refreshAppRequests = false;
            }
        }

        public virtual void onResume()
        {

        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            UIHelpers.refreshAppRequests = true;
            updateScreen();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            Dispose();
        }

        protected bool onNavigatingGlobal(string url)
        {
            if (url.StartsWith("ixian:appAccept:"))
            {
                var split = url.Split(':');
                onAppAccept(new Address(split[2]), split[3]);
            }
            else if (url.StartsWith("ixian:appReject:"))
            {
                var split = url.Split(':');
                onAppReject(new Address(split[2]), split[3]);
            }
            else if (url.StartsWith("ixian:hangUp:"))
            {
                string session_id = url.Substring("ixian:hangUp:".Length);
                VoIPManager.hangupCall(Crypto.stringToHash(session_id));
            }
            else
            {
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            if (!Navigation.NavigationStack.Contains(this))
            {
                pageLoaded = false;
                messageQueue.Clear();

                if (_webView != null)
                {
                    _webView.Navigated -= webViewNavigated;
                    _webView.Navigating -= webViewNavigating;
                    _webView.Handler?.DisconnectHandler();
                    _webView = null;
                }
            }
        }

        public void popPageAsync()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                Page page = await Navigation.PopAsync(Config.defaultXamarinAnimations);
                if (page != null
                    && page is SpixiContentPage)
                {
                    await Task.Delay(200);
                    ((SpixiContentPage)page).Dispose();
                }
            });
        }

        public void popToRootAsync()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var mainPage = (Application.Current.MainPage as NavigationPage);
                while (mainPage.Navigation.NavigationStack.Count > 2)
                {
                    var page = mainPage.Navigation.NavigationStack[mainPage.Navigation.NavigationStack.Count - 2];
                    if (page != null)
                    {
                        Navigation.RemovePage(page);
                        if (page is SpixiContentPage)
                        {
                            ((SpixiContentPage)page).Dispose();
                        }
                    }
                }
                if (mainPage.Navigation.NavigationStack.Count > 1)
                {
                    Page page = await Navigation.PopAsync(Config.defaultXamarinAnimations);
                    if (page != null
                        && page is SpixiContentPage)
                    {
                        await Task.Delay(200);
                        ((SpixiContentPage)page).Dispose();
                    }
                }
            });
        }

        public void removePage(Page page)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (page != null)
                {
                    Navigation.RemovePage(page);
                    if (page is SpixiContentPage)
                    {
                        ((SpixiContentPage)page).Dispose();
                    }
                }
            });
        }
    }
}