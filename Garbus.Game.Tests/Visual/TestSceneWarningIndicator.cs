using Garbus.Game.Core;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneWarningIndicator : GarbusTestScene
    {
        // Driven entirely by a ManualClock — pace steps per-frame (see TestSceneGameplay).
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private WarningIndicatorDisplay display = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create display", () =>
            {
                manualClock = new ManualClock { Rate = 1 };
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = display = new WarningIndicatorDisplay(),
                };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
        }

        [Test]
        public void TestSliderWarningRevealsInWindow()
        {
            AddStep("set objects", () => display.SetHitObjects(new GarbusHitObject[]
            {
                new SliderBody
                {
                    AngleDeg = 90,
                    Side = HorizontalDirection.Left,
                    StartTime = 5000,
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>
                        {
                            new GarbusPathControlPoint { TimeOffset = 200, RotationOffset = 0 },
                        },
                    },
                },
            }));

            AddStep("seek before window", () => manualClock.CurrentTime = 4000);
            AddUntilStep("hidden", () => display.RevealedAngleDeg(HorizontalDirection.Left) == null);

            AddStep("seek into window", () => manualClock.CurrentTime = 4700);
            AddUntilStep("revealed at 90", () => display.RevealedAngleDeg(HorizontalDirection.Left) == 90);

            AddAssert("no right warning", () => display.RevealedAngleDeg(HorizontalDirection.Right) == null);

            AddStep("seek past start", () => manualClock.CurrentTime = 5200);
            AddUntilStep("hidden again", () => display.RevealedAngleDeg(HorizontalDirection.Left) == null);
        }

        [Test]
        public void TestPlayfieldForwardsWarnings()
        {
            GarbusPlayfield playfield = null!;

            AddStep("create playfield", () =>
            {
                manualClock = new ManualClock { Rate = 1 };
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = new GarbusInputManager
                    {
                        Child = playfield = new GarbusPlayfield { Size = Vector2.One },
                    },
                };
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);

            AddStep("hand over a left slider at 5000", () => playfield.SetHitObjects(new GarbusHitObject[]
            {
                new SliderBody
                {
                    AngleDeg = 90,
                    Side = HorizontalDirection.Left,
                    StartTime = 5000,
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>
                        {
                            new GarbusPathControlPoint { TimeOffset = 200, RotationOffset = 0 },
                        },
                    },
                },
            }));

            AddStep("seek into window", () => manualClock.CurrentTime = 4700);
            AddUntilStep("warning revealed", () => playfield.WarningIndicators.RevealedAngleDeg(HorizontalDirection.Left) == 90);
        }
    }
}
