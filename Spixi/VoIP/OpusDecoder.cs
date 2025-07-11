using Concentus;
using System;

namespace SPIXI.VoIP
{
    public enum OpusDecoderReturnType
    {
        bytes,
        shorts,
        floats
    }

    public class OpusDecoder : IAudioDecoder
    {
        IOpusDecoder decoder = null;
        bool running = false;

        int samples;
        int channels;

        int frameSize;

        IAudioDecoderCallback decodedDataCallback = null;
        OpusDecoderReturnType returnType = OpusDecoderReturnType.bytes;

        public OpusDecoder(int samples, int channels, IAudioDecoderCallback decoder_callback, OpusDecoderReturnType return_type = OpusDecoderReturnType.bytes)
        {
            this.samples = samples;
            this.channels = channels;
            frameSize = CodecTools.getPcmFrameByteSize(samples, 16, channels) * 20;
            decodedDataCallback = decoder_callback;
            returnType = return_type;
        }

        private byte[] shortsToBytes(short[] input, int offset, int size)
        {
            byte[] output = new byte[size * 2];
            for (int c = 0; c < size; c++)
            {
                output[(c * 2)] = (byte)(input[c + offset] & 0xFF);
                output[(c * 2) + 1] = (byte)((input[c + offset] >> 8) & 0xFF);
            }
            return output;
        }

        public void decode(byte[] data)
        {
            if (!running)
            {
                return;
            }
            switch (returnType)
            {
                case OpusDecoderReturnType.bytes:
                    short[] shorts = new short[frameSize * 10];
                    for (int offset = 0, packet_size = 0; offset < data.Length; offset += packet_size + 2)
                    {
                        packet_size = BitConverter.ToInt16(data, offset);
                        int decoded_size = decoder.Decode(data.AsSpan(offset + 2, packet_size), shorts, shorts.Length, false);
                        decodedDataCallback.onDecodedData(shortsToBytes(shorts, 0, decoded_size));
                    }
                    break;
                case OpusDecoderReturnType.shorts:
                    shorts = new short[frameSize * 10];
                    for (int offset = 0, packet_size = 0; offset < data.Length; offset += packet_size + 2)
                    {
                        packet_size = BitConverter.ToInt16(data, offset);
                        int decoded_size = decoder.Decode(data.AsSpan(offset + 2, packet_size), shorts, shorts.Length, false);
                        short[] send_buffer = new short[decoded_size];
                        Array.Copy(shorts, send_buffer, send_buffer.Length);
                        decodedDataCallback.onDecodedData(send_buffer);
                    }
                    break;
                case OpusDecoderReturnType.floats:
                    float[] floats = new float[frameSize * 4 * 10];
                    for (int offset = 0, packet_size = 0; offset < data.Length; offset += packet_size + 2)
                    {
                        packet_size = BitConverter.ToInt16(data, offset);
                        int decoded_size = decoder.Decode(data.AsSpan(offset + 2, packet_size), floats, floats.Length, false);
                        float[] send_buffer = new float[decoded_size];
                        Array.Copy(floats, send_buffer, send_buffer.Length);
                        decodedDataCallback.onDecodedData(send_buffer);
                    }
                    break;
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
            decoder = OpusCodecFactory.CreateDecoder(samples, channels);
        }

        public void stop()
        {
            if (!running)
            {
                return;
            }
            running = false;
            decoder = null;
        }

        public void Dispose()
        {
            stop();
        }
    }
}
