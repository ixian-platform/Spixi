using Spixi;
using SPIXI.Interfaces;
using SPIXI.Meta;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class DownloadsPage : SpixiContentPage
    {
        public DownloadsPage()
        {
            InitializeComponent();

            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "downloads.html");
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
            else if (current_url.StartsWith("ixian:open:"))
            {
                string file_name = current_url.Substring("ixian:open:".Length);
                string path = Path.Combine(TransferManager.downloadsPath, file_name);
                if (File.Exists(path))
                {
                    SFileOperations.open(path);
                }
            }
            else if (current_url.StartsWith("ixian:delete:"))
            {
                string file_name = current_url.Substring("ixian:delete:".Length);
                string path = Path.Combine(TransferManager.downloadsPath, file_name);
                if(File.Exists(path))
                {
                    File.Delete(path);
                    onLoad();
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

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void loadFiles()
        {
            Utils.sendUiCommand(this, "clearFiles");

            foreach (var path in Directory.EnumerateFiles(TransferManager.downloadsPath))
            {
                Utils.sendUiCommand(this, "addFile", Path.GetFileName(path), File.GetCreationTime(path).ToString());
            }
        }

        private void onLoad()
        {
            loadFiles();

            // Execute timer-related functionality immediately
            updateScreen();
        }

        // Executed every second
        public override void updateScreen()
        {

        }

        private void onBack()
        {
            Navigation.PopModalAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            onBack();

            return true;
        }
    }
}