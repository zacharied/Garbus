// Pins the coupling specified in docs/presentation-specs/Playfield.md ("Spawn halo and spawn phase"):
// the spawn animation's duration and the motionless hold are one quantity, so the tween reaches full
// scale exactly when the object starts moving — never still growing while it moves, never fully grown
// while still.
//
// Calibration anchor — SpawnHaloFraction 0.25 and TimeRange 800 ms give travelTime = 800 * 0.75 =
// 600 ms, so a note at StartTime 10000 leaves the halo at t = 9400 whatever SpawnDuration is. The
// spawn instant moves with it: leadTime = 600 + SpawnDuration, so 100 ms spawns at 9300 and 300 ms
// spawns at 9100, and both must land full scale on 9400.
//
// The subject is a shoulder note: its two squares grow in place (SpawnSquareScale) and its arc halves
// grow out of each square, meeting only once the squares are full (SpawnArcSpanDeg reaching 90°).

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
    public partial class TestSceneSpawnTween : GarbusTestScene
    {
        private const double note_start_time = 10_000;

        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        private readonly ManualClock manualClock = new ManualClock { Rate = 0 };

        private DrawableShoulderNote drawable = null!;

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
                var note = new ShoulderNote { StartTime = note_start_time, Side = HorizontalDirection.Right };
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

                playfield.Add(drawable = (DrawableShoulderNote)PlayScreen.CreateDrawableRepresentation(note));
            });
        }

        private void seek(double time) => AddStep($"seek {time}", () => manualClock.CurrentTime = time);

        [Test]
        public void TestSquaresReachFullScaleWhenMotionBegins()
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
        public void TestLongerSpawnDurationMovesSpawnEarlierAndStillLandsOnMotion()
        {
            setUpScene(300);

            // leadTime = 600 + 300 = 900, so the note appears at 10000 - 900 = 9100.
            seek(9100);
            AddAssert("squares start from nothing", () => Precision.AlmostEquals(drawable.SpawnSquareScale, 0, 0.01));

            // 9300 is the spawn instant of the 100 ms case; with a 300 ms tween it is only partway.
            seek(9300);
            AddAssert("squares still growing where the short tween would have started", () => drawable.SpawnSquareScale > 0 && drawable.SpawnSquareScale < 1);

            // Motion still begins at 9400, so the longer tween must still land exactly there.
            seek(9400);
            AddAssert("squares full scale as motion begins", () => Precision.AlmostEquals(drawable.SpawnSquareScale, 1, 0.01));
        }

        [Test]
        public void TestArcHalvesCollapsedAtSpawnAndMeetOnMotion()
        {
            setUpScene(100);

            // At the spawn instant the two half-arcs have no span: each sits collapsed on its square.
            seek(9300);
            AddAssert("arc collapsed at spawn", () => Precision.AlmostEquals(drawable.SpawnArcSpanDeg, 0, 0.5));

            // Mid-hold the halves have grown but not yet met (combined span below the full 90°).
            seek(9350);
            AddAssert("arc growing but not met", () => drawable.SpawnArcSpanDeg > 0 && drawable.SpawnArcSpanDeg < 90);

            // As motion begins the halves meet: together they cover the full ±45° (90°).
            seek(9400);
            AddAssert("arc halves meet as motion begins", () => Precision.AlmostEquals(drawable.SpawnArcSpanDeg, 90, 0.5));
        }
    }
}
