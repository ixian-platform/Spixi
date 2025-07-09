using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using IXICore.Meta;
using SPIXI.Interfaces;

namespace Spixi
{
    public class SPlatformUtils
    {
        static MediaPlayer? ringtone = null;
        static MediaPlayer? dialtonePlayer = null;


        public static System.IO.Stream getAsset(string path)
        {
            Task<System.IO.Stream> task = Task.Run<System.IO.Stream>(async () => await FileSystem.Current.OpenAppPackageFileAsync(path));
            return task.Result;
        }

        public static string getAssetsBaseUrl()
        {
            return "file:///android_asset/";
        }

        public static string getAssetsPath()
        {
            throw new System.NotImplementedException();
        }

        public static string getHtmlBaseUrl()
        {
            return SPIXI.Meta.Config.spixiUserFolder + "/html/";
        }

        public static string getHtmlPath()
        {
            return SPIXI.Meta.Config.spixiUserFolder + "/html";
        }

        public static void startRinging()
        {
            if (ringtone != null)
            {
                return;
            }

            try
            {
                bool ring = true;

                NotificationManager nm = (NotificationManager)MainActivity.Instance.GetSystemService(Context.NotificationService)!;
                InterruptionFilter int_filter = nm.CurrentInterruptionFilter;
                if (int_filter != InterruptionFilter.Priority && int_filter != InterruptionFilter.All)
                {
                   ring = false;
                }
                

                AudioManager am = (AudioManager)MainActivity.Instance.GetSystemService(Context.AudioService)!;
                if (am.RingerMode != RingerMode.Normal)
                {
                    ring = false;
                }

                MainActivity.Instance.VolumeControlStream = Android.Media.Stream.Ring;

                if (ring)
                {
                    ringtone = playSoundFromAssets("sounds/default_ringtone.mp3");
                    ringtone.Looping = true;
                    ringtone.Start();
                }
            }
            catch (Exception e)
            {
                Logging.error("Exception occurred in startRinging: " + e);
                ringtone = null;
            }
        }

        public static void stopRinging()
        {
            if (ringtone == null)
            {
                return;
            }

            try
            {
                if (ringtone.IsPlaying)
                {
                    ringtone.Stop();
                }
                ringtone.Release();
            }
            catch (Exception e)
            {
                Logging.error("Exception occurred while stopping the ringtone: " + e);
            }
            finally
            {
                ringtone = null;
                MainActivity.Instance.VolumeControlStream = Android.Media.Stream.NotificationDefault;
            }
        }

        public static void startDialtone(DialtoneType type)
        {
            try
            {
                stopDialtone();
                string toneFile = string.Empty;
                bool shouldLoop = false;

                switch (type)
                {
                    case DialtoneType.busy:
                        toneFile = "sounds/busy_tone.mp3";
                        break;
                    case DialtoneType.dialing:
                        toneFile = "sounds/dialing_tone.mp3";
                        shouldLoop = true;
                        break;
                    case DialtoneType.error:
                        toneFile = "sounds/error_tone.mp3";
                        break;
                    default:
                        return;
                }

                dialtonePlayer = playSoundFromAssets(toneFile);
                dialtonePlayer.SetVolume(0.1f, 0.1f);
                dialtonePlayer.Looping = shouldLoop;
                dialtonePlayer.Start();
            }
            catch (Exception e)
            {
                Logging.error("Exception occurred in startDialtone: " + e);
                dialtonePlayer = null;
            }
        }

        public static void stopDialtone()
        {
            if (dialtonePlayer != null)
            {
                dialtonePlayer.Stop();
                dialtonePlayer.Release();
                dialtonePlayer = null;
            }
        }

        private static MediaPlayer playSoundFromAssets(string filePath)
        {
            MediaPlayer player = new MediaPlayer();
            try
            {
                var assetDescriptor = MainActivity.Instance.Assets!.OpenFd(filePath);
                player.SetDataSource(assetDescriptor.FileDescriptor, assetDescriptor.StartOffset, assetDescriptor.Length);
                assetDescriptor.Close();
                player.Prepare();
            }
            catch (Exception e)
            {
                Logging.error("Error playing sound: " + e);
            }
            return player;
        }
    }

}
