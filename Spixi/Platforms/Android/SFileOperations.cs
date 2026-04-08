using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net.Wifi;
using Android.OS;
using Android.Webkit;
using IXICore.Meta;
using Java.IO;
using Microsoft.Maui.Storage;
using SPIXI.Lang;
using File = Java.IO.File;

namespace Spixi
{
    public class SFileOperations
    {

        public static async Task share(string filepath, string title)
        {
            var action = await Microsoft.Maui.Controls.Application.Current!.MainPage!.DisplayActionSheet(
                            SpixiLocalization._SL("global-share-choose"),
                            SpixiLocalization._SL("global-dialog-cancel"),
                            null,
                            SpixiLocalization._SL("global-share-sharefile"),
                            SpixiLocalization._SL("global-share-savefile"));

            if (action.Equals(SpixiLocalization._SL("global-share-sharefile")))
            {
                shareFile(filepath, title);
            }
            else if(action.Equals(SpixiLocalization._SL("global-share-savefile")))
            {
                saveFile(filepath, title);
            }
        }

        public static void shareFile(string filepath, string title)
        {
            var context = MainActivity.Instance;
            File file = new File(filepath);
            Intent shareIntent = new Intent();
            shareIntent.SetAction(Intent.ActionSend);
            shareIntent.SetType("application/octet-stream");
            Android.Net.Uri uriShare = FileProvider.GetUriForFile(context, "com.ixilabs.spixi.provider", file);
            shareIntent.PutExtra(Intent.ExtraStream, uriShare);

            var chooserIntent = Intent.CreateChooser(shareIntent, title ?? string.Empty);
            chooserIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask);
            context.StartActivity(chooserIntent);
        }
        public static void saveFile(string filepath, string title)
        {
            var context = MainActivity.Instance;
            Intent saveIntent = new Intent(Intent.ActionCreateDocument);
            saveIntent.AddCategory(Intent.CategoryOpenable);
            saveIntent.SetType("application/octet-stream");
            saveIntent.PutExtra(Intent.ExtraTitle, Path.GetFileName(filepath));
            saveIntent.AddFlags(ActivityFlags.GrantWriteUriPermission);

            context.SaveFilePath = filepath;
            context.StartActivityForResult(saveIntent, MainActivity.SaveFileId);
        }
        public static string getMimeType(Android.Net.Uri uri)
        {
            string mime_type = null;
            if (uri.Scheme.Equals(ContentResolver.SchemeContent))
            {
                ContentResolver cr = MainActivity.Instance.ContentResolver;
                mime_type = cr.GetType(uri);
            }
            else
            {
                string ext = MimeTypeMap.GetFileExtensionFromUrl(uri.ToString());
                mime_type = MimeTypeMap.Singleton.GetMimeTypeFromExtension(ext.ToLower());
            }
            if (mime_type == null)
            {
                mime_type = "*/*";
            }
            return mime_type;
        }

        public static void open(string file_path)
        {
            var context = MainActivity.Instance;
            File f;
            if (file_path.StartsWith(context.FilesDir.AbsolutePath))
            {
                f = new File(context.FilesDir, file_path.Substring(context.FilesDir.AbsolutePath.Length));
            }
            else
            {
                f = new File(file_path);
            }

            if (f == null || !f.Exists())
            {
                return;
            }

            try
            {
                Android.Net.Uri file_uri = FileProvider.GetUriForFile(context, "com.ixilabs.spixi.provider", f);

                string mime_type = getMimeType(file_uri);

                Intent intent = new Intent(Intent.ActionView);
                intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.ClearWhenTaskReset | ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
                intent.SetDataAndType(file_uri, mime_type);

                context.StartActivity(Intent.CreateChooser(intent, "Open file with"));
            }
            catch (Exception e)
            {
                Logging.error("Exception occured while trying to open file " + e);
            }
        }


    }
}
