using SPIXI.Meta;
using System;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class SetLockPage : SpixiContentPage
	{
		public SetLockPage ()
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "settings_lock.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            //webView.Eval(string.Format("setNickname(\"{0}\")", IxianHandler.localStorage.nickname));
            Utils.sendUiCommand(this, "unlock");
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
                Navigation.PopAsync(Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:unlock", StringComparison.Ordinal))
            {
                Utils.sendUiCommand(this, "unlock");
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
            Navigation.PopAsync(Config.defaultXamarinAnimations);

            return true;
        }
    }
}