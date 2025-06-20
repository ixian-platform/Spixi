using SPIXI.MiniApps;
using SPIXI.Interfaces;
using SPIXI.Meta;
using System;
using System.Linq;
using System.Text;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AppsPage : SpixiContentPage
    {
        public AppsPage()
        {
            InitializeComponent();

            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "apps.html");
        }

        public override void recalculateLayout()
        {
            ForceLayout();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            onLoad();
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

            if (current_url.StartsWith("ixian:onload", StringComparison.Ordinal))
            {
                onLoad();
            }
            else if (current_url.Equals("ixian:back", StringComparison.Ordinal))
            {
                onBack();
            }
            else if (current_url.StartsWith("ixian:details:"))
            {
                string app_id = current_url.Substring("ixian:details:".Length);
                onDetails(app_id);
            }
            else if (current_url.Equals("ixian:newapp", StringComparison.Ordinal))
            {
                Navigation.PushAsync(new AppNewPage(), Config.defaultXamarinAnimations);
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            loadApps();
            // Execute timer-related functionality immediately
            updateScreen();
        }

        private void onDetails(string app_id)
        {
            Navigation.PushAsync(new AppDetailsPage(app_id), Config.defaultXamarinAnimations);
        }

        private void loadApps()
        {
            Utils.sendUiCommand(this, "clearApps");

            var apps = Node.MiniAppManager.getInstalledApps();
            lock (apps)
            {
                foreach (var app_arr in apps)
                {
                    MiniApp app = app_arr.Value;
                    string icon = Node.MiniAppManager.getAppIconPath(app.id);
                    if(icon == null)
                    {
                        icon = "";
                    }
                    Utils.sendUiCommand(this, "addApp", app.id, app.name, icon);
                }
            }
        }

        // Executed every second
        public override void updateScreen()
        {

        }

        private void onBack()
        {
            popPageAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            onBack();

            return true;
        }
    }
}