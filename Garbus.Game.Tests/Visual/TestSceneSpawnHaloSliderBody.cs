// Pins how a slider body presents while part of it is still inside the spawn hold window, per
// docs/presentation-specs/Playfield.md ("Spawn halo and spawn phase"): an object whose span falls
// entirely inside the hold window renders as a stub at the halo before extending outward, and the
// portion still held collapses onto the emergence front rather than drawing its whole sweep.
//
// Calibration anchor — a 460x460 playfield less its 30px padding gives the scrolling container
// 400x400, so ScrollLength is 200. Parameters are chosen for exact arithmetic, deliberately not the
// production defaults:
//   SpawnHaloFraction 0.25, TimeRange 800 ms, SpawnDuration 100 ms
// Hand-derived from the spec's formulas:
//   haloRadius = 200 * 0.25       =  50 px
//   travelTime = 800 * (1 - 0.25) = 600 ms
//   leadTime   = 600 + 100        = 700 ms
//   velocity   = 200 / 800        = 0.25 px/ms
//
// The subject slider has its head at 10000 on angle 0 and one control point 400 ms later at angle 90.
// From those four numbers and the radius map:
//   spawn instant     9300  (10000 - leadTime)
//   head leaves halo  9400  (10000 - travelTime)
//   tail leaves halo  9800  (10400 - travelTime)

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSpawnHaloSliderBody : GarbusTestScene
    {
        private const double head_time = 10_000;
        private const double tail_offset = 400;

        protected override double TimePerAction => 0;

        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        private readonly ManualClock manualClock = new ManualClock { Rate = 0 };

        private GarbusPlayfield playfield = null!;
        private Objects.Drawables.DrawableSliderBody body = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("set scroll parameters", () =>
            {
                scrollingInfo.TimeRange.Value = 800;
                scrollingInfo.SpawnHaloFraction.Value = 0.25;
                scrollingInfo.SpawnDuration.Value = 100;
            });

            AddStep("build playfield with slider", () =>
            {
                var slider = new SliderBody
                {
                    StartTime = head_time,
                    AngleDeg = 0,
                    Side = HorizontalDirection.Left,
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>
                        {
                            new GarbusPathControlPoint { TimeOffset = tail_offset, RotationOffset = 90 },
                        },
                    },
                };
                slider.ApplyDefaults();

                // Park the clock before the slider exists so the drawable applies with the parameters above.
                manualClock.CurrentTime = head_time - 2000;

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = new GarbusInputManager
                    {
                        Child = playfield = new GarbusPlayfield
                        {
                            RelativeSizeAxes = Axes.None,
                            Size = new Vector2(460),
                        },
                    },
                };

                playfield.Add(PlayScreen.CreateDrawableRepresentation(slider));
            });

            AddUntilStep("scroll length 200", () => Precision.AlmostEquals(scrollingContainer().ScrollLength, 200, 0.001));

            AddUntilStep("slider body present", () =>
            {
                body = playfield.AllHitObjects.OfType<Objects.Drawables.DrawableSliderBody>().FirstOrDefault()!;
                return body != null;
            });
        }

        private GarbusScrollingHitObjectContainer scrollingContainer()
            => playfield.ChildrenOfType<GarbusScrollingHitObjectContainer>().First();

        private void seek(double time) => AddStep($"seek {time}", () => manualClock.CurrentTime = time);

        // Path vertices live in polar-origin-centred space (vertex (0,0) is the playfield centre), and
        // polarToCartesian maps (theta, r) to (cos * r, -sin * r) — so a vertex decodes straight back.
        private List<Vector2> drawnVertices()
            => body.BodyPaths.Where(p => p.Vertices.Count >= 2).SelectMany(p => p.Vertices).ToList();

        private static float radiusOf(Vector2 v) => v.Length;

        private static float angleOf(Vector2 v)
        {
            float deg = MathF.Atan2(-v.Y, v.X) * 180f / MathF.PI;
            return deg < 0 ? deg + 360 : deg;
        }

        /// <summary>
        /// Through the hold window the whole span maps to haloRadius, so the body draws no sweep at all —
        /// it presents as the stub the spec calls for, sitting on the halo at the head's own angle.
        /// </summary>
        [Test]
        public void TestBodyIsAStubOnTheHaloThroughTheHoldWindow()
        {
            // 9300 is the spawn instant, 9350 is mid-hold; the head does not leave the halo until 9400.
            seek(9350);

            AddAssert("no sweep is drawn", () => drawnVertices().Count == 0);
            AddAssert("stub disc is visible", () => body.HeadGlow.Alpha > 0 && body.HeadGlow.Vertices.Count >= 2);
            AddAssert("stub sits on the halo", () => Precision.AlmostEquals(radiusOf(body.HeadGlow.Vertices[0]), 50, 0.5));
            AddAssert("stub sits at the head's angle", () => Precision.AlmostEquals(angleOf(body.HeadGlow.Vertices[0]), 0, 0.5));
        }

        /// <summary>
        /// Once the head has emerged but the tail has not, only the emerged portion draws. The sweep must
        /// stop at the emergence front — it must not run on to the held tail's angle — and its inner end
        /// sits exactly on the halo.
        /// </summary>
        [Test]
        public void TestHeldPortionCollapsesOntoTheEmergenceFront()
        {
            // At 9600 the head is 400 ms out (radius 200 - 400 * 0.25 = 100) so it has emerged, while the
            // tail is 800 ms out — still beyond travelTime, so still held. The emergence front is the path
            // point at 9600 + 600 = 10200, i.e. halfway along the 400 ms link, at angle 45.
            seek(9600);

            AddAssert("a sweep is drawn", () => drawnVertices().Count >= 2);
            AddAssert("inner end sits on the halo", () => Precision.AlmostEquals(drawnVertices().Min(radiusOf), 50, 0.5));
            AddAssert("outer end is the head", () => Precision.AlmostEquals(drawnVertices().Max(radiusOf), 100, 0.5));

            // The discriminating assertion: the held tail's 90 degrees must not be drawn. The sweep stops
            // at the front's 45.
            AddAssert("sweep starts at the head's angle", () => Precision.AlmostEquals(drawnVertices().Min(angleOf), 0, 0.5));
            AddAssert("sweep stops at the emergence front, short of the held tail", () => drawnVertices().Max(angleOf) < 70);
        }

        /// <summary>
        /// Once both nodes have left the halo the body draws its whole sweep, and its inner end is no
        /// longer pinned to the halo — it rides the radius map like any other point.
        /// </summary>
        [Test]
        public void TestFullSweepDrawsOnceBothNodesHaveEmerged()
        {
            // At 9900 the head is 100 ms out (radius 175) and the tail 500 ms out (radius 200 - 125 = 75);
            // the tail left the halo at 9800, so the entire link is on the ramp.
            seek(9900);

            AddAssert("a sweep is drawn", () => drawnVertices().Count >= 2);
            AddAssert("sweep reaches the tail's angle", () => Precision.AlmostEquals(drawnVertices().Max(angleOf), 90, 1.0));
            AddAssert("outer end is the head", () => Precision.AlmostEquals(drawnVertices().Max(radiusOf), 175, 0.5));
            AddAssert("inner end is the tail, clear of the halo", () => Precision.AlmostEquals(drawnVertices().Min(radiusOf), 75, 0.5));
        }
    }
}
