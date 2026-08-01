// Pins the spawn-halo radius map specified in docs/presentation-specs/Playfield.md ("Spawn halo and
// spawn phase").
//
// Calibration anchor — a 400x400 container gives ScrollLength 200; the scrolling parameters are set
// to values chosen for exact arithmetic, deliberately not the production defaults:
//   SpawnHaloFraction 0.25, TimeRange 800 ms, SpawnDuration 100 ms
// Hand-derived from the spec's formulas:
//   haloRadius = 200 * 0.25       =  50 px
//   travelTime = 800 * (1 - 0.25) = 600 ms
//   leadTime   = 600 + 100        = 700 ms
//   velocity   = 200 / 800        = 0.25 px/ms
// Every expected value below is derived by hand from those four numbers.

using Garbus.Game.Gameplay.Objects; // HitObjectLifetimeEntry
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Objects;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSpawnHalo : GarbusTestScene
    {
        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        private GarbusScrollingHitObjectContainer container = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            // Set TimeRange on the scrolling info directly rather than through the ScrollSpeed
            // config: the config->TimeRange binding only fires on config change, so a direct write
            // holds, and 800 ms is not reachable from the tenths-snapped speed slider anyway.
            AddStep("set scroll parameters", () =>
            {
                scrollingInfo.TimeRange.Value = 800;
                scrollingInfo.SpawnHaloFraction.Value = 0.25;
                scrollingInfo.SpawnDuration.Value = 100;
            });

            AddStep("create container", () => Child = container = new GarbusScrollingHitObjectContainer
            {
                RelativeSizeAxes = Axes.None,
                Size = new Vector2(400),
            });

            AddUntilStep("scroll length 200", () => Precision.AlmostEquals(container.ScrollLength, 200, 0.001));
        }

        // Δ is passed as `time` with currentTime 0, so `time` reads directly as time-until-ring.
        private float radiusAt(double delta) => container.ProgressAtTime(delta, 0);

        private float unclampedRadiusAt(double delta) => container.DistanceFromCentreAtTime(delta, 0);

        [Test]
        public void TestRadiusHoldsAtHaloThroughSpawnWindowThenLeaves()
        {
            AddAssert("halo at spawn (delta 700)", () => Precision.AlmostEquals(radiusAt(700), 50, 0.001));
            AddAssert("halo mid-hold (delta 650)", () => Precision.AlmostEquals(radiusAt(650), 50, 0.001));
            AddAssert("halo at travel start (delta 600)", () => Precision.AlmostEquals(radiusAt(600), 50, 0.001));
            // 200 - 599 * 0.25 = 50.25 — one millisecond past the boundary the ramp has taken over,
            // and it takes over exactly at the halo, with no seam.
            AddAssert("just past travel start (delta 599)", () => Precision.AlmostEquals(radiusAt(599), 50.25, 0.001));
        }

        [Test]
        public void TestTravelPhaseReachesRingAtStartTime()
        {
            // Halfway through the 600 ms travel, halfway between halo and ring: (50 + 200) / 2.
            AddAssert("midway (delta 300)", () => Precision.AlmostEquals(radiusAt(300), 125, 0.001));
            AddAssert("ring at start time (delta 0)", () => Precision.AlmostEquals(radiusAt(0), 200, 0.001));
        }

        [Test]
        public void TestDistanceFromCentreExtrapolatesPastRingWhileProgressClamps()
        {
            // 100 ms after the ring, at 0.25 px/ms: 200 + 25 = 225. The unclamped accessor keeps
            // extrapolating so callers can clip what the outer edge has consumed.
            AddAssert("unclamped overshoots (delta -100)", () => Precision.AlmostEquals(unclampedRadiusAt(-100), 225, 0.001));
            AddAssert("clamped pins at ring (delta -100)", () => Precision.AlmostEquals(radiusAt(-100), 200, 0.001));

            // The halo floor must live in DistanceFromCentreAtTime itself, not merely fall out of
            // ProgressAtTime's clamp — DrawableCardinalHoldNote/DrawableSliderBody read the unclamped
            // accessor directly to stub a duration object at the halo while it's inside the hold
            // window. delta 700 = leadTime, still inside the hold: unclamped must also read haloRadius.
            AddAssert("unclamped floors at halo during the hold (delta 700)", () => Precision.AlmostEquals(unclampedRadiusAt(700), 50, 0.001));
        }

        [Test]
        public void TestRadialVelocityIsScrollLengthOverTimeRange()
        {
            // 200 - 200 * 0.25 = 150 and 200 - 100 * 0.25 = 175: 25 px covered in 100 ms.
            AddAssert("radius at delta 200", () => Precision.AlmostEquals(radiusAt(200), 150, 0.001));
            AddAssert("radius at delta 100", () => Precision.AlmostEquals(radiusAt(100), 175, 0.001));
            AddAssert("0.25 px per ms", () => Precision.AlmostEquals((radiusAt(100) - radiusAt(200)) / 100, 0.25, 0.001));
        }

        [Test]
        public void TestHoldWindowIsInvariantToTimeRange()
        {
            // Halving TimeRange to 400 halves travelTime to 300 ms and doubles velocity to 0.5 px/ms,
            // but SpawnDuration is a fixed constant, so the hold stays 100 ms: leadTime 400,
            // travel start still 100 ms after spawn. haloRadius is unchanged at 200 * 0.25 = 50.
            AddStep("halve time range", () => scrollingInfo.TimeRange.Value = 400);

            AddAssert("halo at spawn (delta 400)", () => Precision.AlmostEquals(radiusAt(400), 50, 0.001));
            AddAssert("halo at travel start (delta 300)", () => Precision.AlmostEquals(radiusAt(300), 50, 0.001));
            // 200 - 299 * 0.5 = 50.5
            AddAssert("just past travel start (delta 299)", () => Precision.AlmostEquals(radiusAt(299), 50.5, 0.001));
        }

        [Test]
        public void TestEntryLifetimeStartsAtLeadTime()
        {
            HitObjectLifetimeEntry entry = null!;

            AddStep("add note entry", () =>
            {
                var note = new CardinalNote { StartTime = 10_000, AngleDeg = 0 };
                note.ApplyDefaults();
                container.Add(entry = new HitObjectLifetimeEntry(note));
            });

            // 10000 - 700. A cardinal note's interaction lead is its 200 ms early-miss window
            // (CardinalNoteHitWindows.early_miss_window), so leadTime is the larger of the two and
            // decides when the entry goes alive.
            AddAssert("alive from 9300", () => Precision.AlmostEquals(entry.LifetimeStart, 9300, 0.001));
        }
    }
}
