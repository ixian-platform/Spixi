using IXICore;
using IXICore.Meta;
using IXICore.Streaming;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using SPIXI.Lang;
using SPIXI.Meta;
using System;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class ContactDetails : SpixiContentPage
	{
        private Friend friend = null;
        private bool customChatBtn = false;

		public ContactDetails (Friend lfriend, bool customChatButton = false)
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);

            friend = lfriend;
            customChatBtn = customChatButton;

            loadPage(webView, "contact_details.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            Utils.sendUiCommand(this, "setAddress", friend.walletAddress.ToString());

            updateScreen();
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
            else if (current_url.Equals("ixian:remove", StringComparison.Ordinal))
            {
                onRemove();

                popToRootAsync();
                HomePage.Instance().removeDetailContent();
            }
            else if (current_url.Equals("ixian:removehistory", StringComparison.Ordinal))
            {
                onRemoveHistory();
            }
            else if (current_url.Equals("ixian:request", StringComparison.Ordinal))
            {
                Navigation.PushAsync(new WalletReceivePage(friend), Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:send", StringComparison.Ordinal))
            {
                Navigation.PushAsync(new WalletSendPage(new ExtendedAddress(friend.walletAddress, AddressPaymentFlag.OfflineTag, null)), Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:chat", StringComparison.Ordinal))
            {
                if (customChatBtn)
                {
                    popPageAsync();
                    e.Cancel = true;
                    return;
                }
                else
                {
                    Navigation.PushAsync(new SingleChatPage(friend), Config.defaultXamarinAnimations);
                }
            }
            else if (current_url.Contains("ixian:txdetails:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:txdetails:" }, StringSplitOptions.None);
                byte[] id = Transaction.txIdLegacyToV8(split[1]);

                var activity = Node.activityStorage.getActivityById(id, null, true);
                if (activity == null)
                {
                    e.Cancel = true;
                    return;
                }

                Navigation.PushAsync(new WalletSentPage(activity.transaction), Config.defaultXamarinAnimations);
            }else if(current_url.Contains("ixian:userdefinednick:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:userdefinednick:" }, StringSplitOptions.None);
                string nick = split[1];
                friend.setUserDefinedNick(nick);
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        private void onRemove()
        {
            if (friend.bot && friend.metaData.botInfo != null)
            {
                friend.pendingDeletion = true;
                friend.save();
                UIHelpers.shouldRefreshContacts = true;
                CoreStreamProcessor.sendLeave(friend, null);
                displaySpixiAlert(SpixiLocalization._SL("contact-details-removedcontact-title"), SpixiLocalization._SL("contact-details-removedcontact-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
            else
            {
                if (FriendList.removeFriend(friend) == true)
                {
                    UIHelpers.shouldRefreshContacts = true;
                    displaySpixiAlert(SpixiLocalization._SL("contact-details-removedcontact-title"), SpixiLocalization._SL("contact-details-removedcontact-text"), SpixiLocalization._SL("global-dialog-ok"));
                }
            }
        }

        private void onRemoveHistory()
        {
            // Remove history file
            if(friend.deleteHistory())
            {
                displaySpixiAlert(SpixiLocalization._SL("contact-details-deletedhistory-title"), SpixiLocalization._SL("contact-details-deletedhistory-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
        }

        public void loadTransactions()
        {
            Utils.sendUiCommand(this, "clearRecentActivity");
            foreach (var activity in Node.activityStorage.getActivitiesByStatus(IXICore.Activity.ActivityStatus.Pending))
            {
                Transaction utransaction = activity.transaction;
                Address from_address = utransaction.pubKey;
                // Filter out unrelated transactions
                if (from_address.addressNoChecksum.SequenceEqual(friend.walletAddress.addressNoChecksum) == false)
                {
                    if (utransaction.toList.ContainsKey(friend.walletAddress) == false)
                        continue;
                }

                string tx_type = SpixiLocalization._SL("global-received");
                if (from_address.addressNoChecksum.SequenceEqual(IxianHandler.getWalletStorage().getPrimaryAddress().addressNoChecksum))
                {
                    tx_type = SpixiLocalization._SL("global-sent");
                }
                string time = Utils.unixTimeStampToString(Convert.ToDouble(activity.timestamp));
                Utils.sendUiCommand(this, "addPaymentActivity", utransaction.getTxIdString(), tx_type, time, utransaction.amount.ToString(), "false");
            }

            foreach (var activity in Node.activityStorage.getActivitiesBySeedHashAndType(IxianHandler.getWalletStorage().getSeedHash(), null))
            {
                Transaction transaction = Node.activityStorage.getActivityById(activity.id, null, true).transaction;

                Address from_address = transaction.pubKey;
                // Filter out unrelated transactions
                if (from_address.addressNoChecksum.SequenceEqual(friend.walletAddress.addressNoChecksum) == false)
                {
                    if (transaction.toList.ContainsKey(friend.walletAddress) == false)
                        continue;
                }

                string tx_type = SpixiLocalization._SL("global-received");
                if (from_address.addressNoChecksum.SequenceEqual(IxianHandler.getWalletStorage().getPrimaryAddress().addressNoChecksum))
                {
                    tx_type = SpixiLocalization._SL("global-sent");
                }
                string time = Utils.unixTimeStampToString(Convert.ToDouble(activity.timestamp));

                string confirmed = "true";
                if (activity.status != IXICore.Activity.ActivityStatus.Final)
                {
                    confirmed = "error";
                }

                Utils.sendUiCommand(this, "addPaymentActivity", transaction.getTxIdString(), tx_type, time, transaction.amount.ToString(), confirmed);
            }
        }

        // Executed every second
        public override void updateScreen()
        {
            Utils.sendUiCommand(this, "setNickname", friend.nickname);

            string avatar = IxianHandler.localStorage.getAvatarPath(friend.walletAddress.ToString(), false);
            if (avatar == null)
            {
                avatar = "";
            }

            Utils.sendUiCommand(this, "setAvatar", avatar);

            if (friend.online)
            {
                Utils.sendUiCommand(this, "showIndicator", "true");
            }
            else
            {
                Utils.sendUiCommand(this, "showIndicator", "false");
            }

            loadTransactions();
        }

        protected override bool OnBackButtonPressed()
        {
            popPageAsync();

            return true;
        }
    }
}