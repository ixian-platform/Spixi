using IXICore.Streaming;
using SPIXI.Meta;
using System.Web;

namespace SPIXI
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class WalletRecipientPage : SpixiContentPage
	{
        public event EventHandler<SPIXI.EventArgs<string>> pickSucceeded;

        public WalletRecipientPage ()
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "wallet_recipient.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            loadContacts();
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
            else if (current_url.Equals("ixian:newcontact", StringComparison.Ordinal))
            {
                var recipientPage = new ContactNewPage();
                recipientPage.pickSucceeded += HandleNewContactSucceeded;
                Navigation.PushAsync(recipientPage, Config.defaultXamarinAnimations);
            }
            else if (current_url.Contains("ixian:select:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:select:|" }, StringSplitOptions.None);
                string id = split[1];
                onPickSucceeded(id);
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        private void HandleNewContactSucceeded(object sender, SPIXI.EventArgs<string> e)
        {
            string id = e.Value;
            onPickSucceeded(id);
            Navigation.PopModalAsync();
        }

        private void onPickSucceeded(string id)
        {
            if (pickSucceeded != null)
            {
                pickSucceeded(this, new SPIXI.EventArgs<string>(id));
            }
        }

        public void loadContacts()
        {
            if(FriendList.friends.Count == 0)
            {
                Utils.sendUiCommand(this, "noContacts");
                return;
            }

            Utils.sendUiCommand(this, "clearContacts");

            foreach (Friend friend in FriendList.friends)
            {
                string str_online = "false";
                if (friend.online)
                    str_online = "true";
                
                Utils.sendUiCommand(this, "addContact", friend.walletAddress.ToString(), friend.nickname, "img/spixiavatar.png", str_online);
            }
        }

        protected override bool OnBackButtonPressed()
        {
            Navigation.PopModalAsync();

            return true;
        }

        protected override void OnAppearing()
        {
            loadContacts();
        }

    }
}