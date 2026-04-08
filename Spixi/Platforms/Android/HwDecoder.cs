using Android.Media;
using IXICore.Meta;
using SPIXI.VoIP;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Spixi
{
    class HwDecoder : MediaCodec.Callback, IAudioDecoder
    {
        MediaCodec audioDecoder = null;
        string decoderMimeType = null;
        MediaFormat mediaFormat = null;
        IAudioDecoderCallback decodedDataCallback = null;

        List<byte[]> pendingFrames = new List<byte[]>();
        List<int> availableBuffers = new List<int>();

        bool running = false;

        int delay = 5;

        Thread decodeThread = null;

        public HwDecoder(string mime_type, MediaFormat format, IAudioDecoderCallback decoded_data_callback)
        {
            decoderMimeType = mime_type;
            mediaFormat = format;
            decodedDataCallback = decoded_data_callback;
        }

        public void start()
        {
            if (running)
            {
                return;
            }
            running = true;
            delay = 5;

            lock (pendingFrames)
            {
                pendingFrames.Clear();
                availableBuffers.Clear();
            }

            audioDecoder = MediaCodec.CreateDecoderByType(decoderMimeType);
            audioDecoder.SetCallback(this);
            audioDecoder.Configure(mediaFormat, null, null, MediaCodecConfigFlags.None);
            audioDecoder.Start();

            decodeThread = new Thread(decodeLoop);
            decodeThread.Start();
        }

        public void decode(byte[] data)
        {
            if (!running)
            {
                return;
            }
            lock (pendingFrames)
            {
                if (pendingFrames.Count > 10)
                {
                    pendingFrames.RemoveAt(0);
                }

                pendingFrames.Add(data);

                if (delay > 0)
                {
                    delay--;
                }
                return;
            }
        }

        public override void OnError(MediaCodec codec, MediaCodec.CodecException e)
        {
            Logging.error("Error occured in AudioPlayerAndroid callback: " + e);
        }

        public override void OnInputBufferAvailable(MediaCodec codec, int index)
        {
            if (!running)
            {
                return;
            }
            lock (pendingFrames)
            {
                availableBuffers.Add(index);
            }
        }

        public override void OnOutputBufferAvailable(MediaCodec codec, int index, MediaCodec.BufferInfo info)
        {
            if (!running)
            {
                return;
            }
            try
            {
                var ob = audioDecoder.GetOutputBuffer(index);

                ob.Position(info.Offset);
                ob.Limit(info.Offset + info.Size);

                byte[] decoded_data = new byte[info.Size];
                ob.Get(decoded_data);

                audioDecoder.ReleaseOutputBuffer(index, false);

                decodedDataCallback.onDecodedData(decoded_data);
            }
            catch (Exception e)
            {
                Logging.error("Exception occured while playing audio stream: " + e);
            }
        }

        public override void OnOutputFormatChanged(MediaCodec codec, MediaFormat format)
        {
        }

        public void stop()
        {
            if (!running)
            {
                return;
            }
            running = false;

            lock (pendingFrames)
            {
                pendingFrames.Clear();
                availableBuffers.Clear();
            }

            if (audioDecoder != null)
            {
                try
                {
                    audioDecoder.Stop();
                    audioDecoder.Release();
                }
                catch (Exception)
                {

                }
                audioDecoder.Dispose();
                audioDecoder = null;
            }
        }

        public new void Dispose()
        {
            stop();
            base.Dispose();
        }

        private void doDecode(int buffer_index, byte[] data)
        {
            if (!running)
            {
                return;
            }
            try
            {
                var ib = audioDecoder.GetInputBuffer(buffer_index);
                ib.Clear();

                ib.Put(data);

                audioDecoder.QueueInputBuffer(buffer_index, 0, data.Length, 0, 0);
            }
            catch (Exception e)
            {
                Logging.error("Exception occured in audio decoder: " + e);
            }
            return;
        }

        private void decodeLoop()
        {
            while (running)
            {
                if (delay > 0)
                {
                    Thread.Sleep(10);
                    continue;
                }
                int buffer_index = -1;
                byte[] frame = null;
                lock (pendingFrames)
                {
                    if (availableBuffers.Count > 0 && pendingFrames.Count > 0)
                    {
                        buffer_index = availableBuffers[0];
                        frame = pendingFrames[0];
                        pendingFrames.RemoveAt(0);
                        availableBuffers.RemoveAt(0);
                    }
                }
                if (buffer_index > -1)
                {
                    doDecode(buffer_index, frame);
                }
                Thread.Sleep(10);
            }
            decodeThread = null;
        }

    }
}
