using IXICore;
using IXICore.Meta;
using IXICore.Streaming;
using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
//using ZXing.Net.Mobile.Forms;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class WalletSendPage : SpixiContentPage
	{
        private Address recipient = null;

        public WalletSendPage ()
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "wallet_send.html");
        }

        public WalletSendPage(Address wal)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            recipient = wal;

            loadPage(webView, "wallet_send.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            Utils.sendUiCommand(this, "setBalance", Node.getAvailableBalance().ToString());

            // If we have a pre-set recipient, fill out the recipient wallet address and nickname
            if (recipient != null)
            {
                string nickname = recipient.ToString();

                Friend friend = FriendList.getFriend(recipient);
                if (friend != null)
                    nickname = friend.nickname;

                Utils.sendUiCommand(this, "addRecipient", nickname, 
                    recipient.ToString());
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
            else if (current_url.Equals("ixian:quickscan", StringComparison.Ordinal))
            {
                quickScan();
            }
            else if (current_url.Equals("ixian:error", StringComparison.Ordinal))
            {
                displaySpixiAlert(SpixiLocalization._SL("global-invalid-address-title"), SpixiLocalization._SL("global-invalid-address-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
            else if (current_url.Equals("ixian:error2", StringComparison.Ordinal))
            {
                displaySpixiAlert(SpixiLocalization._SL("wallet-error-amount-title"), SpixiLocalization._SL("wallet-error-amount-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
            else if (current_url.Contains("ixian:send:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:send:" }, StringSplitOptions.None);
                string address = split[1];

                byte[] addressBytes = Base58Check.Base58CheckEncoding.DecodePlain(address);
                if (!Address.validateAddress(addressBytes)
                    || !Address.validateChecksum(addressBytes))
                {
                    e.Cancel = true;
                    //displaySpixiAlert(SpixiLocalization._SL("global-invalid-address-title"), SpixiLocalization._SL("global-invalid-address-text"), SpixiLocalization._SL("global-dialog-ok"));
                    return;
                }

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(200); // WinUI Crash fix
                    await Navigation.PushAsync(new WalletSend2Page(address), Config.defaultXamarinAnimations);
                });

                /*             // TODO re-enable in a future update  
                 *             // Extract all addresses and amounts
                               string[] addresses_split = split[1].Split(new string[] { "|" }, StringSplitOptions.None);

                               // Go through each entry
                               foreach (string address_and_amount in addresses_split)
                               {
                                   if (address_and_amount.Length < 1)
                                       continue;

                                   // Extract the address and amount
                                   string[] asplit = address_and_amount.Split(new string[] { ":" }, StringSplitOptions.None);
                                   if (asplit.Count() < 2)
                                       continue;

                                   string address = asplit[0];
                                   string amount = asplit[1];

                                   if (Address.validateChecksum(Base58Check.Base58CheckEncoding.DecodePlain(address)) == false)
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
                               }
                               Navigation.PushAsync(new WalletSend2Page(addresses_split), Config.defaultXamarinAnimations);
                */

            }
            else if (current_url.Contains("ixian:getMaxAmount"))
            {
                if (Node.getAvailableBalance() > ConsensusConfig.forceTransactionPrice * 6)
                {
                    // TODO needs to be improved and pubKey length needs to be taken into account
                    Utils.sendUiCommand(this, "setAmount", (Node.getAvailableBalance() - (ConsensusConfig.forceTransactionPrice * 6)).ToString());
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

        private void HandleScanSucceeded(object sender, SPIXI.EventArgs<string> e)
        {
            string wallets_to_add = e.Value;

            processQRResult(wallets_to_add);

        }
        public async void quickScan()
        {
            var scanPage = new ScanPage();
            scanPage.scanSucceeded += HandleScanSucceeded;
            await Navigation.PushAsync(scanPage, Config.defaultXamarinAnimations);
        }
        public void processQRResult(string result)
        {
            if (result.Contains(":ixi"))
            {
                string[] split = result.Split(new string[] { ":ixi" }, StringSplitOptions.None);
                if (split.Count() < 1)
                    return;
                string wallet_to_send = split[0];
                string nickname = wallet_to_send;

                Friend friend = FriendList.getFriend(new Address(wallet_to_send));
                if (friend != null)
                    nickname = friend.nickname;
                Utils.sendUiCommand(this, "addRecipient", nickname, wallet_to_send);
                return;
            }
            else if (result.Contains(":send"))
            {
                // Check for transaction request
                string[] split = result.Split(new string[] { ":send:" }, StringSplitOptions.None);
                if (split.Count() > 1)
                {
                    string wallet_to_send = split[0];
                    string nickname = wallet_to_send;

                    Friend friend = FriendList.getFriend(new Address(wallet_to_send));
                    if (friend != null)
                        nickname = friend.nickname;
                    Utils.sendUiCommand(this, "addRecipient", nickname, wallet_to_send);
                    return;
                }
            }
            else
            {
                // Handle direct addresses
                string wallet_to_send = result;
                if (Address.validateChecksum(Base58Check.Base58CheckEncoding.DecodePlain(wallet_to_send)))
                {
                    string nickname = wallet_to_send;

                    Friend friend = FriendList.getFriend(new Address(wallet_to_send));
                    if (friend != null)
                        nickname = friend.nickname;

                    Utils.sendUiCommand(this, "addRecipient", nickname, wallet_to_send);
                    return;
                }
            }
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