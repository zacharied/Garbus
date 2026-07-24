using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
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
            // Derive seeks from the tunable window so the test survives WARNING_TIME changes.
            const double start = 5000;
            double warning = WarningIndicatorDisplay.WARNING_TIME;

            AddStep("set objects", () => display.SetHitObjects(new GarbusHitObject[]
            {
                new SliderBody
                {
                    AngleDeg = 90,
                    Side = HorizontalDirection.Left,
                    StartTime = start,
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>
                        {
                            new GarbusPathControlPoint { TimeOffset = 200, RotationOffset = 0 },
                        },
                    },
                },
            }));

            AddStep("seek before window", () => manualClock.CurrentTime = start - warning - 500);
            AddUntilStep("hidden", () => display.RevealedAngleDeg(HorizontalDirection.Left) == null);

            AddStep("seek into window", () => manualClock.CurrentTime = start - warning / 2);
            AddUntilStep("revealed at 90", () => display.RevealedAngleDeg(HorizontalDirection.Left) == 90);

            AddAssert("no right warning", () => display.RevealedAngleDeg(HorizontalDirection.Right) == null);

            AddStep("seek past start", () => manualClock.CurrentTime = start + 200);
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

        /// <summary>
        /// The default (gameplay) display draws the blurred under-ring glow — a <see cref="BufferedContainer"/>
        /// per side blurs the arc and a <see cref="Circle"/> masks the disc interior. The mini-preview style
        /// (<see cref="MiniWarningIndicatorDisplay"/>) instead draws a plain, unblurred <see cref="Arc"/> of the
        /// side's colour directly on the ring — so it has neither the blur buffers nor the clip mask.
        /// </summary>
        [Test]
        public void TestMiniStyleDrawsPlainArcNotBlurredGlow()
        {
            WarningIndicatorDisplay glow = null!;
            WarningIndicatorDisplay mini = null!;

            AddStep("create both displays", () =>
            {
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        glow = new WarningIndicatorDisplay(),
                        mini = new MiniWarningIndicatorDisplay(),
                    },
                };
            });

            AddUntilStep("both loaded", () => glow.IsLoaded && mini.IsLoaded);

            // Gameplay glow: blurred + clipped, so buffers and a mask circle exist.
            AddAssert("glow uses blur buffers", () => glow.ChildrenOfType<BufferedContainer>().Any());
            AddAssert("glow uses a clip mask", () => glow.ChildrenOfType<Circle>().Any());

            // Mini: a plain arc per side, no blur buffer and no clip mask.
            AddAssert("mini has no blur buffer", () => !mini.ChildrenOfType<BufferedContainer>().Any());
            AddAssert("mini has no clip mask", () => !mini.ChildrenOfType<Circle>().Any());
            AddAssert("mini draws one arc per side", () => mini.ChildrenOfType<Arc>().Count() == 2);
        }

        [Test]
        public void TestMiniStylePlayfieldUsesMiniDisplay()
        {
            GarbusPlayfield gameplay = null!;
            GarbusPlayfield preview = null!;

            AddStep("create gameplay and mini-style playfields", () =>
            {
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        gameplay = new GarbusPlayfield { Size = Vector2.One },
                        preview = new GarbusPlayfield(interactive: false, miniStyle: true) { Size = Vector2.One },
                    },
                };
            });

            AddUntilStep("both loaded", () => gameplay.IsLoaded && preview.IsLoaded);

            AddAssert("gameplay uses the blurred glow display",
                () => gameplay.WarningIndicators is not MiniWarningIndicatorDisplay);
            AddAssert("mini-style playfield uses the mini display",
                () => preview.WarningIndicators is MiniWarningIndicatorDisplay);
        }

        /// <summary>
        /// Parks both warning glows on a real playfield (ring visible) and keeps them revealed indefinitely,
        /// so the appearance constants in <see cref="WarningIndicatorDisplay"/> can be tuned live in the visual
        /// browser. Runs on the ambient clock (not a frozen ManualClock) so the fade-in actually plays; a
        /// per-frame driver re-feeds two sliders whose StartTime stays just inside the reveal window.
        /// </summary>
        [Test]
        public void TestTuneBothWarnings()
        {
            GarbusPlayfield playfield = null!;
            Container root = null!;

            AddStep("create playfield", () =>
            {
                Child = root = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new GarbusInputManager
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = playfield = new GarbusPlayfield { Size = Vector2.One },
                    },
                };
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);

            // Left glow on the screen-left of the ring (θ = 180°), right glow on the screen-right (θ = 0°).
            AddStep("drive both warnings", () => root.Add(new RevealDriver(playfield,
                (HorizontalDirection.Left, 180),
                (HorizontalDirection.Right, 0))));

            AddUntilStep("both revealed", () =>
                playfield.WarningIndicators.RevealedAngleDeg(HorizontalDirection.Left) == 180 &&
                playfield.WarningIndicators.RevealedAngleDeg(HorizontalDirection.Right) == 0);
        }

        /// <summary>
        /// Re-feeds the playfield two isolated sliders every frame with a StartTime a fixed lead ahead of the
        /// current time, keeping both warnings inside the reveal window so the glows stay on screen for tuning.
        /// The angle never changes, so the fade-in only fires once.
        /// </summary>
        private partial class RevealDriver : Component
        {
            private const double lead = 1000;

            private readonly GarbusPlayfield playfield;
            private readonly (HorizontalDirection side, int angle)[] objects;

            public RevealDriver(GarbusPlayfield playfield, params (HorizontalDirection, int)[] objects)
            {
                this.playfield = playfield;
                this.objects = objects;
            }

            protected override void Update()
            {
                base.Update();

                var sliders = new GarbusHitObject[objects.Length];
                for (int i = 0; i < objects.Length; i++)
                {
                    sliders[i] = new SliderBody
                    {
                        Side = objects[i].side,
                        AngleDeg = objects[i].angle,
                        StartTime = Time.Current + lead,
                        Path = new GarbusPath
                        {
                            ControlPoints = new BindableList<GarbusPathControlPoint>
                            {
                                new GarbusPathControlPoint { TimeOffset = 200, RotationOffset = 0 },
                            },
                        },
                    };
                }

                playfield.SetHitObjects(sliders);
            }
        }
    }
}
