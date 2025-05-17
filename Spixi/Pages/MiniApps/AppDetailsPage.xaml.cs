using IXICore.Meta;
using SPIXI.MiniApps;
using SPIXI.Lang;
using SPIXI.Meta;
using System;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AppDetailsPage : SpixiContentPage
    {
        string appId = null;

        MiniApp fetchedApp = null;

        public AppDetailsPage(string app_id)
        {
            InitializeComponent();

            appId = app_id;

            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "app_details.html");
        }

        public AppDetailsPage(MiniApp app)
        {
            InitializeComponent();

            fetchedApp = app;

            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "app_details.html");
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

            if (current_url.StartsWith("ixian:onload", StringComparison.Ordinal))
            {
                onLoad();
            }
            else if (current_url.Equals("ixian:back", StringComparison.Ordinal))
            {
                onBack();
            }
            else if (current_url.StartsWith("ixian:install", StringComparison.Ordinal))
            {
                onInstall();
            }
            else if (current_url.StartsWith("ixian:uninstall", StringComparison.Ordinal))
            {
                onUninstall();
            }
            else if (current_url.StartsWith("ixian:details", StringComparison.Ordinal))
            {
                onDetails();
            }
            else if (current_url.StartsWith("ixian:startApp", StringComparison.Ordinal))
            {
                string appId = current_url.Substring("ixian:startApp:".Length);
                onStartApp(appId);
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
            MiniApp app = fetchedApp;
            string icon = null;
            if (app == null)
            {
                app = Node.MiniAppManager.getApp(appId);
                icon = Node.MiniAppManager.getAppIconPath(appId);
            }
            else
            {
                appId = app.id;
                icon = app.image;
            }


            if(icon == null)
            {
                icon = "";
            }

            var app_list = Node.MiniAppManager.getInstalledApps();
            bool app_installed = app_list.ContainsKey(appId);
            bool app_verified = true;

            Utils.sendUiCommand(this, "init", 
                app.name, 
                icon, app.publisher, 
                app.description, 
                app.version, 
                app.url, 
                Utils.bytesToHumanFormatString(app.contentSize), 
                app.getCapabilitiesAsString(), 
                appId, 
                app.hasCapability(MiniAppCapabilities.MultiUser).ToString(), 
                app_installed.ToString(),
                app_verified.ToString());

            // Execute timer-related functionality immediately
            updateScreen();
        }

        private void onInstall()
        {
            if(fetchedApp == null)
            {
                return;
            }

            Utils.sendUiCommand(this, "showInstalling");

            string app_name = Node.MiniAppManager.install(fetchedApp);
            if (app_name != null)
            {
                Node.shouldRefreshApps = true;
                Utils.sendUiCommand(this, "showInstallSuccess");
                
            }
            else
            {               
                Utils.sendUiCommand(this, "showInstallFailed");
            }

        }

        private void onUninstall()
        {
            if(Node.MiniAppManager.remove(appId))
            {
                Utils.sendUiCommand(this, "showAppRemoved");
            }
            else
            {
                displaySpixiAlert(SpixiLocalization._SL("app-details-dialog-title"), SpixiLocalization._SL("app-details-dialog-removefailed-text"), SpixiLocalization._SL("global-dialog-ok"));
                Navigation.PopAsync(Config.defaultXamarinAnimations);
            }
            Node.shouldRefreshApps = true;
        }

        private void onDetails()
        {
            MiniApp app = Node.MiniAppManager.getApp(appId);
            if (app == null)
            {
                return;
            }

            Navigation.PushAsync(new AppDetailsPage(app), Config.defaultXamarinAnimations);
            Navigation.RemovePage(this);
        }

        // Executed every second
        public override void updateScreen()
        {

        }

        private void onBack()
        {
            Navigation.PopAsync(Config.defaultXamarinAnimations);
        }

        protected override bool OnBackButtonPressed()
        {
            onBack();

            return true;
        }

        public void onStartApp(string appId)
        {
            MiniAppPage MiniAppPage = new MiniAppPage(appId, IxianHandler.getWalletStorage().getPrimaryAddress(), null, Node.MiniAppManager.getAppEntryPoint(appId));
            MiniAppPage.accepted = true;
            Node.MiniAppManager.addAppPage(MiniAppPage);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Navigation.PushAsync(MiniAppPage, Config.defaultXamarinAnimations);
            });
        }
    }
}