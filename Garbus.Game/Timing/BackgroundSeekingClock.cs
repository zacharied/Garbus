using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Timing;

namespace Garbus.Game.Timing
{
    /// <summary>
    /// Wraps an adjustable source clock (typically an audio <see cref="osu.Framework.Audio.Track.Track"/>) and makes
    /// <see cref="Seek"/> non-blocking. The source seek runs on a background worker, and rapid seeks coalesce so only
    /// the most recent target is ever applied.
    /// </summary>
    /// <remarks>
    /// A raw <c>Track.Seek</c> enqueues onto the audio thread and blocks the caller until it completes. During
    /// interactive scrubbing that seek is issued every update frame, so the update thread stalls waiting on the audio
    /// thread and stops producing frames (the draw thread then spin-waits). Because the decoupling clock above this one
    /// exposes its own seek target immediately, the audio track only needs to reach the final position — intermediate
    /// seeks can be dropped and applied asynchronously.
    /// </remarks>
    public sealed class BackgroundSeekingClock : IAdjustableClock
    {
        private readonly IAdjustableClock source;

        private readonly object sync = new object();
        private double? pendingTarget;
        private bool workerRunning;

        public BackgroundSeekingClock(IAdjustableClock source)
        {
            this.source = source;
        }

        public bool Seek(double position)
        {
            lock (sync)
            {
                pendingTarget = position;

                if (!workerRunning)
                {
                    workerRunning = true;
                    Task.Run(pump);
                }
            }

            return true;
        }

        private void pump()
        {
            while (true)
            {
                double target;

                lock (sync)
                {
                    if (pendingTarget == null)
                    {
                        workerRunning = false;
                        Monitor.PulseAll(sync);
                        return;
                    }

                    target = pendingTarget.Value;
                    pendingTarget = null;
                }

                source.Seek(target);

                lock (sync)
                    Monitor.PulseAll(sync);
            }
        }

        /// <summary>
        /// Blocks until every requested seek has been applied to the source.
        /// </summary>
        private void waitForSeeksToDrain()
        {
            lock (sync)
            {
                while (workerRunning || pendingTarget != null)
                    Monitor.Wait(sync);
            }
        }

        public void Start()
        {
            // The source must be at the latest sought position before playback resumes.
            waitForSeeksToDrain();
            source.Start();
        }

        public void Stop() => source.Stop();

        public void Reset()
        {
            lock (sync)
                pendingTarget = null;

            source.Reset();
        }

        public void ResetSpeedAdjustments() => source.ResetSpeedAdjustments();

        public double Rate
        {
            get => source.Rate;
            set => source.Rate = value;
        }

        public double CurrentTime => source.CurrentTime;

        public bool IsRunning => source.IsRunning;
    }
}
