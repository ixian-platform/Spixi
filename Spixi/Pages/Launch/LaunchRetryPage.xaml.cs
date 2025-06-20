using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using System;
using System.Linq;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class LaunchRetryPage : SpixiContentPage
    {
        private int attempts = 0;   // Number of wrong password attempts

		public LaunchRetryPage ()
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "intro_retry.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {

        }

        private void onNavigating(object sender, WebNavigatingEventArgs e)
        {
            string current_url = HttpUtility.UrlDecode(e.Url);

            if (current_url.Equals("ixian:back", StringComparison.Ordinal))
            {
                popPageAsync();
            }
            else if (current_url.Contains("ixian:proceed:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:proceed:" }, StringSplitOptions.None);
                if(split.Count() < 1)
                {
                    e.Cancel = true;
                    return;
                }

                string password = split[1]; // Todo: secure this
                proceed(password);
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        private void proceed(string pass)
        {
            // TODO: encrypt the password
            Preferences.Default.Set("walletpass", pass);

            bool wallet_decrypted = Node.loadWallet();

            if (wallet_decrypted == false)
            {
                displaySpixiAlert(SpixiLocalization._SL("intro-retry-invalidpassword-title"), SpixiLocalization._SL("intro-retry-invalidpassword-text"), SpixiLocalization._SL("global-dialog-ok"));

                // If too many wrong attempts, throw the user to the launch screen, allowing creation or restoration of wallet
                attempts++;
                if(attempts > Config.encryptionRetryPasswordAttempts)
                {
                    Navigation.PushAsync(new LaunchPage(), Config.defaultXamarinAnimations);
                    removePage(this);
                }

                // Remove overlay
                Utils.sendUiCommand(this, "removeLoadingOverlay");

                return;
            }

            Navigation.PushAsync(HomePage.Instance(true), Config.defaultXamarinAnimations);
            removePage(this);
        }
    }
}