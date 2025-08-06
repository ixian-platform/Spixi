using Concentus;
using Concentus.Enums;
using IXICore.Meta;

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

        private const int maxOpusPacketSize = 1275;
        byte[] frameOutputBuffer = new byte[maxOpusPacketSize];

        public OpusEncoder(int samples, int bitRate, int channels, OpusApplication application, IAudioEncoderCallback encoder_callback)
        {
            this.samples = samples;
            this.bitRate = bitRate;
            this.channels = channels;
            opusApplication = application;
            frameSize = CodecTools.getPcmFrameSize(samples, 20); // 20ms frame
            encodedDataCallback = encoder_callback;
        }

        private short[] bytesToShorts(byte[] data, int offset, int size)
        {
            int sampleCount = size / 2;
            short[] output = new short[sampleCount];
            Buffer.BlockCopy(data, offset, output, 0, size);
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
                int spaceLeft = inputBuffer.Length - inputBufferPos;
                if (size > spaceLeft)
                {
                    size = spaceLeft;
                }
                if (size > 0)
                {
                    Array.Copy(data, offset, inputBuffer, inputBufferPos, size);
                    inputBufferPos += size;
                }
            }
        }

        public void encode(byte[] data, int offset, int size)
        {
            if (!running)
            {
                return;
            }

            short[] shorts = bytesToShorts(data, offset, size);
            encode(shorts, 0, shorts.Length);
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

            encodeThread?.Join();
            encodeThread = null;

            lock (inputBuffer)
            {
                encoder?.Dispose();
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
            short[] tmpBuffer = new short[inputBuffer.Length];

            while (running)
            {
                int tmpBufferSize = 0;

                lock (inputBuffer)
                {
                    int frameCount = inputBufferPos / frameSize;
                    if (frameCount > 0)
                    {
                        tmpBufferSize = frameCount * frameSize;
                        Array.Copy(inputBuffer, 0, tmpBuffer, 0, tmpBufferSize);

                        // Move remaining samples to front
                        int remaining = inputBufferPos - tmpBufferSize;
                        if (remaining > 0)
                        {
                            Array.Copy(inputBuffer, tmpBufferSize, inputBuffer, 0, remaining);
                        }
                        inputBufferPos = remaining;
                    }
                }

                int processed = 0;
                while (running && processed + frameSize <= tmpBufferSize)
                {
                    try
                    {
                        byte[] encoded = encodeFrame(tmpBuffer, processed);
                        if (encoded.Length > 0)
                        {
                            encodedDataCallback?.onEncodedData(encoded);
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.error("Exception during encoding: " + e);
                    }
                    processed += frameSize;
                }

                Thread.Sleep(5);
            }
        }
    }
}
