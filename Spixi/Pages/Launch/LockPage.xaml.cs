using IXICore;
using Plugin.Fingerprint.Abstractions;
using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class LockPage : SpixiContentPage
    {
        private CancellationTokenSource _cancel;
        private bool justConfirmAction = false;
        public event EventHandler<SPIXI.EventArgs<bool>> authSucceeded;
        public event EventHandler<SPIXI.EventArgs<bool>> authWithPassword;

        public LockPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "lock.html");
        }

        public LockPage(bool justConfirm)
        {
            justConfirmAction = justConfirm;
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "lock.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private async void onLoad()
        {
            if(justConfirmAction)
                Utils.sendUiCommand(this, "setJustConfirm", "True");

            if (Device.RuntimePlatform == Device.WinUI)
                return;

            // Show biometric and alternative authentication methods
            await AuthenticateAsync(SpixiLocalization._SL("global-lock-auth-text"));
        }

        private void onNavigating(object sender, WebNavigatingEventArgs e)
        {
            string current_url = HttpUtility.UrlDecode(e.Url);

            if (current_url.Equals("ixian:onload", StringComparison.Ordinal))
            {
                onLoad();
            }
            else if (current_url.Equals("ixian:back", StringComparison.Ordinal))
            {
                // No back button for this screen
            }
            else if (current_url.Contains("ixian:unlock:"))
            {
                // Retrieve the password and unlock
                string[] split = current_url.Split(new string[] { "ixian:unlock:" }, StringSplitOptions.None);
                string pass = split[1];
                if (pass != null)
                    doUnlock(pass);
            }
            else if (current_url.Equals("ixian:change", StringComparison.Ordinal))
            {
                if (justConfirmAction)
                {
                    if (authSucceeded != null)
                    {
                        authSucceeded(this, new SPIXI.EventArgs<bool>(false));
                    }
                    Navigation.PopModalAsync();
                }
                else
                {
                    // Show the launch screen
                    Navigation.PushAsync(new SPIXI.LaunchPage(), Config.defaultXamarinAnimations);
                    removePage(this);
                }
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        private void doUnlock(string pass)
        {
            string target_filepath = Path.Combine(Config.spixiUserFolder, Config.walletFile);
            WalletStorage ws = new WalletStorage(target_filepath);
            if (!ws.verifyWallet(target_filepath, pass))
            {
                displaySpixiAlert(SpixiLocalization._SL("intro-restore-file-invalidpassword-title"), SpixiLocalization._SL("intro-restore-file-invalidpassword-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
            else
            {
                if(authWithPassword != null)
                {
                    authWithPassword(this, new SPIXI.EventArgs<bool>(true));
                }
                performUnlock();
            }
        }

        private async void performUnlock()
        {
            if (authSucceeded != null)
            {
                authSucceeded(this, new SPIXI.EventArgs<bool>(true));
            }

            if (justConfirmAction)
            {              
                Navigation.PopModalAsync();
                return;
            }
            Navigation.InsertPageBefore(HomePage.Instance(true), this);

            removePage(this);           
        }

        protected override bool OnBackButtonPressed()
        {
            return true;
        }

        private async Task AuthenticateAsync(string reason, string cancel = null, string fallback = null, string tooFast = null)
        {
            _cancel = new CancellationTokenSource();

            var dialogConfig = new AuthenticationRequestConfiguration("SPIXI", reason)
            {
                CancelTitle = cancel,
                FallbackTitle = fallback,
                AllowAlternativeAuthentication = true,
                ConfirmationRequired = true
            };

            dialogConfig.HelpTexts.MovedTooFast = tooFast;

            var result = await Plugin.Fingerprint.CrossFingerprint.Current.AuthenticateAsync(dialogConfig, _cancel.Token);

            await SetResultAsync(result);
        }
        
        private Task SetResultAsync(FingerprintAuthenticationResult result)
        {
            if (result.Authenticated)
            {
                performUnlock();
            }
            else
            {
                _ = displaySpixiAlert(SpixiLocalization._SL("global-lock-invalidpassword-title"), SpixiLocalization._SL("global-lock-invalidpassword-text"), "Cancel");
            }

            return Task.CompletedTask;
        }

    }
}