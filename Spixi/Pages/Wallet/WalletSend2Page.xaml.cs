using IXICore;
using IXICore.Meta;
using IXICore.Streaming;
using IXICore.Utils;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using SPIXI.Lang;
using SPIXI.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using static IXICore.Transaction;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class WalletSend2Page : SpixiContentPage
    {
        SortedDictionary<Address, ToEntry> to_list = new SortedDictionary<Address, ToEntry>(new AddressComparer());
        IxiNumber totalAmount = 0;
        Transaction? transaction = null;

        ExtendedAddress _address;
        public WalletSend2Page(ExtendedAddress address)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            _address = address;

            loadPage(webView, "wallet_send_2.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            // Calculate tx fee
            Address from = IxianHandler.getWalletStorage().getPrimaryAddress();
            string nickname = _address.RoutingAddress.ToString();

            Friend? friend = FriendList.getFriend(_address.RoutingAddress);
            if (friend != null)
                nickname = friend.nickname;
            Utils.sendUiCommand(this, "setRecipient", nickname, _address.ToString(), "img/spixiavatar.png");
            Utils.sendUiCommand(this, "setBalance", Node.getAvailableBalance().ToString(), Node.fiatPrice.ToString());

            var fee = Node.calculateTransactionFee(from, _address, ConsensusConfig.transactionDustLimit);
            Utils.sendUiCommand(this, "setFees", fee.ToString());
        }

        private void onNavigating(object sender, WebNavigatingEventArgs e)
        {
            string current_url = HttpUtility.UrlDecode(e.Url);
            e.Cancel = true;

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
                OnBackButtonPressed();
            }
            else if (current_url.Contains("ixian:send:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:send:" }, StringSplitOptions.None);
                // Extract the fee

                // Send the payment
                sendPayment(split[1]);
            }
            else if (current_url.Contains("ixian:getMaxAmount"))
            {
                var fee = Node.calculateTransactionFeeFromAvailableBalance(IxianHandler.primaryWalletAddress, _address);
                Utils.sendUiCommand(this, "setMaxAmount", (Node.getAvailableBalance() - fee).ToString());
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        private void sendPayment(string amount)
        {
            IxiNumber _amount = amount;

            if (_amount < (long)0)
            {
                displaySpixiAlert(SpixiLocalization._SL("wallet-error-amount-title"), SpixiLocalization._SL("wallet-error-amount-text"), SpixiLocalization._SL("global-dialog-ok"));
                return;
            }

            IxiNumber fee = ConsensusConfig.forceTransactionPrice;
            Address from = IxianHandler.getWalletStorage().getPrimaryAddress();
            Address pubKey = new Address(IxianHandler.getWalletStorage().getPrimaryPublicKey());

            var txPrep = Node.prepareTransactionFrom(from, _address, amount);
            transaction = txPrep.transaction;
            if (transaction == null)
            {
                displaySpixiAlert(SpixiLocalization._SL("wallet-error-amount-title"), SpixiLocalization._SL("wallet-error-amount-text"), SpixiLocalization._SL("global-dialog-ok"));
                return;
            }
            var relayNodeAddresses = txPrep.relayNodeAddresses;
            totalAmount = transaction.amount + transaction.fee;

            Logging.info("Broadcasting tx {0}", transaction.id);
            IxianHandler.addTransaction(transaction, relayNodeAddresses, txPrep.extendedAddresses, null, true);

            // Show the payment details
            Navigation.PushAsync(new WalletSentPage(transaction, false), Config.defaultXamarinAnimations);
        }

        protected override bool OnBackButtonPressed()
        {
            popPageAsync();

            return true;
        }
    }
}