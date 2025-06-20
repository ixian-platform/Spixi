using IXICore;
using IXICore.Meta;
using Spixi;
using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class BackupPage : SpixiContentPage
	{
		public BackupPage ()
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);
            loadPage(webView, "settings_backup.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
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
                popPageAsync();
            }
            else if (current_url.Equals("ixian:error", StringComparison.Ordinal))
            {
                displaySpixiAlert(SpixiLocalization._SL("settings-backup-invalidpassword-title"), SpixiLocalization._SL("settings-backup-invalidpassword-text"), SpixiLocalization._SL("global-dialog-ok"));
            }
            else if (current_url.Equals("ixian:backupAccount", StringComparison.Ordinal))
            {
                onBackupAccount();
            }
            else if (current_url.Equals("ixian:backupWallet", StringComparison.Ordinal))
            {
                onBackupWallet();
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        private async void onBackupWallet()
        {
            try
            {
                // TODO add file header
                string docpath = Config.spixiUserFolder;
                string filepath = Path.Combine(docpath, Config.walletFile);
                await SFileOperations.share(filepath, "Backup Spixi Wallet");
            }
            catch (Exception ex)
            {
                Logging.error("Exception backing up wallet: " + ex.ToString());
            }
        }

        private async void onBackupAccount()
        {
            try
            {
                // TODO add file header
                string backup_file_name = Path.Combine(Config.spixiUserFolder, "spixi.account.backup.ixi");
                if (File.Exists(backup_file_name))
                {
                    File.Delete(backup_file_name);
                }

                using (ZipArchive archive = ZipFile.Open(backup_file_name, ZipArchiveMode.Create))
                {
                    string root_path = Path.Combine(Config.spixiUserFolder, "Acc");
                    var directories = Directory.EnumerateDirectories(root_path);
                    foreach (var dir in directories)
                    {
                        var files = Directory.EnumerateFiles(dir);
                        foreach (var file in files)
                        {
                            archive.CreateEntryFromFile(file, Path.Combine("Acc", file.Substring(file.IndexOf(root_path) + root_path.Length + 1)));
                        }
                    }
                    if (File.Exists(Path.Combine(Config.spixiUserFolder, "account.ixi")))
                    {
                        archive.CreateEntryFromFile(Path.Combine(Config.spixiUserFolder, "account.ixi"), "account.ixi");
                    }
                    if (File.Exists(Path.Combine(Config.spixiUserFolder, "avatar.jpg")))
                    {
                        archive.CreateEntryFromFile(Path.Combine(Config.spixiUserFolder, "avatar.jpg"), "avatar.jpg");
                    }
                    if (File.Exists(Path.Combine(Config.spixiUserFolder, "txcache.ixi")))
                    {
                        archive.CreateEntryFromFile(Path.Combine(Config.spixiUserFolder, "txcache.ixi"), "txcache.ixi");
                    }
                    archive.CreateEntryFromFile(Path.Combine(Config.spixiUserFolder, Config.walletFile), "wallet.ixi");
                }

                string password = Preferences.Default.Get("walletpass","").ToString();
                byte[] backup_file_bytes = File.ReadAllBytes(backup_file_name);
                byte[] header = UTF8Encoding.UTF8.GetBytes("SPIXIACCB1");
                byte[] bytes_to_encrypt = new byte[header.Length + backup_file_bytes.Length];
                Array.Copy(header, bytes_to_encrypt, header.Length);
                Array.Copy(backup_file_bytes, 0, bytes_to_encrypt, header.Length, backup_file_bytes.Length);

                byte[] encrypted_backup = CryptoManager.lib.encryptWithPassword(bytes_to_encrypt, password, true);
                File.Delete(backup_file_name);
                File.WriteAllBytes(backup_file_name, encrypted_backup);
                await SFileOperations.share(backup_file_name, "Share Spixi Account Backup File");
            }
            catch (Exception ex)
            {
                Logging.error("Exception backing up account: " + ex.ToString());
            }
        }

        protected override bool OnBackButtonPressed()
        {
            popPageAsync();

            return true;
        }
    }
}