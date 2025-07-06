using IXICore;
using IXICore.Meta;
using IXICore.Streaming;
using SPIXI.Lang;
using SPIXI.Meta;
using System.Text;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class WalletReceivePage : SpixiContentPage
	{
        private Friend local_friend = null;

		public WalletReceivePage ()
		{
			InitializeComponent ();
//            pubkeyAddress.Text = IxianHandler.getWalletStorage().address;
            NavigationPage.SetHasNavigationBar(this, false);


            loadPage(webView, "wallet_request.html");
        }

        public WalletReceivePage(Friend friend)
        {
            InitializeComponent();
            //            pubkeyAddress.Text = IxianHandler.getWalletStorage().address;
            NavigationPage.SetHasNavigationBar(this, false);

            local_friend = friend;

            loadPage(webView, "wallet_request.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            Utils.sendUiCommand(this, "setAddress", IxianHandler.getWalletStorage().getPrimaryAddress().ToString());

            // Check if this page is accessed from the home wallet
            if (local_friend == null)
            {
                //webView.Eval("hideRequest()");
            }
            else
            {
                Utils.sendUiCommand(this, "addRecipient", local_friend.nickname, local_friend.walletAddress.ToString());
            }
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
                popPageAsync();
            }
            else if (current_url.Equals("ixian:pick", StringComparison.Ordinal))
            {
                var recipientPage = new WalletRecipientPage();
                recipientPage.pickSucceeded += HandlePickSucceeded;
                Navigation.PushAsync(recipientPage, Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:error", StringComparison.Ordinal))
            {
                displaySpixiAlert(SpixiLocalization._SL("wallet-error-amount-title"), SpixiLocalization._SL("wallet-error-amount-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
            else if (current_url.Contains("ixian:sendrequest:"))
            {
                try
                {
                    string[] split = current_url.Split(new string[] { "ixian:sendrequest:" }, StringSplitOptions.None);

                    // Extract all addresses and amounts
                    string[] addresses_split = split[1].Split(new string[] { "|" }, StringSplitOptions.None);

                    foreach (var address_amount in addresses_split)
                    {
                        if(address_amount == "")
                        {
                            continue;
                        }

                        string[] split_address_amount = address_amount.Split(':');
                        if (split_address_amount.Count() < 2)
                            continue;

                        string recipient = split_address_amount[0];
                        string amount = split_address_amount[1];
                        if (Address.validateChecksum(Base58Check.Base58CheckEncoding.DecodePlain(recipient)) == false)
                        {
                            e.Cancel = true;
                            displaySpixiAlert(SpixiLocalization._SL("global-invalid-address-title"), SpixiLocalization._SL("global-invalid-address-text"), SpixiLocalization._SL("global-dialog-ok"));
                            return;
                        }
                        string[] amount_split = amount.Split(new string[] { "." }, StringSplitOptions.None);
                        if (amount_split.Length > 2)
                        {
                            displaySpixiAlert(SpixiLocalization._SL("wallet-error-amount-title"), SpixiLocalization._SL("wallet-error-amountdecimal-text"), SpixiLocalization._SL("global-dialog-ok"));
                            e.Cancel = true;
                            return;
                        }

                        // Add decimals if none found
                        if (amount_split.Length == 1)
                            amount = String.Format("{0}.0", amount);

                        IxiNumber _amount = amount;

                        if (_amount == 0)
                        {
                            displaySpixiAlert(SpixiLocalization._SL("wallet-error-amount-title"), SpixiLocalization._SL("wallet-error-amount-text"), SpixiLocalization._SL("global-dialog-ok"));
                            e.Cancel = true;
                            return;
                        }

                        if (_amount < (long)0)
                        {
                            displaySpixiAlert(SpixiLocalization._SL("wallet-error-amount-title"), SpixiLocalization._SL("wallet-error-amount-text"), SpixiLocalization._SL("global-dialog-ok"));
                            e.Cancel = true;
                            return;
                        }
                        onRequest(recipient, amount);
                    }
                }catch(Exception ex)
                {
                    Logging.error("Exception occurent for sendrequest action: " + ex);
                    displaySpixiAlert(SpixiLocalization._SL("wallet-request-error-title"), SpixiLocalization._SL("wallet-request-error-text"), SpixiLocalization._SL("global-dialog-ok"));
                }
            }
            else if (current_url.Contains("ixian:addrecipient"))
            {
                try
                {
                    string[] split = current_url.Split(new string[] { "ixian:addrecipient:" }, StringSplitOptions.None);
                    if (Address.validateChecksum(Base58Check.Base58CheckEncoding.DecodePlain(split[1])))
                    {
                        Utils.sendUiCommand(this, "addRecipient", split[1], split[1]);
                    }
                    else
                    {
                        displaySpixiAlert(SpixiLocalization._SL("global-invalid-address-title"), SpixiLocalization._SL("global-invalid-address-text"), SpixiLocalization._SL("global-dialog-ok"));
                    }
                }
                catch (Exception)
                {
                    displaySpixiAlert(SpixiLocalization._SL("global-invalid-address-title"), SpixiLocalization._SL("global-invalid-address-text"), SpixiLocalization._SL("global-dialog-ok"));
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

        private void onRequest(string recipient, string amount)
        {
            Address recipient_address = new Address(recipient);
            Friend friend = FriendList.getFriend(recipient_address);
            if (friend != null && (new IxiNumber(amount)) > 0)
            {
                FriendMessage friend_message = Node.addMessageWithType(null, FriendMessageType.requestFunds, friend.walletAddress, 0, amount, true);

                SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.requestFunds, Encoding.UTF8.GetBytes(amount));

                StreamMessage message = new StreamMessage(friend.protocolVersion);
                message.type = StreamMessageCode.info;
                message.recipient = recipient_address;
                message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                message.data = spixi_message.getBytes();
                message.id = friend_message.id;

                StreamProcessor.sendMessage(friend, message);

                popPageAsync();
            }// else error?

        }

        private async void HandlePickSucceeded(object sender, SPIXI.EventArgs<string> e)
        {
            string wallets_to_send = e.Value;

            string[] wallet_arr = wallets_to_send.Split('|');

            foreach (string wallet_to_send in wallet_arr)
            {
                Friend friend = FriendList.getFriend(new Address(wallet_to_send));

                string nickname = wallet_to_send;
                if (friend != null)
                    nickname = friend.nickname;

                Utils.sendUiCommand(this, "addRecipient", nickname, wallet_to_send);
            }

            popPageAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            popPageAsync();

            return true;
        }
    }
}