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

        public AppDetailsPage(string app_id)
        {
            InitializeComponent();

            appId = app_id;

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
            else if (current_url.StartsWith("ixian:uninstall", StringComparison.Ordinal))
            {
                onUninstall();
            }
            else if (current_url.StartsWith("ixian:startApp", StringComparison.Ordinal))
            {
                onStartApp();
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
            MiniApp app = Node.MiniAppManager.getApp(appId);

            string icon = Node.MiniAppManager.getAppIconPath(appId);
            if(icon == null)
            {
                icon = "";
            }

            Utils.sendUiCommand(this, "init", app.name, icon, app.publisher, app.version, app.getCapabilitiesAsArray(), app.hasCapability(MiniAppCapabilities.SingleUser).ToString(), appId);

            // Execute timer-related functionality immediately
            updateScreen();
        }

        private void onUninstall()
        {
            if(Node.MiniAppManager.remove(appId))
            {
                displaySpixiAlert(SpixiLocalization._SL("app-details-dialog-title"), SpixiLocalization._SL("app-details-dialog-removed-text"), SpixiLocalization._SL("global-dialog-ok"));
            }else
            {
                displaySpixiAlert(SpixiLocalization._SL("app-details-dialog-title"), SpixiLocalization._SL("app-details-dialog-removefailed-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
            Navigation.PopAsync(Config.defaultXamarinAnimations);
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

        public void onStartApp()
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