using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using System;
using System.Web;

namespace SPIXI
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class EmptyDetail : SpixiContentPage
	{
		public EmptyDetail()
		{
			InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "empty_detail.html");
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
            return true;
        }
    }
}