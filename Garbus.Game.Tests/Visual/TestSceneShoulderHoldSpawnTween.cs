// The shoulder hold note's head shares the two-square-plus-arc spawn of the tap ShoulderNote (see
// TestSceneSpawnTween): the squares grow in place and the arc halves grow out of each square, meeting
// only as the squares reach full scale. Same calibration anchor — TimeRange 800 / SpawnHaloFraction
// 0.25 give travelTime 600, so a note at StartTime 10000 leaves the halo at 9400 whatever SpawnDuration
// is, and leadTime = 600 + SpawnDuration moves the spawn instant to match.

using Garbus.Game.Core;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneShoulderHoldSpawnTween : GarbusTestScene
    {
        private const double note_start_time = 10_000;

        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        private readonly ManualClock manualClock = new ManualClock { Rate = 0 };

        private DrawableShoulderHoldNote drawable = null!;

        private void setUpScene(double spawnDuration)
        {
            AddStep($"scroll parameters, spawn {spawnDuration}ms", () =>
            {
                scrollingInfo.TimeRange.Value = 800;
                scrollingInfo.SpawnHaloFraction.Value = 0.25;
                scrollingInfo.SpawnDuration.Value = spawnDuration;
            });

            AddStep("build playfield", () =>
            {
                var note = new ShoulderHoldNote { StartTime = note_start_time, Duration = 1000, Side = HorizontalDirection.Right };
                note.ApplyDefaults();

                // Park the clock before the note exists so the drawable applies with the parameters above.
                manualClock.CurrentTime = note_start_time - 2000;

                GarbusPlayfield playfield;

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = new GarbusInputManager
                    {
                        Child = playfield = new GarbusPlayfield { Size = Vector2.One },
                    },
                };

                playfield.Add(drawable = (DrawableShoulderHoldNote)PlayScreen.CreateDrawableRepresentation(note));
            });
        }

        private void seek(double time) => AddStep($"seek {time}", () => manualClock.CurrentTime = time);

        [Test]
        public void TestHeadSquaresReachFullScaleWhenMotionBegins()
        {
            setUpScene(100);

            // leadTime = 600 + 100 = 700, so the note appears at 10000 - 700 = 9300.
            seek(9300);
            AddAssert("squares start from nothing", () => Precision.AlmostEquals(drawable.SpawnSquareScale, 0, 0.01));

            seek(9350);
            AddAssert("squares growing mid-hold", () => drawable.SpawnSquareScale > 0 && drawable.SpawnSquareScale < 1);

            // travelTime = 600, so motion begins at 10000 - 600 = 9400 — and the tween ends there.
            seek(9400);
            AddAssert("squares full scale as motion begins", () => Precision.AlmostEquals(drawable.SpawnSquareScale, 1, 0.01));
        }

        [Test]
        public void TestHeadArcHalvesCollapsedAtSpawnAndMeetOnMotion()
        {
            setUpScene(100);

            seek(9300);
            AddAssert("arc collapsed at spawn", () => Precision.AlmostEquals(drawable.SpawnArcSpanDeg, 0, 0.5));

            seek(9350);
            AddAssert("arc growing but not met", () => drawable.SpawnArcSpanDeg > 0 && drawable.SpawnArcSpanDeg < 90);

            seek(9400);
            AddAssert("arc halves meet as motion begins", () => Precision.AlmostEquals(drawable.SpawnArcSpanDeg, 90, 0.5));
        }
    }
}
