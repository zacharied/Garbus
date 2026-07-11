// Tests for BackgroundSeekingClock: interactive seeks must not block the caller on the
// (potentially slow) source seek, and rapid seeks must coalesce so only the latest target is applied.
// Plain NUnit — no game host required.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Garbus.Game.Timing;
using NUnit.Framework;
using osu.Framework.Timing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestBackgroundSeekingClock
    {
        [Test]
        [Timeout(15000)]
        public void TestSeekDoesNotBlockCaller()
        {
            var source = new BlockingAdjustableClock();
            var clock = new BackgroundSeekingClock(source);

            // source.Seek blocks until released; a blocking wrapper would deadlock here (caught by Timeout).
            clock.Seek(50);

            // We returned without the underlying seek completing.
            Assert.That(source.CompletedSeeks, Is.EqualTo(0));

            source.ReleaseSeeks();
            waitUntil(() => source.CompletedSeeks == 1);
            Assert.That(source.CurrentTime, Is.EqualTo(50));
        }

        [Test]
        [Timeout(15000)]
        public void TestRapidSeeksCoalesceToLatestTarget()
        {
            var source = new BlockingAdjustableClock();
            var clock = new BackgroundSeekingClock(source);

            clock.Seek(1);
            // Wait until the worker is actually blocked inside source.Seek(1).
            waitUntil(() => source.SeekTargets.Count == 1);

            // Issued while (1) is in-flight: (2) is superseded by (3) before either can apply.
            clock.Seek(2);
            clock.Seek(3);

            source.ReleaseSeeks();
            waitUntil(() => source.CurrentTime == 3);

            Assert.That(source.SeekTargets, Does.Not.Contain(2.0));
            Assert.That(source.SeekTargets, Is.EqualTo(new[] { 1.0, 3.0 }));
            Assert.That(source.CompletedSeeks, Is.EqualTo(2));
        }

        [Test]
        [Timeout(15000)]
        public void TestSeekDoesNotRunOnThreadPoolThread()
        {
            // A real audio-track seek (TrackBass.Seek) blocks on GetResultSafely, which throws
            // "Can't use GetResultSafely from inside an async operation" when invoked on a TPL
            // thread-pool thread. The worker must therefore run on a dedicated (non-pool) thread.
            var source = new BlockingAdjustableClock();
            var clock = new BackgroundSeekingClock(source);

            clock.Seek(50);
            waitUntil(() => source.SeekTargets.Count == 1);
            source.ReleaseSeeks();
            waitUntil(() => source.CompletedSeeks == 1);

            Assert.That(source.SeekRanOnThreadPoolThread, Is.False,
                "The source seek must not run on a TPL thread-pool thread (would trip GetResultSafely).");
        }

        [Test]
        [Timeout(15000)]
        public void TestStartFlushesPendingSeekBeforeStartingSource()
        {
            var source = new BlockingAdjustableClock();
            var clock = new BackgroundSeekingClock(source);

            clock.Seek(200);
            waitUntil(() => source.SeekTargets.Count == 1);

            // Start must wait for the pending seek to land before starting the source,
            // otherwise playback would resume from the wrong position.
            var startTask = Task.Run(() => clock.Start());
            source.ReleaseSeeks();
            Assert.That(startTask.Wait(10000), Is.True);

            Assert.That(source.IsRunning, Is.True);
            Assert.That(source.TimeWhenStarted, Is.EqualTo(200));
        }

        private static void waitUntil(Func<bool> condition, int timeoutMs = 10000)
        {
            var sw = Stopwatch.StartNew();
            while (!condition())
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                    Assert.Fail("Condition was not met within the timeout.");
                Thread.Sleep(5);
            }
        }

        /// <summary>
        /// An <see cref="IAdjustableClock"/> whose <see cref="Seek"/> blocks until explicitly released,
        /// standing in for a slow audio-track seek that runs on another thread.
        /// </summary>
        private class BlockingAdjustableClock : IAdjustableClock
        {
            private readonly ManualResetEventSlim gate = new ManualResetEventSlim(false);
            private readonly object sync = new object();
            private readonly List<double> seekTargets = new List<double>();
            private int completedSeeks;
            private double currentTime;
            private volatile bool seekRanOnThreadPoolThread;

            public bool SeekRanOnThreadPoolThread => seekRanOnThreadPoolThread;

            public IReadOnlyList<double> SeekTargets
            {
                get
                {
                    lock (sync)
                        return seekTargets.ToArray();
                }
            }

            public int CompletedSeeks
            {
                get
                {
                    lock (sync)
                        return completedSeeks;
                }
            }

            public double CurrentTime
            {
                get
                {
                    lock (sync)
                        return currentTime;
                }
            }

            public double Rate { get; set; } = 1;
            public bool IsRunning { get; private set; }
            public double TimeWhenStarted { get; private set; } = double.NaN;

            public void ReleaseSeeks() => gate.Set();

            public bool Seek(double position)
            {
                if (Thread.CurrentThread.IsThreadPoolThread)
                    seekRanOnThreadPoolThread = true;

                lock (sync)
                    seekTargets.Add(position);

                gate.Wait();

                lock (sync)
                {
                    currentTime = position;
                    completedSeeks++;
                }

                return true;
            }

            public void Start()
            {
                TimeWhenStarted = CurrentTime;
                IsRunning = true;
            }

            public void Stop() => IsRunning = false;

            public void Reset()
            {
                IsRunning = false;
                lock (sync)
                    currentTime = 0;
            }

            public void ResetSpeedAdjustments()
            {
            }
        }
    }
}
