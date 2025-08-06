using AVFoundation;
using Foundation;
using IXICore.Meta;
using SPIXI.VoIP;
using System.Runtime.InteropServices;

namespace Spixi
{
    public class SAudioPlayer : IAudioPlayer, IAudioDecoderCallback
    {
        private AVAudioEngine audioEngine = null;
        private AVAudioPlayerNode audioPlayer = null;
        private AVAudioFormat inputAudioFormat = null;

        private IAudioDecoder audioDecoder = null;

        private bool running = false;

        int sampleRate = SPIXI.Meta.Config.VoIP_sampleRate;
        int bitsPerSample = SPIXI.Meta.Config.VoIP_bitsPerSample;
        int channels = SPIXI.Meta.Config.VoIP_channels;

        private static SAudioPlayer _singletonInstance;
        public static SAudioPlayer Instance()
        {
            if (_singletonInstance == null)
            {
                _singletonInstance = new SAudioPlayer();
            }
            return _singletonInstance;
        }

        public void start(string codec)
        {
            if (running)
            {
                Logging.warn("Audio player is already running.");
                return;
            }

            running = true;

            initPlayer();
            initDecoder(codec);
        }

        private void initPlayer()
        {
            audioEngine = new AVAudioEngine();
            NSError error = new NSError();
            if (!AVAudioSession.SharedInstance().SetPreferredSampleRate(sampleRate, out error))
            {
                throw new Exception("Error setting preffered sample rate for player: " + error);
            }
            AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.PlayAndRecord, AVAudioSessionCategoryOptions.InterruptSpokenAudioAndMixWithOthers);
            AVAudioSession.SharedInstance().SetActive(true);

            audioPlayer = new AVAudioPlayerNode();
            setVolume(AVAudioSession.SharedInstance().OutputVolume);
            inputAudioFormat = new AVAudioFormat(AVAudioCommonFormat.PCMFloat32, sampleRate, (uint)channels, false);

            audioEngine.AttachNode(audioPlayer);
            audioEngine.Connect(audioPlayer, audioEngine.MainMixerNode, inputAudioFormat);
            audioEngine.Prepare();
            if (!audioEngine.StartAndReturnError(out error))
            {
                throw new Exception("Error starting playback audio engine: " + error);
            }
            audioPlayer.Play();
        }

        private void initDecoder(string codec)
        {
            switch (codec)
            {
                case "opus":
                    initOpusDecoder();
                    break;

                default:
                    throw new Exception("Unknown player codec selected " + codec);
            }
        }

        private void initOpusDecoder()
        {
            audioDecoder = new OpusDecoder(sampleRate, channels, this, OpusDecoderReturnType.floats);
            audioDecoder.start();
        }

        public int write(byte[] audio_data)
        {
            if (!running)
            {
                return 0;
            }
            if (audioPlayer != null && running)
            {
                decode(audio_data);
                return audio_data.Length;
            }
            return 0;
        }

        public void stop()
        {
            if (!running)
            {
                return;
            }

            running = false;

            AVAudioSession.SharedInstance().SetActive(false);

            if (audioPlayer != null)
            {
                try
                {
                    audioPlayer.Stop();
                    audioPlayer.Reset();
                }
                catch (Exception)
                {

                }

                audioPlayer.Dispose();
                audioPlayer = null;
            }

            if (audioDecoder != null)
            {
                audioDecoder.stop();
                audioDecoder.Dispose();
                audioDecoder = null;
            }

            if (audioEngine != null)
            {
                try
                {
                    audioEngine.Stop();
                    audioEngine.Reset();
                }
                catch (Exception)
                {

                }

                audioEngine.Dispose();
                audioEngine = null;
            }
        }

        public void Dispose()
        {
            stop();
        }

        public bool isRunning()
        {
            return running;
        }

        private void decode(byte[] data)
        {
            if (!running)
            {
                return;
            }
            audioDecoder.decode(data);
        }

        public void onDecodedData(byte[] data)
        {
            throw new NotImplementedException();
        }

        public void setVolume(float volume)
        {
            if (audioPlayer != null)
            {
                audioPlayer.Volume = volume;
            }
        }

        public void onDecodedData(float[] data)
        {
            if (!running)
            {
                return;
            }

            if (audioPlayer != null)
            {
                AVAudioPcmBuffer buffer = new AVAudioPcmBuffer(inputAudioFormat, (uint)data.Length);
                IntPtr int_data = Marshal.AllocHGlobal(data.Length * sizeof(float));
                Marshal.Copy(data, 0, int_data, data.Length);

                buffer.AudioBufferList.SetData(0, int_data, data.Length);
                buffer.FrameLength = (uint)data.Length;

                audioPlayer.ScheduleBuffer(buffer, () => {
                    Marshal.FreeHGlobal(int_data);
                    buffer.Dispose();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                });
            }
        }

        public void onDecodedData(short[] data)
        {
            throw new NotImplementedException();
        }

    }
}
