using AVFoundation;
using Foundation;
using IXICore.Meta;
using Spixi.VoIP;
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

        private PlaybackCatchupController playbackCatchupController = new PlaybackCatchupController();
        private AVAudioUnitTimePitch timePitchNode;
        private long totalFramesWritten = 0;
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
            inputAudioFormat = new AVAudioFormat(AVAudioCommonFormat.PCMFloat32, sampleRate, (uint)channels, true);
            
            timePitchNode = new AVAudioUnitTimePitch();
            timePitchNode.Rate = 1.0f;
            
            audioEngine.AttachNode(audioPlayer);
            audioEngine.AttachNode(timePitchNode);

            audioEngine.Connect(audioPlayer, timePitchNode, inputAudioFormat);
            audioEngine.Connect(timePitchNode, audioEngine.MainMixerNode, inputAudioFormat);

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

            totalFramesWritten = 0;
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
            if (!running || audioPlayer == null)
            {
                return;
            }

            int frames = data.Length / channels;
            if (frames <= 0)
            {
                return;
            }

            var buffer = new AVAudioPcmBuffer(inputAudioFormat, (uint)frames)
            {
                FrameLength = (uint)frames
            };

            var basePtr = buffer.FloatChannelData;
            if (basePtr == IntPtr.Zero)
            {
                buffer.Dispose();
                return;
            }

            IntPtr channelPtr = Marshal.ReadIntPtr(basePtr, 0);

            Marshal.Copy(data, 0, channelPtr, data.Length);

            long playedFrames = 0;
            var lastRenderTime = audioEngine.OutputNode.LastRenderTime;
            if (lastRenderTime != null)
            {
                var playerTime = audioPlayer.GetPlayerTimeFromNodeTime(lastRenderTime);
                if (playerTime != null)
                    playedFrames = playerTime.SampleTime;
            }

            long queuedFrames = Math.Max(totalFramesWritten - playedFrames, 0);
            double queuedSeconds = (double)queuedFrames / sampleRate;

            var catchup = playbackCatchupController.Update(queuedSeconds);
            bool shouldDrop = false;

            switch (catchup.Type)
            {
                case PlaybackCatchupType.Drop:
                    timePitchNode.Rate = catchup.Speed;
                    if (Random.Shared.NextDouble() < 0.10)
                    {
                        Logging.warn($"VoIP Dropping frame, avg {playbackCatchupController.GetAverageLatency() * 1000:F0}ms");
                        shouldDrop = true;
                    }
                    break;

                case PlaybackCatchupType.SpeedUp:
                    timePitchNode.Rate = catchup.Speed;
                    break;

                case PlaybackCatchupType.Normal:
                    timePitchNode.Rate = 1.0f;
                    break;
            }

            if (!shouldDrop)
            {
                audioPlayer.ScheduleBuffer(buffer, () =>
                {
                    buffer.Dispose();
                });

                totalFramesWritten += buffer.FrameLength;
            }
            else
            {
                buffer.Dispose();
            }
        }


        public void onDecodedData(short[] data)
        {
            throw new NotImplementedException();
        }

    }
}
