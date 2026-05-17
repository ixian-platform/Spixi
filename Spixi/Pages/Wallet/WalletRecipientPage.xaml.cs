using IXICore;
using IXICore.Meta;
using IXICore.Streaming;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Spixi;
using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace SPIXI
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class WalletRecipientPage : SpixiContentPage
	{
        public event EventHandler<SPIXI.EventArgs<(List<ExtendedAddress>, string?, bool)>> pickSucceeded;
        private bool multiContactMode = false;
        private bool payment = true;
        public static string temporaryImagePath = "avatar-tmp.jpg";
        public WalletRecipientPage (bool multiContactMode = false, bool payment = true)
		{
            temporaryImagePath = Path.Combine(IxianHandler.localStorage.avatarsPath, "avatar-tmp.jpg");

            InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);

            this.multiContactMode = multiContactMode;
            this.payment = payment;

            loadPage(webView, "wallet_recipient.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            loadContacts();
            if (File.Exists(temporaryImagePath))
            {
                File.Delete(temporaryImagePath);
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
            else if (current_url.Equals("ixian:newcontact", StringComparison.Ordinal))
            {
                var recipientPage = new ContactNewPage();
                recipientPage.pickSucceeded += HandleNewContactSucceeded;
                Navigation.PushAsync(recipientPage, Config.defaultXamarinAnimations);
            }
            else if (current_url.Contains("ixian:select:"))
            {
                string name = current_url.Substring("ixian:select:".Length, current_url.IndexOf(":", "ixian:select:".Length) - "ixian:select:".Length);
                bool blindMode = name.First() == '1' ? true : false;
                name = name.Substring(1);
                string[] split = current_url.Split(new string[] { name + ":|" }, StringSplitOptions.None);
                List<ExtendedAddress> addresses = new();
                foreach (var address in split[1].Split('|'))
                {
                    addresses.Add(new ExtendedAddress(address));
                }

                onPickSucceeded(addresses, name, blindMode);
            }
            else if (current_url.Equals("ixian:avatar", StringComparison.Ordinal))
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                onChangeAvatarAsync(sender, e);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        public async Task onChangeAvatarAsync(object sender, EventArgs e)
        {
            SpixiImageData spixi_img_data = await SFilePicker.PickImageAsync();
            if (spixi_img_data == null)
                return;

            Stream stream = spixi_img_data.stream;
            if (stream == null)
                return;

            var file_path = temporaryImagePath;
            try
            {
                byte[]? image_bytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    stream.Close();
                    image_bytes = SFilePicker.ResizeImage(ms.ToArray(), 960, 960, 80);
                    if (image_bytes == null)
                    {
                        return;
                    }
                }

                FileStream fs = new FileStream(file_path, FileMode.OpenOrCreate, FileAccess.Write);
                fs.Write(image_bytes, 0, image_bytes.Length);
                fs.Close();
            }
            catch (Exception ex)
            {
                await displaySpixiAlert(SpixiLocalization._SL("intro-new-avatarerror-title"), ex.ToString(), SpixiLocalization._SL("global-dialog-ok"));
                return;
            }

            Utils.sendUiCommand(this, "loadAvatar", file_path);
        }

        private void HandleNewContactSucceeded(object? sender, SPIXI.EventArgs<string> e)
        {
            ExtendedAddress id = new ExtendedAddress(e.Value);
            onPickSucceeded(new() { id });
            popPageAsync();
        }

        private void onPickSucceeded(List<ExtendedAddress> addresses, string? groupName = null, bool blindMode = false)
        {
            if (pickSucceeded != null)
            {
                pickSucceeded(this, new((addresses, groupName, blindMode)));
            }
        }

        public void loadContacts()
        {
            if(FriendList.friends.Count == 0)
            {
                Utils.sendUiCommand(this, "noContacts");
                return;
            }

            if (multiContactMode)
            {
                Utils.sendUiCommand(this, "setMultiContactMode");
            }

            Utils.sendUiCommand(this, "clearContacts");

            foreach (Friend friend in FriendList.friends)
            {
                string? avatar_path = IxianHandler.localStorage.getAvatarPath(friend.walletAddress.ToString());
                if (friend.type == FriendType.Payment
                    || friend.type == FriendType.Temporary)
                {
                    if (!HomePage.Instance().devMode)
                    {
                        continue;
                    }
                }
                else if (friend.type == FriendType.Group)
                {
                    if (payment)
                    {
                        continue;
                    }
                }

                if (avatar_path == null)
                {
                    if (friend.type == FriendType.Group)
                    {
                        avatar_path = "img/spixi-group-avatar.png";
                    }
                    else
                    {
                        avatar_path = "img/spixiavatar.png";
                    }
                }
                                
                string str_online = "false";
                if (friend.online)
                    str_online = "true";

                int type = 0;
                if (friend.bot
                    || !friend.hasCapabilities(StreamCapabilities.GroupCapabilites))
                {
                    type = 2;
                }
                else if (friend.type == FriendType.Group)
                {
                    type = 1;
                }

                Utils.sendUiCommand(this, "addContact", friend.walletAddress.ToString(), friend.nickname, avatar_path, str_online, type.ToString());
            }
        }

        protected override bool OnBackButtonPressed()
        {
            popPageAsync();

            return true;
        }

        protected override void OnAppearing()
        {
            loadContacts();
        }

    }
}
