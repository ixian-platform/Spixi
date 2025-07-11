using IXICore.Meta;
using NAudio.Wave;
using SPIXI.Interfaces;
using SPIXI.Meta;

namespace Spixi
{
    public class SPlatformUtils
    {
        static IWavePlayer? ringtonePlayer = null;
        static IWavePlayer? dialtonePlayer = null;
        static WaveStream? ringtoneStream = null;
        static WaveStream? dialtoneStream = null;

        public static Stream getAsset(string path)
        {
            Task<Stream> task = Task.Run<Stream>(async () => await FileSystem.Current.OpenAppPackageFileAsync(path));
            return task.Result;
        }

        public static string getAssetsBaseUrl()
        {
            return "pack://siteoforigin:,,,/";
        }

        public static string getAssetsPath()
        {
            return System.AppDomain.CurrentDomain.BaseDirectory;
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
                ringtonePlayer = playSoundFromAssets("sounds/default_ringtone.mp3", true);
            }
            catch (Exception e)
            {
                Logging.error("Exception occurred in startRinging: " + e);
                ringtonePlayer = null;
            }
        }

        public static void stopRinging()
        {
            if (ringtonePlayer == null)
            {
                return;
            }

            try
            {
                ringtonePlayer.Stop();
                ringtoneStream?.Dispose();
            }
            catch (Exception e)
            {
                Logging.error("Exception occurred while stopping the ringtone: " + e);
            }
            finally
            {
                ringtonePlayer.Dispose();
                ringtonePlayer = null;
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

                dialtonePlayer = playSoundFromAssets(toneFile, shouldLoop);
                dialtonePlayer.Volume = 0.1f;
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
                dialtoneStream?.Dispose();
                dialtonePlayer.Dispose();
                dialtonePlayer = null;
            }
        }


        private static IWavePlayer playSoundFromAssets(string filePath, bool loop = false)
        {
            string fullPath = Path.Combine(getAssetsPath(), filePath);
            IWavePlayer player = new WaveOutEvent();
            WaveStream mp3Reader = new Mp3FileReader(fullPath);

            if (loop)
            {
                player.Init(new LoopStream(mp3Reader));
            }
            else
            {
                player.Init(mp3Reader);
            }

            player.Play();
            return player;
        }

    }

    public class LoopStream : WaveStream
    {
        private readonly WaveStream sourceStream;

        public LoopStream(WaveStream sourceStream)
        {
            this.sourceStream = sourceStream;
        }

        public override WaveFormat WaveFormat => sourceStream.WaveFormat;

        public override long Length => long.MaxValue;

        public override long Position
        {
            get => sourceStream.Position;
            set => sourceStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = sourceStream.Read(buffer, offset, count);
            if (read == 0)
            {
                sourceStream.Position = 0;
                read = sourceStream.Read(buffer, offset, count);
            }
            return read;
        }
    }
}
