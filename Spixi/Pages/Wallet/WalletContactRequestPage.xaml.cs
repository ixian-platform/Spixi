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
	public partial class WalletContactRequestPage : SpixiContentPage
	{
        private Friend friend = null;
        private FriendMessage requestMsg = null;
        private string amount = null;
        private string date = null;

        public WalletContactRequestPage (FriendMessage request_msg, Friend fr, string am, string dt)
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);

            friend = fr;
            amount = am;
            date = dt;
            if(request_msg != null)
            {
                requestMsg = request_msg;
            }

            loadPage(webView, "wallet_contact_request.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            Utils.sendUiCommand(this, "setData", friend.walletAddress.ToString(), friend.nickname, amount, ConsensusConfig.forceTransactionPrice.ToString(), date);
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
            else if (current_url.Equals("ixian:decline", StringComparison.Ordinal))
            {
                onDecline();
            }
            else if (current_url.Equals("ixian:send", StringComparison.Ordinal))
            {
                onSend();
            }
            else if (current_url.Equals("ixian:back", StringComparison.Ordinal))
            {
                popPageAsync();
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        private void onDecline()
        {
            if (requestMsg != null)
            {
                // send decline
                if (!requestMsg.message.StartsWith(":"))
                {
                    string msg_id = Crypto.hashToString(requestMsg.id);

                    SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.requestFundsResponse, Encoding.UTF8.GetBytes(msg_id));

                    requestMsg.message = "::" + requestMsg.message;

                    StreamMessage message = new StreamMessage();
                    message.type = StreamMessageCode.info;
                    message.recipient = friend.walletAddress;
                    message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                    message.data = spixi_message.getBytes();

                    CoreStreamProcessor.sendMessage(friend, message);

                    IxianHandler.localStorage.requestWriteMessages(friend.walletAddress, 0);

                    var chat_page = Utils.getChatPage(friend);
                    if (chat_page != null)
                    {
                        chat_page.updateRequestFundsStatus(requestMsg.id, null, SpixiLocalization._SL("chat-payment-status-declined"));
                    }
                }
            }
            popPageAsync();
        }

        private void onSend()
        {
            IxiNumber fee = ConsensusConfig.forceTransactionPrice;
            IxiNumber relayFee = fee * 4;

            IxiNumber _amount = amount;

            if (_amount < (long)0)
            {
                displaySpixiAlert(SpixiLocalization._SL("wallet-error-amount-title"), SpixiLocalization._SL("wallet-error-amount-text"), SpixiLocalization._SL("global-dialog-ok"));
                return;
            }

            IxiNumber availableBalance = Node.getAvailableBalance();
            if (_amount + fee + relayFee > availableBalance)
            {
                string alert_body = String.Format(SpixiLocalization._SL("wallet-error-balance-text"), _amount + fee, availableBalance);
                displaySpixiAlert(SpixiLocalization._SL("wallet-error-balance-title"), alert_body, SpixiLocalization._SL("global-dialog-ok"));
                return;
            }


            string msg_id = Crypto.hashToString(requestMsg.id);

            // Send tx details to the request
            if (!requestMsg.message.StartsWith(":"))
            {
                // Create an ixian transaction and send it to the dlt network
                Address to = friend.walletAddress;
                
                Address from = IxianHandler.getWalletStorage().getPrimaryAddress();

                Transaction transaction = Node.sendTransactionFrom(from, to, amount);
                
                SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.requestFundsResponse, Encoding.UTF8.GetBytes(msg_id + ":" + transaction.getTxIdString()));

                requestMsg.message = ":" + transaction.getTxIdString();

                StreamMessage message = new StreamMessage();
                message.type = StreamMessageCode.info;
                message.recipient = to;
                message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                message.data = spixi_message.getBytes();

                CoreStreamProcessor.sendMessage(friend, message);

                IxianHandler.localStorage.requestWriteMessages(friend.walletAddress, 0);

                var chat_page = Utils.getChatPage(friend);
                if (chat_page != null)
                {
                    chat_page.updateRequestFundsStatus(requestMsg.id, transaction.id, SpixiLocalization._SL("chat-payment-status-pending"));
                }
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