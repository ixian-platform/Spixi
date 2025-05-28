using IXICore.Meta;
using Spixi;
using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using SPIXI.Storage;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SettingsPage : SpixiContentPage
    {
        string selectedLanguage = null;
        ThemeAppearance selectedAppearance = ThemeAppearance.automatic;

        bool lockEnabled = false;

        public SettingsPage()
        {
            InitializeComponent();

            NavigationPage.SetHasNavigationBar(this, false);

            loadPage(webView, "settings.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            Utils.sendUiCommand(this, "setNickname", Node.localStorage.nickname);
            selectedAppearance = ThemeManager.getActiveAppearance();
            int activeAppearanceIdx = (int)selectedAppearance;
            Utils.sendUiCommand(this, "setAppearance", activeAppearanceIdx.ToString());

            if (Preferences.Default.ContainsKey("lockenabled"))
            {
                lockEnabled = (bool)Preferences.Default.Get("lockenabled",false);
            }
            Utils.sendUiCommand(this, "setLockEnabled", lockEnabled.ToString());


            var filePath = Node.localStorage.getOwnAvatarPath();
            if (filePath.Equals("img/spixiavatar.png", StringComparison.Ordinal))
            {
                // No custom avatar has been chosen
            }
            else
            {
                // A custom avatar has been chosen previously
                Utils.sendUiCommand(this, "showRemoveAvatar", "1");
            }

            Utils.sendUiCommand(this, "loadAvatar", filePath);

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
                OnBackButtonPressed();
            }
            else if (current_url.Equals("ixian:error", StringComparison.Ordinal))
            {
                displaySpixiAlert(SpixiLocalization._SL("settings-emptynick-title"), SpixiLocalization._SL("settings-emptynick-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
            else if (current_url.Equals("ixian:delete", StringComparison.Ordinal))
            {
                var lockPage = new LockPage(true);
                lockPage.authSucceeded += onDeleteWallet;
                Navigation.PushModalAsync(lockPage);
            }
            else if (current_url.Equals("ixian:deletea", StringComparison.Ordinal))
            {
                var lockPage = new LockPage(true);
                lockPage.authSucceeded += onDeleteAccount;
                Navigation.PushModalAsync(lockPage);
            }
            else if (current_url.Equals("ixian:deleteh", StringComparison.Ordinal))
            {
                onDeleteHistory();
            }
            else if (current_url.Equals("ixian:deleted", StringComparison.Ordinal))
            {
                onDeleteDownloads();
            }
            else if (current_url.Equals("ixian:backup", StringComparison.Ordinal))
            {
                Navigation.PushAsync(new BackupPage(), Config.defaultXamarinAnimations);
            }
            else if (current_url.Contains("ixian:save:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:save:" }, StringSplitOptions.None);
                string nick = split[1];
                onSaveSettings(nick);
            }
            else if (current_url.Equals("ixian:avatar", StringComparison.Ordinal))
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                onChangeAvatarAsync(sender, e);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            else if (current_url.Equals("ixian:remove", StringComparison.Ordinal))
            {
                onRemoveAvatar();
            }
            else if (current_url.StartsWith("ixian:language:", StringComparison.Ordinal))
            {
                string lang = current_url.Substring("ixian:language:".Length);
                if (SpixiLocalization.loadLanguage(lang))
                {
                    selectedLanguage = lang;
                    loadPage(webView, "settings.html");
                }
                else
                {
                    selectedLanguage = null;
                }
            }
            else if (current_url.StartsWith("ixian:lock:", StringComparison.Ordinal))
            {
                string status = current_url.Substring("ixian:lock:".Length);
                if (status.Equals("on", StringComparison.Ordinal))
                {
                    // Turn on lock
                    lockEnabled = true;
                }
                else
                {
                    // Turn off lock
                    // Show authentication screen
                    var lockPage = new LockPage(true);
                    lockPage.authSucceeded += HandleAuthSucceeded;
                    Navigation.PushModalAsync(lockPage);


                }
            }
            else if (current_url.StartsWith("ixian:appearance:", StringComparison.Ordinal))
            {
                string appearanceString = current_url.Substring("ixian:appearance:".Length);
                selectedAppearance = (ThemeAppearance)Convert.ToInt32(appearanceString);
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;
        }



        public void onSaveSettings(string nick)
        {
            if (selectedLanguage != null)
            {
                Preferences.Default.Set("language", selectedLanguage);
            }
            else
            {
                resetLanguage();
            }

            Preferences.Default.Set("lockenabled", lockEnabled);

            if (Node.localStorage.nickname != nick)
            {
                Node.localStorage.nickname = nick;
                FriendList.broadcastNicknameChange();
            }
            Node.localStorage.writeAccountFile();
            Node.changedSettings = true;
            applyAvatar();

            if (ThemeManager.changeAppearance(selectedAppearance))
            {
                UIHelpers.reloadAllPages();
            }

            // Pop the current page from the stack
            Navigation.PopAsync(Config.defaultXamarinAnimations);
        }

        private void resetLanguage()
        {
            string lang = "en-us";
            if (Preferences.Default.ContainsKey("language"))
            {
                lang = Preferences.Default.Get("language", "") as string;
            }
            SpixiLocalization.loadLanguage(lang);
        }

        private void HandleAuthSucceeded(object sender, SPIXI.EventArgs<bool> e)
        {
            bool succeeded = e.Value;

            if(succeeded)
            {
                lockEnabled = false;
                Utils.sendUiCommand(this, "setLockEnabled", lockEnabled.ToString());
            }

        }

        public void onDeleteWallet(object sender, EventArgs e)
        {
            if (IxianHandler.getWalletStorage().deleteWallet())
            {
                // Also delete the account
                onDeleteAccount(sender, e);

                // Stop network activity
                Node.stop();

                Preferences.Default.Remove("onboardingComplete");
                Preferences.Default.Remove("lockenabled");
                Preferences.Default.Remove("waletpass");

                SpixiLocalization.addCustomString("OnboardingComplete", "false");

                Node.localStorage.deleteTransactionCacheFile();
                TransactionCache.clearAllTransactions();
                Node.tiv.clearCache();

                // Remove the settings page
                Navigation.PopToRootAsync();

                // Show the launch page
                Navigation.PushAsync(new LaunchPage(), Config.defaultXamarinAnimations);

                // Todo: also remove the parent page without causing memory leaks
            }
            else
            {
                displaySpixiAlert(SpixiLocalization._SL("settings-deletew-error-title"), SpixiLocalization._SL("settings-deletew-error-text"), SpixiLocalization._SL("global-dialog-ok"));
            }

        }

        public void onDeleteAccount(object sender, EventArgs e)
        {
            Node.localStorage.deleteAllAvatars();
            Node.localStorage.deleteAccountFile();
            Node.localStorage.deleteAllDownloads();
            StreamProcessor.deletePendingMessages();
            FriendList.deleteEntireHistory();
            FriendList.deleteAccounts();
            FriendList.clear();

            onLoad();

            displaySpixiAlert(SpixiLocalization._SL("settings-deleteda-title"), SpixiLocalization._SL("settings-deleteda-text"), SpixiLocalization._SL("global-dialog-ok"));
        }

        public void onDeleteHistory()
        {
            FriendList.deleteEntireHistory();
            displaySpixiAlert(SpixiLocalization._SL("settings-deletedh-title"), SpixiLocalization._SL("settings-deletedh-text"), SpixiLocalization._SL("global-dialog-ok"));
        }

        public void onDeleteDownloads()
        {
            try
            {
                TransferManager.resetIncomingTransfers();
                int file_count = 0;
                foreach (var file in Directory.EnumerateFiles(Path.Combine(Config.spixiUserFolder, "Downloads")))
                {
                    File.Delete(file);
                    file_count++;
                }
                displaySpixiAlert(SpixiLocalization._SL("settings-deletedd-title"), string.Format(SpixiLocalization._SL("settings-deletedd-text"), file_count), SpixiLocalization._SL("global-dialog-ok"));
            }
            catch (Exception e)
            {
                Logging.error("Exception while deleting downloads: " + e);
                displaySpixiAlert(SpixiLocalization._SL("settings-deleted-error-title"), SpixiLocalization._SL("settings-deleted-error-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
        }

        public async Task onChangeAvatarAsync(object sender, EventArgs e)
        {
            SpixiImageData spixi_img_data = await SFilePicker.PickImageAsync();
            if (spixi_img_data == null)
                return;

            Stream stream = spixi_img_data.stream;
            if (stream == null)
                return;          

            var file_path = Path.Combine(Node.localStorage.avatarsPath, "avatar-tmp.jpg");
            try
            {
                byte[] image_bytes = null;
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
            Node.changedSettings = true;
        }

        // Applies the avatar image once the user chooses to Save changes
        public void applyAvatar()
        {
            var file_path = Node.localStorage.getOwnAvatarPath(false);
            var source_file_path = Path.Combine(Node.localStorage.avatarsPath, "avatar-tmp.jpg");

            // Check if the source file exists before proceeding
            if (!File.Exists(source_file_path))
            {
                return;
            }

            // Remove the avatar image first
            if (File.Exists(file_path))
            {
                File.Delete(file_path);
            }

            File.Copy(source_file_path, file_path);

            // Delete the temporary avatar image if the copy was successfull
            if (File.Exists(file_path))
            {
                File.Delete(source_file_path);
            }

            FriendList.broadcastAvatarChange();
        }

        public void onRemoveAvatar()
        {
            if (Node.localStorage.deleteOwnAvatar())
            {
                Utils.sendUiCommand(this, "showRemoveAvatar", "0");
                Utils.sendUiCommand(this, "loadAvatar", Node.localStorage.getOwnAvatarPath());
                Node.changedSettings = true;
            }
        }

        protected override bool OnBackButtonPressed()
        {
            resetLanguage();

            Navigation.PopAsync(Config.defaultXamarinAnimations);

            return true;
        }
    }
}