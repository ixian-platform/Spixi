using SPIXI.VoIP;
using NAudio.Wave;
using IXICore.Meta;
using Spixi.VoIP;

namespace Spixi
{
    public class SAudioPlayer : IAudioPlayer, IAudioDecoderCallback, IDisposable
    {
        private IWavePlayer? audioPlayer = null;
        private IAudioDecoder? audioDecoder = null;

        private BufferedWaveProvider? provider = null;

        private bool running = false;

        int sampleRate = SPIXI.Meta.Config.VoIP_sampleRate;
        int bitsPerSample = SPIXI.Meta.Config.VoIP_bitsPerSample;
        int channels = SPIXI.Meta.Config.VoIP_channels;

        private static SAudioPlayer? _singletonInstance;

        private PlaybackCatchupController playbackCatchupController = new PlaybackCatchupController();
        public static SAudioPlayer Instance()
        {
            if (_singletonInstance == null)
            {
                _singletonInstance = new SAudioPlayer();
            }
            return _singletonInstance;
        }
        public SAudioPlayer()
        {
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
            provider = new BufferedWaveProvider(new WaveFormat(sampleRate, bitsPerSample, channels))
            {
                BufferDuration = TimeSpan.FromSeconds(2),
                DiscardOnBufferOverflow = false
            };

            audioPlayer = new WaveOutEvent();
            audioPlayer.Init(provider);
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
            audioDecoder = new OpusDecoder(sampleRate, channels, this);
            audioDecoder.start();
        }

        public int write(byte[] audio_data)
        {
            if (!running || audioDecoder == null)
            {
                return 0;
            }

            decode(audio_data);
            return audio_data.Length;
        }

        public void stop()
        {
            if (!running)
            {
                return;
            }

            running = false;

            if (audioPlayer != null)
            {
                try
                {
                    audioPlayer.Stop();
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

            if (provider != null)
            {
                provider.ClearBuffer();
                provider = null;
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
            if (!running || audioDecoder == null)
            {
                return;
            }
            audioDecoder.decode(data);
        }

        public void onDecodedData(byte[] data)
        {
            if (!running || provider == null)
            {
                return;
            }

            try
            {
                double queuedSeconds = provider.BufferedDuration.TotalSeconds;

                var catchup = playbackCatchupController.Update(queuedSeconds);
                bool shouldDrop = false;
                switch (catchup.Type)
                {
                    case PlaybackCatchupType.Drop:
                        {
                            if (1 + Random.Shared.NextDouble() < catchup.Speed + 0.10)
                            {
                                Logging.warn($"VoIP Dropping frame, avg {playbackCatchupController.GetAverageLatency() * 1000:F0}ms");
                                shouldDrop = true;
                            }
                        }
                        break;
                    case PlaybackCatchupType.SpeedUp:
                        {
                            if (1 + Random.Shared.NextDouble() < catchup.Speed)
                            {
                                Logging.warn($"VoIP Dropping frame, avg {playbackCatchupController.GetAverageLatency() * 1000:F0}ms");
                                shouldDrop = true;
                            }
                        }
                        break;

                    case PlaybackCatchupType.Normal:
                        break;
                }

                if (!shouldDrop)
                {
                    provider.AddSamples(data, 0, data.Length);
                }
            }
            catch (Exception e)
            {
                Logging.error("Audio buffer error: " + e.Message);
            }
        }

        public void setVolume(float volume)
        {
            if (audioPlayer is WaveOutEvent waveOut)
            {
                waveOut.Volume = Math.Clamp(volume, 0f, 1f);
            }
        }

        public void onDecodedData(float[] data)
        {
            throw new NotImplementedException();
        }

        public void onDecodedData(short[] data)
        {
            throw new NotImplementedException();
        }
    }
}
