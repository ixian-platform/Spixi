using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using SPIXI.MiniApps;
using System;
using System.Linq;
using System.Text;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AppNewPage : SpixiContentPage
    {
        public AppNewPage()
        {
            InitializeComponent();

            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "app_new.html");
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
            else if (current_url.Equals("ixian:quickscan", StringComparison.Ordinal))
            {
                quickScan();
            }
            else if (current_url.Contains("ixian:qrresult:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:qrresult:" }, StringSplitOptions.None);
                string result = split[1];
                processQRResult(result);
                e.Cancel = true;
                return;
            }
            else if (current_url.StartsWith("ixian:fetch:"))
            {
                string url = current_url.Substring("ixian:fetch:".Length);
                onFetch(url);
            }
            else if (current_url.StartsWith("ixian:install:"))
            {
                string url = current_url.Substring("ixian:install:".Length);
                onInstall(url);
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
            // Execute timer-related functionality immediately
            updateScreen();
        }

        // Executed every second
        public override void updateScreen()
        {

        }

        public async void quickScan()
        {
            var scanPage = new ScanPage();
            scanPage.scanSucceeded += HandleScanSucceeded;
            await Navigation.PushAsync(scanPage, Config.defaultXamarinAnimations);
        }

        private void HandleScanSucceeded(object sender, SPIXI.EventArgs<string> e)
        {
            string mini_app_install_url = e.Value;
            processQRResult(mini_app_install_url);
        }

        public void processQRResult(string result)
        {
            if (result.Contains(":ixi"))
            {
                string[] split = result.Split(new string[] { ":ixi" }, StringSplitOptions.None);
                if (split.Count() < 1)
                    return;
                string appUrl = split[0];
                Utils.sendUiCommand(this, "setScannedData", appUrl);
            }
            else
            {
                string appUrl = result;
                // TODO: enter exact Ixian address length
                if (appUrl.Length > 20 && appUrl.Length < 128)
                    Utils.sendUiCommand(this, "setScannedData", appUrl);
            }

        }

        private void onFetch(string path)
        {
            MiniApp? app = Node.MiniAppManager.fetch(path);
            if(app == null)
            {
                Utils.sendUiCommand(this, "showUrlError");
                return;
            }

            app.url = path;
            
            Navigation.PushAsync(new AppDetailsPage(app), Config.defaultXamarinAnimations);
            Navigation.RemovePage(this);
        }

        private void onInstall(string path)
        {
            string app_name = Node.MiniAppManager.install(path);
            if (app_name != null)
            {
                UIHelpers.shouldRefreshApps = true;
                displaySpixiAlert(SpixiLocalization._SL("app-new-dialog-title"), string.Format(SpixiLocalization._SL("app-new-dialog-installed-text"), app_name), SpixiLocalization._SL("global-dialog-ok"));
                Navigation.PopAsync(Config.defaultXamarinAnimations);
            }
            else
            {
                displaySpixiAlert(SpixiLocalization._SL("app-new-dialog-title"), SpixiLocalization._SL("app-new-dialog-installfailed-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
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
    }
}