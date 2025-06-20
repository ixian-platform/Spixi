using IXICore.Meta;
using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using System;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class EncryptionPassword : SpixiContentPage
    {
		public EncryptionPassword ()
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "settings_encryption.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {

        }

        private void onNavigating(object sender, WebNavigatingEventArgs e)
        {
            string current_url = HttpUtility.UrlDecode(e.Url);

            if (onNavigatingGlobal(current_url))
            {
                e.Cancel = true;
                return;
            }

            if (current_url.Equals("ixian:onload", StringComparison.Ordinal))
            {
                onLoad();
            }
            else if (current_url.Equals("ixian:back", StringComparison.Ordinal))
            {
                popPageAsync();
            }
            else if (current_url.Equals("ixian:error", StringComparison.Ordinal))
            {
                displaySpixiAlert(SpixiLocalization._SL("settings-encryption-invalidpassword-title"), SpixiLocalization._SL("settings-encryption-invalidpassword-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
            else if (current_url.StartsWith("ixian:changepass:", StringComparison.Ordinal))
            {
                string[] split_url = current_url.Split(new string[] { "--1ec4ce59e0535704d4--" }, StringSplitOptions.None);
                string old_password = split_url[1];
                string new_password = split_url[2];
                if (IxianHandler.getWalletStorage().isValidPassword(old_password))
                {
                    IxianHandler.getWalletStorage().writeWallet(new_password);
                    displaySpixiAlert(SpixiLocalization._SL("settings-encryption-passwordchanged-title"), SpixiLocalization._SL("settings-encryption-passwordchanged-text"), SpixiLocalization._SL("global-dialog-ok"));
                    popPageAsync();
                }
                else
                {
                    displaySpixiAlert(SpixiLocalization._SL("settings-encryption-invalidpassword-title"), SpixiLocalization._SL("settings-encryption-invalidpassword-current-text"), SpixiLocalization._SL("global-dialog-ok"));
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

        protected override bool OnBackButtonPressed()
        {
            popPageAsync();

            return true;
        }
    }
}