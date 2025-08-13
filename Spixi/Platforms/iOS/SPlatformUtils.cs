using AVFoundation;
using Foundation;
using SPIXI.Interfaces;
using SPIXI.Meta;

namespace Spixi
{
    public class SPlatformUtils
    {
        private static AVAudioPlayer? ringtonePlayer;
        private static AVAudioPlayer? dialtonePlayer;

        public static Stream getAsset(string path)
        {
            return new FileStream(Path.Combine(getAssetsPath(), path), FileMode.Open, FileAccess.Read);
        }

        public static string getAssetsBaseUrl()
        {
            return NSBundle.MainBundle.BundlePath + "/";
        }

        public static string getAssetsPath()
        {
            return NSBundle.MainBundle.BundlePath;
        }

        public static string getHtmlBaseUrl()
        {
            return Config.spixiUserFolder + "/html/";
        }

        public static string getHtmlPath()
        {
            return Config.spixiUserFolder + "/html";
        }

        public static void startRinging()
        {
            if (ringtonePlayer != null)
            {
                return;
            }

            try
            {
                bool ring = true;

                AVAudioSession audioSession = AVAudioSession.SharedInstance();
                audioSession.SetCategory(AVAudioSessionCategory.Playback);
                audioSession.SetActive(true);

                if (audioSession.OutputVolume == 0.0f)
                {
                    ring = false;
                }

                if (ring)
                {
                    string ringtonePath = Path.Combine(getAssetsPath(), "sounds/default_ringtone.mp3");
                    NSUrl soundUrl = NSUrl.FromFilename(ringtonePath);

                    ringtonePlayer = AVAudioPlayer.FromUrl(soundUrl)!;
                    ringtonePlayer.NumberOfLoops = -1;
                    ringtonePlayer.PrepareToPlay();
                    ringtonePlayer.Play();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception occurred in StartRinging: {e}");
                ringtonePlayer = null;
            }
        }

        public static void stopRinging()
        {
            if (ringtonePlayer == null)
            {
                return;
            }

            ringtonePlayer.Stop();
            ringtonePlayer.Dispose();
            ringtonePlayer = null;
        }

        public static void startDialtone(DialtoneType type)
        {
            try
            {
                stopDialtone();

                string soundFileName = string.Empty;
                bool shouldLoop = false;

                switch (type)
                {
                    case DialtoneType.busy:
                        soundFileName = "sounds/busy_tone.mp3";
                        break;
                    case DialtoneType.dialing:
                        soundFileName = "sounds/dialing_tone.mp3";
                        shouldLoop = true;
                        break;
                    case DialtoneType.error:
                        soundFileName = "sounds/error_tone.mp3";
                        break;
                    default:
                        return;
                }

                string toneFilePath = Path.Combine(getAssetsPath(), soundFileName);
                NSUrl soundUrl = NSUrl.FromFilename(toneFilePath);

                dialtonePlayer = AVAudioPlayer.FromUrl(soundUrl)!;
                dialtonePlayer.Volume = 0.1f;
                dialtonePlayer.NumberOfLoops = shouldLoop ? -1 : 0;  // Loop for dialing, play once for others
                dialtonePlayer.PrepareToPlay();
                dialtonePlayer.Play();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception occurred in StartDialtone: {e}");
                dialtonePlayer = null;
            }
        }

        public static void stopDialtone()
        {
            if (dialtonePlayer != null)
            {
                dialtonePlayer.Stop();
                dialtonePlayer.Dispose();
                dialtonePlayer = null;
            }
        }

        public static void setEdgeToEdge()
        {

        }

    }
}
