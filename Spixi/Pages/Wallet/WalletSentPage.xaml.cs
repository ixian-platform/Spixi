using IXICore;
using IXICore.Meta;
using IXICore.Storage;
using IXICore.Streaming;
using SPIXI.Lang;
using SPIXI.Meta;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class WalletSentPage : SpixiContentPage
    {
        private Transaction transaction = null;

        private bool viewOnly = true;

        private HomePage? homePage;

        public WalletSentPage(Transaction tx, bool view_only = true, HomePage? home = null)
        {
            viewOnly = view_only;

            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            webView.Opacity = 0;
            Content.BackgroundColor = ThemeManager.getBackgroundColor();

            transaction = tx;

            loadPage(webView, "wallet_sent.html");

            homePage = home;
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            if (transaction == null)
            {
                onDismiss();
                return;
            }
            else
            {
                checkTransaction();
            }
            if (homePage != null)
            {
                Utils.sendUiCommand(this, "hideBackButton");
            }

            webView.FadeTo(1, 150);
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
            else if (current_url.Equals("ixian:dismiss", StringComparison.Ordinal))
            {
                onDismiss();
            }
            else if (current_url.Equals("ixian:viewexplorer", StringComparison.Ordinal))
            {
                Browser.Default.OpenAsync(new Uri(String.Format("{0}?p=transaction&id={1}", Config.explorerUrl, transaction.getTxIdString())));
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        // Retrieve the transaction from local cache storage
        private void checkTransaction()
        {
            Utils.sendUiCommand(this, "clearEntries");

            string confirmed = "true";

            Transaction ctransaction = TransactionCache.getTransaction(transaction.id);
            if (ctransaction == null || ctransaction.applied == 0)
            {
                ctransaction = transaction;
                confirmed = "false";
            } else if (IxianHandler.getHighestKnownNetworkBlockHeight() > ctransaction.applied + Config.txConfirmationBlocks)
            {
                confirmed = "true";
            }

            IxiNumber amount = ctransaction.amount;

            string time = Utils.unixTimeStampToHumanFormatString(Convert.ToDouble(ctransaction.timeStamp));

            string type = "send";

            Address addr = ctransaction.pubKey;
            if (addr.SequenceEqual(IxianHandler.getWalletStorage().getPrimaryAddress()))
            {
                // this is a sent payment

                foreach (var entry in ctransaction.toList)
                {
                    Friend friend = FriendList.getFriend(entry.Key);
                    IxiNumber entry_amount = entry.Value.amount;
                    IxiNumber fiat_amount = entry_amount * Node.fiatPrice;

                    string username = SpixiLocalization._SL("wallet-unknown-recipient");
                    string useravatar = "img/spixiavatar.png";
                    if (friend != null)
                    {
                        username = friend.nickname;
                        useravatar = IxianHandler.localStorage.getAvatarPath(friend.walletAddress.ToString());
                    }

                    Utils.sendUiCommand(this, "addEntry", entry.Key.ToString(), username, useravatar, Utils.amountToHumanFormatString(entry_amount), Utils.amountToHumanFormatString(fiat_amount), time, type, confirmed);                    
                }
            }
            else
            {
                // this is a received payment
                type = "receive";
                amount = 0;

                foreach (var entry in ctransaction.toList)
                {
                    if (IxianHandler.getWalletStorage().isMyAddress(entry.Key))
                    {
                        amount += entry.Value.amount;
                    }
                }
                IxiNumber fiat_amount = amount * Node.fiatPrice;

                Utils.sendUiCommand(this, "setReceivedMode");
                Address sender_address = ctransaction.pubKey;
                Friend friend = FriendList.getFriend(sender_address);

                string username = SpixiLocalization._SL("wallet-unknown-sender");
                string useravatar = "img/spixiavatar.png";

                if (friend != null)
                {
                    username = friend.nickname;
                    useravatar = IxianHandler.localStorage.getAvatarPath(friend.walletAddress.ToString());
                }

                Utils.sendUiCommand(this, "addEntry", sender_address.ToString(), username, useravatar, Utils.amountToHumanFormatString(amount), Utils.amountToHumanFormatString(fiat_amount), time, type, confirmed);               

            }

            Utils.sendUiCommand(this, "setData", amount.ToString(), ctransaction.fee.ToString(),
                time, transaction.getTxIdString(), confirmed);
            return;
        }

        public override void updateScreen()
        {
            //checkTransaction();
        }

        private void onDismiss()
        {
            if (!viewOnly)
            {
                Navigation.RemovePage(Navigation.NavigationStack[Navigation.NavigationStack.Count - 2]);
                Navigation.RemovePage(Navigation.NavigationStack[Navigation.NavigationStack.Count - 2]);
            }
            Navigation.PopAsync(Config.defaultXamarinAnimations);
        }

        protected override bool OnBackButtonPressed()
        {
            onDismiss();
            return true;
        }
    }
}