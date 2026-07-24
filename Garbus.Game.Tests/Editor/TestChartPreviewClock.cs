using System.Threading;
using Garbus.Game.Edit.Preview;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestChartPreviewClock
    {
        [Test]
        public void TestStoppedClockHoldsAuthoritativeTime()
        {
            var clock = new ChartPreviewClock(timestampFrequency: 1000);
            clock.Apply(new PreviewTransportState(2000, false, 1, 100));

            Assert.That(clock.CurrentTimeAt(1500), Is.EqualTo(2000));
        }

        [Test]
        public void TestRunningClockExtrapolatesFromSenderTimestamp()
        {
            var clock = new ChartPreviewClock(timestampFrequency: 1000);
            clock.Apply(new PreviewTransportState(1000, true, 1, 10_000));

            Assert.That(clock.CurrentTimeAt(10_250), Is.EqualTo(1250));
        }

        [Test]
        public void TestReceiveDelayDoesNotResetSenderTimestampAnchor()
        {
            var immediateClock = new ChartPreviewClock(timestampFrequency: 1000);
            var delayedClock = new ChartPreviewClock(timestampFrequency: 1000);
            var state = new PreviewTransportState(1000, true, 1, 10_000);
            const long queryTimestamp = 10_250;

            immediateClock.Apply(state);
            Thread.Sleep(50);
            delayedClock.Apply(state);

            Assert.Multiple(() =>
            {
                Assert.That(immediateClock.CurrentTimeAt(queryTimestamp), Is.EqualTo(1250));
                Assert.That(delayedClock.CurrentTimeAt(queryTimestamp), Is.EqualTo(1250));
            });
        }

        [Test]
        public void TestPauseSnapsToAuthoritativeTime()
        {
            var clock = new ChartPreviewClock(timestampFrequency: 1000);
            clock.Apply(new PreviewTransportState(5000, true, 1, 100));
            clock.Apply(new PreviewTransportState(2400, false, 1, 200));

            Assert.That(clock.CurrentTimeAt(5000), Is.EqualTo(2400));
        }

        [Test]
        public void TestBackwardsSeekSnapsImmediately()
        {
            var clock = new ChartPreviewClock(timestampFrequency: 1000);
            clock.Apply(new PreviewTransportState(5000, true, 1, 100));
            clock.Apply(new PreviewTransportState(1200, true, 1, 200));

            Assert.That(clock.CurrentTimeAt(200), Is.EqualTo(1200));
        }

        [Test]
        public void TestLaterStateReplacesClockState()
        {
            var clock = new ChartPreviewClock(timestampFrequency: 1000);
            clock.Apply(new PreviewTransportState(2000, true, 1.5, 500));
            clock.Apply(new PreviewTransportState(9000, false, 1, 1000));

            Assert.That(clock.CurrentTimeAt(1500), Is.EqualTo(9000));
        }
    }
}
