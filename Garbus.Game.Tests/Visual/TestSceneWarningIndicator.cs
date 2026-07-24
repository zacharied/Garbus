using System;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Gameplay;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
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
            AddAssert("gameplay effect buffer still fills warning display", () =>
                warningEffectBuffer(playfield.WarningIndicators).DrawSize,
                () => Is.EqualTo(playfield.WarningIndicators.DrawSize));
            AddAssert("gameplay blur buffer still fills warning display", () =>
                warningBlurBuffer(playfield.WarningIndicators).DrawSize,
                () => Is.EqualTo(playfield.WarningIndicators.DrawSize));
            AddAssert("gameplay mask still matches ring diameter", () =>
                warningRingMask(playfield.WarningIndicators).DrawSize,
                () => Is.EqualTo(new Vector2(MathF.Min(
                    playfield.WarningIndicators.DrawWidth,
                    playfield.WarningIndicators.DrawHeight))));
            AddAssert("gameplay arc keeps ring-relative diameter", () =>
                    MathF.Min(warningArc(playfield.WarningIndicators).DrawWidth, warningArc(playfield.WarningIndicators).DrawHeight)
                    / warningRingMask(playfield.WarningIndicators).DrawWidth,
                () => Is.EqualTo(1.1f).Within(0.001f));
            AddAssert("gameplay buffer and ring stay centred", () =>
                (warningEffectBuffer(playfield.WarningIndicators).ScreenSpaceDrawQuad.Centre
                 - warningRingMask(playfield.WarningIndicators).ScreenSpaceDrawQuad.Centre).Length,
                () => Is.LessThan(0.01f));

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

        [Test]
        public void TestWarningBufferExpansionIsIndependentOfClockDrivenVisuals()
        {
            WarningIndicatorDisplay expandedWarning = null!;
            WarningIndicatorDisplay clockDrivenWarning = null!;

            AddStep("create independent policy variants", () => Child = new FillFlowContainer
            {
                Direction = FillDirection.Horizontal,
                Children =
                [
                    new WarningPolicyHost(
                        new TestPresentationPolicy(usesClockDrivenVisuals: false, expandsWarningEffectBufferToPlayfield: true),
                        expandedWarning = new WarningIndicatorDisplay()),
                    new WarningPolicyHost(
                        new TestPresentationPolicy(usesClockDrivenVisuals: true, expandsWarningEffectBufferToPlayfield: false),
                        clockDrivenWarning = new WarningIndicatorDisplay()),
                ],
            });
            AddUntilStep("policy warnings loaded", () => expandedWarning.IsLoaded && clockDrivenWarning.IsLoaded);

            AddAssert("warning capability expands buffer", () => warningEffectBuffer(expandedWarning).RelativeSizeAxes,
                () => Is.EqualTo(Axes.None));
            AddAssert("clock capability does not expand buffer", () => warningEffectBuffer(clockDrivenWarning).RelativeSizeAxes,
                () => Is.EqualTo(Axes.Both));
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

        private static Circle warningRingMask(WarningIndicatorDisplay warning) =>
            warning.ChildrenOfType<Circle>().First();

        private static BufferedContainer warningEffectBuffer(WarningIndicatorDisplay warning) =>
            (BufferedContainer)warningRingMask(warning).Parent!;

        private static Arc warningArc(WarningIndicatorDisplay warning) =>
            warning.ChildrenOfType<Arc>().First();

        private static BufferedContainer warningBlurBuffer(WarningIndicatorDisplay warning) =>
            (BufferedContainer)warningArc(warning).Parent!;

        private partial class WarningPolicyHost : Container
        {
            private readonly IGameplayPresentationPolicy policy;
            private readonly GarbusPlayfield playfield = new GarbusPlayfield();

            public WarningPolicyHost(IGameplayPresentationPolicy policy, WarningIndicatorDisplay warning)
            {
                this.policy = policy;
                Size = new Vector2(100);
                Child = warning;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs(policy);
                dependencies.Cache(playfield);
                return dependencies;
            }
        }

        private sealed class TestPresentationPolicy : IGameplayPresentationPolicy
        {
            public TestPresentationPolicy(bool usesClockDrivenVisuals, bool expandsWarningEffectBufferToPlayfield)
            {
                UsesClockDrivenVisuals = usesClockDrivenVisuals;
                ExpandsWarningEffectBufferToPlayfield = expandsWarningEffectBufferToPlayfield;
            }

            public bool HandlesInput => true;
            public bool PlaysSamples => true;
            public bool PlaysSpawnAnimations => true;
            public bool UsesExternalResults => false;
            public bool UsesClockDrivenVisuals { get; }
            public bool ExpandsWarningEffectBufferToPlayfield { get; }

            public double LifetimeEndFor(HitObject hitObject) => double.PositiveInfinity;
            public double ResultTimeFor(HitObject hitObject) => hitObject.StartTime;
            public bool PresentsHoldAsHeld(DrawableHitObject hold) => false;
            public bool PresentsSliderAngleAsCaught(HorizontalDirection side, double angleDeg) => false;
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
