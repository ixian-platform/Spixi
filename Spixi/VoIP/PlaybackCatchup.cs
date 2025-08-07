namespace Spixi.VoIP
{
    public enum PlaybackCatchupType
    {
        Normal,
        SpeedUp,
        Drop
    }

    public class PlaybackCatchup
    {
        public PlaybackCatchupType Type { get; }
        public float Speed { get; } // only used for SpeedUp

        private PlaybackCatchup(PlaybackCatchupType type, float speed = 1.0f)
        {
            Type = type;
            Speed = speed;
        }

        public static PlaybackCatchup Normal() => new PlaybackCatchup(PlaybackCatchupType.Normal, 1.0f);
        public static PlaybackCatchup SpeedUp(float speed) => new PlaybackCatchup(PlaybackCatchupType.SpeedUp, speed);
        public static PlaybackCatchup Drop(float speed) => new PlaybackCatchup(PlaybackCatchupType.Drop, speed);
    }

    /// <summary>
    /// Adaptive playback controller for VoIP.
    /// Keeps average buffer latency within safe limits by dynamically
    /// switching between normal playback, slight speed-up, or dropping.
    /// </summary>
    public class PlaybackCatchupController
    {
        private readonly Queue<double> latencyHistory = new();
        private readonly int historySize;
        private double avgQueuedSeconds = 0;

        // thresholds
        public double DropThreshold = 0.50;   // > 500ms
        public double SpeedThreshold = 0.25; // > 250ms
        private const float MaxSpeedup = 1.15f;     // max 15% faster
        private const float MinSpeedup = 1.05f;     // min 5% faster

        // optional smoothing
        private readonly double smoothingFactor;
        private double smoothedLatency = 0;

        public PlaybackCatchupController(int historySize = 20, double smoothingFactor = 0.3)
        {
            this.historySize = historySize;
            this.smoothingFactor = Math.Clamp(smoothingFactor, 0.0, 1.0);
        }

        /// <summary>
        /// Call every time new audio is queued. 
        /// queuedSeconds = how much audio is waiting in seconds.
        /// </summary>
        public PlaybackCatchup Update(double queuedSeconds)
        {
            // Store for average
            latencyHistory.Enqueue(queuedSeconds);
            if (latencyHistory.Count > historySize) latencyHistory.Dequeue();
            avgQueuedSeconds = latencyHistory.Average();

            // Smooth with EMA (reacts faster than plain average)
            smoothedLatency = (smoothingFactor * queuedSeconds) +
                              ((1 - smoothingFactor) * smoothedLatency);

            double latency = smoothedLatency;

            if (latency > DropThreshold)
            {
                return PlaybackCatchup.Drop(MaxSpeedup);
            }
            else if (latency > SpeedThreshold)
            {
                double excess = latency - SpeedThreshold;
                double factor = Math.Min(excess / (DropThreshold - SpeedThreshold), 1.0);
                float speed = (float)(MinSpeedup + factor * (MaxSpeedup - MinSpeedup));
                return PlaybackCatchup.SpeedUp(speed);
            }
            else
            {
                return PlaybackCatchup.Normal();
            }
        }

        public double GetAverageLatency() => avgQueuedSeconds;
        public double GetSmoothedLatency() => smoothedLatency;
    }
}
