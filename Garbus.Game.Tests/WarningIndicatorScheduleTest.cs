using Garbus.Game.Core;
using Garbus.Game.Objects;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Bindables;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class WarningIndicatorScheduleTest
    {
        private const double warning_time = 600;

        private static GarbusSlamCentered Slam(HorizontalDirection side, int angle, double start)
            => new GarbusSlamCentered { AngleDeg = angle, Side = side, StartTime = start };

        private static SliderBody Slider(HorizontalDirection side, int angle, double start, double duration)
            => new SliderBody
            {
                AngleDeg = angle,
                Side = side,
                StartTime = start,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = duration, RotationOffset = 0 },
                    },
                },
            };

        [Test]
        public void IsolatedSliderIsEligibleWithinWindow()
        {
            var schedule = new WarningIndicatorSchedule(
                new GarbusHitObject[] { Slider(HorizontalDirection.Left, 90, 5000, 200) }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4300), Is.Null);              // before window
            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4600)?.AngleDeg, Is.EqualTo(90)); // in window
            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4999)?.AngleDeg, Is.EqualTo(90));
            Assert.That(schedule.Revealed(HorizontalDirection.Left, 5000), Is.Null);              // at start: hidden
        }

        [Test]
        public void SlamIsNotIndicated()
        {
            // A SlamCentered occupies the stick (it counts for the gap rule) but is never itself telegraphed.
            var schedule = new WarningIndicatorSchedule(
                new GarbusHitObject[] { Slam(HorizontalDirection.Left, 90, 5000) }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4600), Is.Null);
            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4999), Is.Null);
        }

        [Test]
        public void CloseSameSidePriorSuppressesWarning()
        {
            // A slam at 5000 keeps the stick busy through 5000; the slider head at 5300 is only 300ms later
            // (< 600) → the slider is not telegraphed. The slam itself is never telegraphed either.
            var schedule = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                Slam(HorizontalDirection.Left, 90, 5000),
                Slider(HorizontalDirection.Left, 180, 5300, 200),
            }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4700), Is.Null); // slam not indicated
            Assert.That(schedule.Revealed(HorizontalDirection.Left, 5100), Is.Null); // slider suppressed
        }

        [Test]
        public void DistantSameSidePriorIsEligible()
        {
            var schedule = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                Slam(HorizontalDirection.Left, 90, 5000),
                Slider(HorizontalDirection.Left, 180, 6000, 200), // head 1000ms after the slam (> 600)
            }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Left, 5500)?.AngleDeg, Is.EqualTo(180));
        }

        [Test]
        public void SliderGapMeasuredFromEndTime()
        {
            // Slider A occupies 5000..5800. Slider B's head at 6200 is 1200ms after A's START but only 400ms
            // after A ENDS (< 600) → B is NOT eligible.
            var schedule = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                Slider(HorizontalDirection.Left, 0, 5000, 800),
                Slider(HorizontalDirection.Left, 90, 6200, 200),
            }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Left, 5900), Is.Null);
        }

        [Test]
        public void OppositeSidesAreIndependent()
        {
            var schedule = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                Slider(HorizontalDirection.Left, 90, 5000, 200),
                Slider(HorizontalDirection.Right, 270, 5200, 200),
            }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Right, 4900)?.AngleDeg, Is.EqualTo(270));
        }

        [Test]
        public void SlamEdgeCountsAsStickObject()
        {
            // A SlamEdge occupies the stick through 5000; a slider head only 300ms later (< 600) is suppressed.
            var suppressed = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                new GarbusSlamEdge { AngleDeg = 90, Side = HorizontalDirection.Left, StartTime = 5000 },
                Slider(HorizontalDirection.Left, 180, 5300, 200),
            }, warning_time);

            Assert.That(suppressed.Revealed(HorizontalDirection.Left, 5100), Is.Null);

            // A slider head 1000ms after the SlamEdge (> 600) IS telegraphed.
            var eligible = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                new GarbusSlamEdge { AngleDeg = 90, Side = HorizontalDirection.Left, StartTime = 5000 },
                Slider(HorizontalDirection.Left, 270, 6000, 200),
            }, warning_time);

            Assert.That(eligible.Revealed(HorizontalDirection.Left, 5500)?.AngleDeg, Is.EqualTo(270));
        }

        [Test]
        public void RevealWindowAndGapBoundariesAreExact()
        {
            // Reveal lower bound is inclusive: at exactly StartTime - WarningTime the slider shows.
            var isolated = new WarningIndicatorSchedule(
                new GarbusHitObject[] { Slider(HorizontalDirection.Left, 90, 5000, 200) }, warning_time);
            Assert.That(isolated.Revealed(HorizontalDirection.Left, 4400)?.AngleDeg, Is.EqualTo(90));

            // Gap of exactly WarningTime is NOT eligible (rule is strict >): slam ends 5000, slider head at 5600 → gap 600.
            var boundary = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                Slam(HorizontalDirection.Left, 0, 5000),
                Slider(HorizontalDirection.Left, 180, 5600, 200),
            }, warning_time);
            Assert.That(boundary.Revealed(HorizontalDirection.Left, 5100), Is.Null);
        }
    }
}
