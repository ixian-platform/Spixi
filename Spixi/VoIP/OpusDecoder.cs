using Concentus;

namespace SPIXI.VoIP
{
    public enum OpusDecoderReturnType
    {
        bytes,
        shorts,
        floats
    }

    public class OpusDecoder : IAudioDecoder, IDisposable
    {
        IOpusDecoder decoder = null;
        bool running = false;

        int samples;
        int channels;

        IAudioDecoderCallback decodedDataCallback = null;
        OpusDecoderReturnType returnType = OpusDecoderReturnType.bytes;

        public OpusDecoder(int samples, int channels, IAudioDecoderCallback decoder_callback, OpusDecoderReturnType return_type = OpusDecoderReturnType.bytes)
        {
            this.samples = samples;
            this.channels = channels;
            decodedDataCallback = decoder_callback;
            returnType = return_type;
        }

        private byte[] shortsToBytes(short[] input, int size)
        {
            byte[] output = new byte[size * 2];
            Buffer.BlockCopy(input, 0, output, 0, size * 2);
            return output;
        }

        public void decode(byte[] data)
        {
            if (!running)
            {
                return;
            }

            int maxFrameSize = samples / 1000 * 120;
            int offset = 0;
            while (offset + 2 <= data.Length)
            {
                int packetSize = BitConverter.ToInt16(data, offset);
                offset += 2;

                if (offset + packetSize > data.Length)
                {
                    break;
                }

                switch (returnType)
                {
                    case OpusDecoderReturnType.bytes:
                        {
                            short[] pcmShorts = new short[maxFrameSize * channels];
                            int decodedSamples = decoder.Decode(data.AsSpan(offset, packetSize), pcmShorts, pcmShorts.Length, false);
                            decodedDataCallback.onDecodedData(shortsToBytes(pcmShorts, decodedSamples * channels));
                        }
                        break;

                    case OpusDecoderReturnType.shorts:
                        {
                            short[] pcmShorts = new short[maxFrameSize * channels];
                            int decodedSamples = decoder.Decode(data.AsSpan(offset, packetSize), pcmShorts, pcmShorts.Length, false);
                            short[] sendShorts = new short[decodedSamples * channels];
                            Array.Copy(pcmShorts, sendShorts, sendShorts.Length);
                            decodedDataCallback.onDecodedData(sendShorts);
                        }
                        break;

                    case OpusDecoderReturnType.floats:
                        {
                            float[] pcmFloats = new float[maxFrameSize * channels];
                            int decodedSamples = decoder.Decode(data.AsSpan(offset, packetSize), pcmFloats, pcmFloats.Length, false);
                            float[] sendFloats = new float[decodedSamples * channels];
                            Array.Copy(pcmFloats, sendFloats, sendFloats.Length);
                            decodedDataCallback.onDecodedData(sendFloats);
                        }
                        break;
                }

                offset += packetSize;
            }
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

            decoder?.Dispose();
            decoder = null;
        }

        public void Dispose()
        {
            stop();
        }
    }
}
