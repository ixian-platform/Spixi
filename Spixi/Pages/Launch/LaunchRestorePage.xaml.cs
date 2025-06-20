using IXICore;
using IXICore.Meta;
using Spixi;
using SPIXI.Interfaces;
using SPIXI.Lang;
using SPIXI.Meta;
using System.IO.Compression;
using System.Text;
using System.Web;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class LaunchRestorePage : SpixiContentPage
	{
		public LaunchRestorePage ()
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);
            loadPage(webView, "intro_restore.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {

        }

        private void onNavigating(object sender, WebNavigatingEventArgs e)
        {
            string current_url = HttpUtility.UrlDecode(e.Url);

            if (current_url.Equals("ixian:back", StringComparison.Ordinal))
            {
                popPageAsync();
            }
            else if (current_url.Equals("ixian:selectfile", StringComparison.Ordinal))
            {
                onSelectFile();
            }
            else if (current_url.Contains("ixian:restore:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:restore:" }, StringSplitOptions.None);
                if (split.Count() < 1)
                {
                    e.Cancel = true;
                    Utils.sendUiCommand(this, "removeLoadingOverlay");
                    displaySpixiAlert(SpixiLocalization._SL("intro-restore-file-invalidpassword-title"), SpixiLocalization._SL("intro-restore-file-invalidpassword-text"), SpixiLocalization._SL("global-dialog-ok"));
                    return;
                }

                string password = split[1]; // Todo: secure this
                if(!onRestore(password))
                {
                    e.Cancel = true;
                    Utils.sendUiCommand(this, "removeLoadingOverlay");
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

        // Shows a file picker to select the wallet file
        private async void onSelectFile()
        {
            byte[] _data = null;
            try
            {
                SpixiImageData fileData = await SFilePicker.PickFileAsync();
                if (fileData == null)
                    return; // User canceled file picking

                var stream = fileData.stream;
                _data = new byte[stream.Length];
                stream.Read(_data, 0, (int)stream.Length);
            }
            catch (Exception ex)
            {
                Logging.error("Exception choosing file: " + ex.ToString());
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                displaySpixiAlert(SpixiLocalization._SL("intro-restore-file-error-title"), SpixiLocalization._SL("intro-restore-file-selecterror-text"), SpixiLocalization._SL("global-dialog-ok"));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                return;
            }

            if (_data == null)
            {
                await displaySpixiAlert(SpixiLocalization._SL("intro-restore-file-error-title"), SpixiLocalization._SL("intro-restore-file-readerror-text"), SpixiLocalization._SL("global-dialog-ok"));
                return;
            }

            string docpath = Config.spixiUserFolder;
            string filepath = Path.Combine(docpath, Config.walletFile + ".tmp");
            try
            {
                File.WriteAllBytes(filepath, _data);
            }
            catch (Exception ex)
            {
                Logging.error("Exception caught in process: {0}", ex);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                displaySpixiAlert(SpixiLocalization._SL("intro-restore-file-error-title"), SpixiLocalization._SL("intro-restore-file-writeerror-text"), SpixiLocalization._SL("global-dialog-ok"));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                return;
            }

            Utils.sendUiCommand(this, "enableRestore");
        }

        // Attempt to restore the wallet
        private bool onRestore(string pass)
        {
            Preferences.Default.Set("walletpass", pass);

            string source_path = Path.Combine(Config.spixiUserFolder, Config.walletFile) + ".tmp";
            if(!File.Exists(source_path))
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                displaySpixiAlert(SpixiLocalization._SL("intro-restore-file-error-title"), SpixiLocalization._SL("intro-restore-file-selecterror-text"), SpixiLocalization._SL("global-dialog-ok"));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                return false;
            }
            if (restoreAccountFile(source_path, pass))
            {
                return true;
            }
            restoreWalletFile(source_path, pass);
            return true;
        }

        private bool restoreAccountFile(string source_path, string pass)
        {
            // TODO add file header
            string tmpDirectory = Path.Combine(Config.spixiUserFolder, "tmp_zip");
            try
            {
                if(Directory.Exists(tmpDirectory))
                {
                    Directory.Delete(tmpDirectory, true);
                }
                Directory.CreateDirectory(tmpDirectory);
                byte[] decrypted = CryptoManager.lib.decryptWithPassword(File.ReadAllBytes(source_path), pass, true);
                byte[] header = UTF8Encoding.UTF8.GetBytes("SPIXIACCB1");
                for(int i = 0; i < header.Length; i++)
                {
                    if(decrypted[i] != header[i])
                    {
                        Directory.Delete(tmpDirectory, true);
                        return false;
                    }
                }
                byte[] zipFileBytes = decrypted.Skip(header.Length).ToArray();
                File.WriteAllBytes(source_path, zipFileBytes);
                ZipFile.ExtractToDirectory(source_path, tmpDirectory);
                string tmpWalletFile = Path.Combine(tmpDirectory, Config.walletFile);
                WalletStorage ws = new WalletStorage(tmpWalletFile);
                if (!ws.verifyWallet(tmpWalletFile, pass))
                {
                    Directory.Delete(tmpDirectory, true);
                    displaySpixiAlert(SpixiLocalization._SL("intro-restore-file-invalidpassword-title"), SpixiLocalization._SL("intro-restore-file-invalidpassword-text"), SpixiLocalization._SL("global-dialog-ok"));
                    // Remove overlay
                    Utils.sendUiCommand(this, "removeLoadingOverlay");
                    return false;
                }
                Directory.Delete(Path.Combine(Config.spixiUserFolder, "Acc"), true);
                Directory.Move(Path.Combine(tmpDirectory, "Acc"), Path.Combine(Config.spixiUserFolder, "Acc"));
//                Directory.Delete(Path.Combine(Config.spixiUserFolder, "Chats"), true);
//                Directory.Move(Path.Combine(tmpDirectory, "Chats"), Path.Combine(Config.spixiUserFolder, "Chats"));
                if (File.Exists(Path.Combine(tmpDirectory, "account.ixi")))
                {
                    File.Move(Path.Combine(tmpDirectory, "account.ixi"), Path.Combine(Config.spixiUserFolder, "account.ixi"));
                }
                if (File.Exists(Path.Combine(tmpDirectory, "avatar.jpg")))
                {
                    File.Move(Path.Combine(tmpDirectory, "avatar.jpg"), Path.Combine(Config.spixiUserFolder, "avatar.jpg"));
                }
                if (File.Exists(Path.Combine(tmpDirectory, "txcache.ixi")))
                {
                    File.Move(Path.Combine(tmpDirectory, "txcache.ixi"), Path.Combine(Config.spixiUserFolder, "txcache.ixi"));
                }
                File.Move(Path.Combine(tmpDirectory, "wallet.ixi"), Path.Combine(Config.spixiUserFolder, "wallet.ixi"));

                Node.loadWallet();
                Directory.Delete(tmpDirectory, true);
                File.Delete(source_path);
                Navigation.PushAsync(HomePage.Instance(true), Config.defaultXamarinAnimations);
                removePage(this);
                return true;
            }catch(Exception e)
            {
                Logging.warn("Exception occured while trying to restore account file: " + e);
                Directory.Delete(tmpDirectory, true);
            }
            return false;
        }

        private bool restoreWalletFile(string source_path, string pass)
        {
            // TODO add file header
            string target_filepath = Path.Combine(Config.spixiUserFolder, Config.walletFile);
            WalletStorage ws = new WalletStorage(source_path);
            if (!ws.verifyWallet(source_path, pass))
            {
                displaySpixiAlert(SpixiLocalization._SL("intro-restore-file-invalidpassword-title"), SpixiLocalization._SL("intro-restore-file-invalidpassword-text"), SpixiLocalization._SL("global-dialog-ok"));
                // Remove overlay
                Utils.sendUiCommand(this, "removeLoadingOverlay");
                return false;
            }
            else
            {
                File.Move(source_path, target_filepath);
                Node.loadWallet();
            }
            Navigation.PushAsync(HomePage.Instance(true), Config.defaultXamarinAnimations);
            removePage(this);
            return true;
        }

        protected override bool OnBackButtonPressed()
        {
            popPageAsync();

            return true;
        }
    }
}