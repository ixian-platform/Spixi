
namespace SPIXI.VoIP
{
    public static class CodecTools
    {
        public static int getPcmFrameByteSize(int samples, int bitsPerSample, int channels)
        {
            return channels * (bitsPerSample / 8) * samples / 1000;
        }

        public static int getPcmFrameSize(int sampleRate, int frameDurationMs)
        {
            return sampleRate / 1000 * frameDurationMs;
        }
    }
}
