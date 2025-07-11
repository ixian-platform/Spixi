using Spixi;
using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class OnboardPage : SpixiContentPage
    {
        public event EventHandler<SPIXI.EventArgs<bool>> onboardDone;
        public bool joinBot = false;

        public OnboardPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "onboarding.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {

        }

        private void onNavigating(object sender, WebNavigatingEventArgs e)
        {
            string current_url = HttpUtility.UrlDecode(e.Url);

            if (current_url.Equals("ixian:back", StringComparison.Ordinal))
            {
                Navigation.PopModalAsync();
            }
            else if (current_url.Contains("ixian:joinbot"))
            {
                joinBot = true;
                finishOnboarding();
            }
            else if (current_url.Equals("ixian:error", StringComparison.Ordinal))
            {
                displaySpixiAlert(SpixiLocalization._SL("intro-new-emptynick-title"), SpixiLocalization._SL("intro-new-emptynick-text"), SpixiLocalization._SL("global -dialog-ok"));
            }
            else if (current_url.Equals("ixian:finish", StringComparison.Ordinal))
            {
                finishOnboarding();
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        private void finishOnboarding()
        {
            if (onboardDone != null)
            {
                onboardDone(this, new SPIXI.EventArgs<bool>(joinBot));
            }
            Navigation.PopModalAsync(false);
        }

        protected override bool OnBackButtonPressed()
        {
            Navigation.PopModalAsync(false);

            return true;
        }
    }
}
