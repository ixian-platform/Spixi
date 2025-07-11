using Concentus;
using Concentus.Enums;
using IXICore.Meta;
using Org.BouncyCastle.Crypto.Digests;
using System;
using System.Threading;

namespace SPIXI.VoIP
{
    public class OpusEncoder : IAudioEncoder
    {
        IOpusEncoder encoder = null;
        bool running = false;

        int samples;
        int bitRate;
        int channels;
        OpusApplication opusApplication = OpusApplication.OPUS_APPLICATION_AUDIO;

        int frameSize;

        IAudioEncoderCallback encodedDataCallback = null;

        Thread encodeThread = null;

        short[] inputBuffer = null;
        int inputBufferPos = 0;

        byte[] frameOutputBuffer = new byte[1275];

        public OpusEncoder(int samples, int bit_rate, int channels, OpusApplication application, IAudioEncoderCallback encoder_callback)
        {
            this.samples = samples;
            bitRate = bit_rate;
            this.channels = channels;
            opusApplication = application;
            frameSize = CodecTools.getPcmFrameByteSize(samples, 16, channels) * 20;
            encodedDataCallback = encoder_callback;
        }

        private short[] bytesToShorts(byte[] data, int offset, int size)
        {
            short[] output = new short[size / 2];
            for (int c = 0; c < output.Length; c++)
            {
                output[c] = (short)(((int)data[(c * 2) + offset]) << 0);
                output[c] += (short)(((int)data[(c * 2) + 1 + offset]) << 8);
            }

            return output;
        }

        public void encode(short[] data, int offset, int size)
        {
            if (!running)
            {
                return;
            }

            lock (inputBuffer)
            {
                if (size > inputBuffer.Length - inputBufferPos)
                {
                    size = 0;
                }
                if (size == 0)
                {
                    return;
                }
                Array.Copy(data, offset, inputBuffer, inputBufferPos, size);
                inputBufferPos += size;
            }

            return;
        }

        public void encode(byte[] data, int offset, int size)
        {
            if (!running)
            {
                return;
            }

            lock(inputBuffer)
            {
                if(size > inputBuffer.Length - inputBufferPos)
                {
                    size = 0;
                }
                if(size == 0)
                {
                    return;
                }
                short[] shorts = bytesToShorts(data, offset, size);
                Array.Copy(shorts, 0, inputBuffer, inputBufferPos, shorts.Length);
                inputBufferPos += shorts.Length;
            }

            return;
        }

        public void start()
        {
            if (running)
            {
                return;
            }
            running = true;

            inputBuffer = new short[frameSize * 500];
            inputBufferPos = 0;
            
            encoder = OpusCodecFactory.CreateEncoder(samples, channels, opusApplication);
            encoder.Bitrate = bitRate;

            encodeThread = new Thread(encodeLoop);
            encodeThread.Start();
        }

        public void stop()
        {
            if (!running)
            {
                return;
            }
            running = false;

            lock (inputBuffer)
            {
                encoder = null;

                inputBuffer = null;
                inputBufferPos = 0;
            }
        }

        public void Dispose()
        {
            stop();
        }

        private byte[] encodeFrame(short[] shorts, int offset)
        {
            int packet_size = encoder.Encode(shorts.AsSpan(offset), frameSize, frameOutputBuffer, frameOutputBuffer.Length);
            byte[] trimmed_buffer = new byte[packet_size + 2];

            byte[] packet_size_bytes = BitConverter.GetBytes((short)packet_size);            
            trimmed_buffer[0] = packet_size_bytes[0];
            trimmed_buffer[1] = packet_size_bytes[1];
            
            Array.Copy(frameOutputBuffer, 0, trimmed_buffer, 2, packet_size);

            return trimmed_buffer;
        }

        private void encodeLoop()
        {
            short[] tmp_buffer = new short[inputBuffer.Length];
            while (running)
            {
                int tmp_buffer_size = 0;
                lock (inputBuffer)
                {
                    int frame_count = (int)Math.Floor((decimal)inputBufferPos / frameSize);
                    if(frame_count > 0)
                    {
                        tmp_buffer_size = frame_count * frameSize;
                        Array.Copy(inputBuffer, tmp_buffer, tmp_buffer_size);
                        inputBufferPos = inputBufferPos - tmp_buffer_size;
                        if (inputBufferPos > 0)
                        {
                            Array.Copy(inputBuffer, tmp_buffer_size, inputBuffer, 0, inputBufferPos);
                        }
                    }
                }
                int encoded_bytes = 0;
                while (running && encoded_bytes + frameSize <= tmp_buffer_size)
                {
                    try
                    {
                        byte[] data = encodeFrame(tmp_buffer, encoded_bytes);
                        encodedDataCallback.onEncodedData(data);
                    }
                    catch (Exception e)
                    {
                        Logging.error("Exception occured while encoding audio stream: " + e);
                    }
                    encoded_bytes += frameSize;
                }
                Thread.Sleep(5);
            }
            encodeThread = null;
        }
    }
}
