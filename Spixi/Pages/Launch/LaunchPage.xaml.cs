using IXICore.Meta;
using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using System;
using System.IO;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class LaunchPage : SpixiContentPage
	{
	    ThemeAppearance selectedAppearance = ThemeAppearance.automatic;
        private bool acceptedTerms = false;
		public LaunchPage ()
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "intro.html");
        }

        private void onLoad()
        {
            Utils.sendUiCommand(this, "setVersion", Config.version);
            if(!acceptedTerms)
            {
                Utils.sendUiCommand(this, "showTerms");
            }
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            
        }

        private void onNavigating(object sender, WebNavigatingEventArgs e)
        {
            string current_url = HttpUtility.UrlDecode(e.Url);

            if (current_url.Equals("ixian:introload", StringComparison.Ordinal))
            {
                onLoad();
            }
            else if (current_url.Equals("ixian:create", StringComparison.Ordinal))
            {
                Navigation.PushAsync(new LaunchCreatePage(), Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:restore", StringComparison.Ordinal))
            {
                Navigation.PushAsync(new LaunchRestorePage(), Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:accept", StringComparison.Ordinal))
            {
                acceptedTerms = true;
            }
            else if (current_url.StartsWith("ixian:language:", StringComparison.Ordinal))
            {
                string lang = current_url.Substring("ixian:language:".Length);
                if(SpixiLocalization.loadLanguage(lang))
                {
                    Preferences.Default.Set("language", lang);
                }
                loadPage(webView, "intro.html");
                Utils.sendUiCommand(this, "showOnboardingSection");
            }
            else if (current_url.StartsWith("ixian:appearance:", StringComparison.Ordinal))
                {
                    string appearanceString = current_url.Substring("ixian:appearance:".Length);
                    selectedAppearance = (ThemeAppearance)Convert.ToInt32(appearanceString);

                    if (ThemeManager.changeAppearance(selectedAppearance))
                    {
                        loadPage(webView, "intro.html");
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

        public void onCreateAccount(object sender, EventArgs e)
        {
            Navigation.PushAsync(new LaunchCreatePage(), Config.defaultXamarinAnimations);
        }

        public void onRestoreAccount(object sender, EventArgs e)
        {
            Navigation.PushAsync(new LaunchRestorePage(), Config.defaultXamarinAnimations);
        }
    }
}