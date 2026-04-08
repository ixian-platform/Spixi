using Android.Media;
using IXICore.Meta;
using SPIXI.VoIP;
using System;
using System.Collections.Generic;

namespace Spixi
{
    class HwEncoder : MediaCodec.Callback, IAudioEncoder
    {
        MediaCodec audioEncoder = null;
        List<int> availableBuffers = new List<int>();
        string encoderMimeType = null;
        MediaFormat mediaFormat = null;
        IAudioEncoderCallback encodedDataCallback = null;

        bool running = false;

        public HwEncoder(string mime_type, MediaFormat format, IAudioEncoderCallback encoded_data_callback)
        {
            encoderMimeType = mime_type;
            mediaFormat = format;
            encodedDataCallback = encoded_data_callback;
        }
        public void start()
        {
            if (running)
            {
                return;
            }
            running = true;

            lock (availableBuffers)
            {
                availableBuffers.Clear();
            }

            audioEncoder = MediaCodec.CreateEncoderByType(encoderMimeType);
            audioEncoder.SetCallback(this);
            audioEncoder.Configure(mediaFormat, null, null, MediaCodecConfigFlags.Encode);
            audioEncoder.Start();
        }

        public void stop()
        {
            if (!running)
            {
                return;
            }
            running = false;
            if (audioEncoder != null)
            {
                try
                {
                    audioEncoder.Stop();
                    audioEncoder.Release();
                }
                catch (Exception)
                {

                }
                audioEncoder.Dispose();
                audioEncoder = null;
            }
            lock (availableBuffers)
            {
                availableBuffers.Clear();
            }
        }

        public new void Dispose()
        {
            stop();
            base.Dispose();
        }

        public void encode(byte[] data, int offset, int size)
        {
            if (!running)
            {
                return;
            }
            if (size == 0)
            {
                return;
            }
            int buffer_index = -1;

            lock (availableBuffers)
            {
                if (availableBuffers.Count > 0)
                {
                    buffer_index = availableBuffers[0];
                    availableBuffers.RemoveAt(0);
                }
            }

            if (buffer_index > -1)
            {
                var ib = audioEncoder.GetInputBuffer(buffer_index);
                ib.Clear();

                ib.Put(data);

                audioEncoder.QueueInputBuffer(buffer_index, offset, size, 0, 0);
            }
            return;
        }

        public override void OnError(MediaCodec codec, MediaCodec.CodecException e)
        {
            Logging.error("Error occured in HwEncoder callback: " + e);
        }

        public override void OnInputBufferAvailable(MediaCodec codec, int index)
        {
            if (!running)
            {
                return;
            }
            lock (availableBuffers)
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
                var ob = audioEncoder.GetOutputBuffer(index);

                ob.Position(info.Offset);
                ob.Limit(info.Offset + info.Size);

                byte[] buffer = new byte[info.Size];
                ob.Get(buffer, 0, info.Size);
                audioEncoder.ReleaseOutputBuffer(index, false);

                encodedDataCallback.onEncodedData(buffer);
            }
            catch (Exception e)
            {
                Logging.error("Exception occured while recording audio stream: " + e);
            }
        }

        public override void OnOutputFormatChanged(MediaCodec codec, MediaFormat format)
        {
        }

        public void encode(short[] data, int offset, int size)
        {
            throw new NotImplementedException();
        }
    }
}
