// Gameplay lifecycle tests over the playfield stack. Drives the playfield with a manual clock so
// headless runs can seek deterministically instead of playing out in real time.

using System;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osu.Framework.Timing;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneGameplay : GarbusTestScene
    {
        // This scene is driven entirely by a ManualClock, so there is nothing to watch in real time;
        // pace automated steps per-frame. At the default 200ms-per-action pacing, playThrough's ~100
        // until-polls need 20s of wall time in the interactive test browser, which can never fit
        // UntilStepButton's fixed 10s wall-clock timeout (headless passes only because its fast clock
        // decouples game time from wall time).
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private ManualInputManager input = null!;
        private GarbusPlayfield playfield = null!;
        private HoldActivationDebugDisplay holdActivationDisplay = null!;

        [Resolved]
        private ISampleStore samples { get; set; } = null!;

        [Resolved]
        private Gameplay.UI.Scrolling.GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create playfield with test chart", () =>
            {
                // Pin the scroll TimeRange so the polar geometry assertions below stay independent of
                // the user-facing scroll-speed default (which GarbusGameBase drives into this bindable).
                scrollingInfo.TimeRange.Value = 700;

                manualClock = new ManualClock { Rate = 1 };

                var chart = GarbusTestChartGenerator.GenerateChart();

                foreach (var hitObject in chart.HitObjects)
                    hitObject.ApplyDefaults();

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Clock = new FramedClock(manualClock),
                        Children = new Drawable[]
                        {
                            new GarbusInputManager
                            {
                                Child = playfield = new GarbusPlayfield
                                {
                                    Size = Vector2.One,
                                },
                            },
                            holdActivationDisplay = new HoldActivationDebugDisplay(playfield),
                        },
                    },
                };

                foreach (var hitObject in chart.HitObjects)
                    playfield.Add(PlayScreen.CreateDrawableRepresentation(hitObject));
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);
        }

        [Test]
        public void TestObjectsBecomeAlive()
        {
            AddAssert("nothing alive at time 0", () => playfield.AllHitObjects.All(h => !h.IsAlive));

            AddStep("seek to first note", () => manualClock.CurrentTime = 2000);
            AddUntilStep("cardinal note alive", () => playfield.AllHitObjects.OfType<Objects.Drawables.DrawableCardinalNote>().Any(h => h.IsAlive));

            AddStep("seek to slider", () => manualClock.CurrentTime = 3000);
            AddUntilStep("slider body alive", () => playfield.AllHitObjects.OfType<Objects.Drawables.DrawableSliderBody>().Any(h => h.IsAlive));
        }

        [Test]
        public void TestSliderContactSpikesFollowRingContact()
        {
            Objects.Drawables.DrawableSliderBody rightBody = null!;
            Objects.Drawables.SliderContactSpikes contactSpikes = null!;
            float firstContactAngle = 0;

            AddUntilStep("right slider registered", () =>
            {
                rightBody = playfield.AllHitObjects
                                     .OfType<Objects.Drawables.DrawableSliderBody>()
                                     .SingleOrDefault(b => b.HitObject.Side == HorizontalDirection.Right)!;
                return rightBody != null;
            });

            AddAssert("paddles have no endpoint triangles", () => playfield.ChildrenOfType<StickIndicator>()
                                                                         .All(i => i.ChildrenOfType<StickCentreSpike>().Count() == 1 &&
                                                                                   i.ChildrenOfType<osu.Framework.Graphics.Shapes.Triangle>().Count() == 1));
            playThrough(1900);
            AddUntilStep("right slider contact effect loaded", () =>
            {
                contactSpikes = rightBody.ContactSpikes;
                return contactSpikes.IsLoaded;
            });
            AddAssert("hidden before slider reaches ring", () => !contactSpikes.Active && contactSpikes.Alpha == 0);

            playThrough(2000);
            AddUntilStep("split spike appears at contact", () => contactSpikes.Active && contactSpikes.Alpha > 0);
            AddAssert("two flared triangles with centre gap", () =>
                contactSpikes.Blades.Count == 2 &&
                contactSpikes.Blades[0].AngleRadians < contactSpikes.ContactAngleRadians &&
                contactSpikes.Blades[1].AngleRadians > contactSpikes.ContactAngleRadians);
            AddAssert("spike uses slider side colour", () => contactSpikes.SideColour.Equals(Constants.RightColour));
            AddAssert("triangles point inward and stop before centre", () => contactSpikes.Blades.All(blade =>
            {
                var triangle = blade.ChildrenOfType<osu.Framework.Graphics.Shapes.Triangle>().SingleOrDefault();
                float ringRadius = MathF.Min(playfield.DrawWidth, playfield.DrawHeight) / 2;
                return triangle != null && triangle.DrawHeight > 0 && triangle.DrawHeight < ringRadius;
            }));
            AddStep("remember first contact angle", () => firstContactAngle = contactSpikes.ContactAngleRadians);

            playThrough(3500);
            AddUntilStep("spike follows moving contact", () =>
                !approximately(contactSpikes.ContactAngleRadians, firstContactAngle) &&
                approximately(contactSpikes.ContactAngleRadians, rightBody.AngleDegAt(manualClock.CurrentTime) * MathF.PI / 180));

            playThrough(6200);
            AddUntilStep("spike hides after slider leaves ring", () => !contactSpikes.Active && contactSpikes.Alpha == 0);
        }

        /// <summary>
        /// Path drawables each own a buffered framebuffer and a GPU vertex batch, and hit objects are not
        /// pooled — so without a shared pool the live Path count would track how many sliders the chart
        /// contains rather than how many are on screen. This pins the borrow/return cycle that prevents that.
        /// </summary>
        [Test]
        public void TestSliderPathsReturnToSharedPoolOnKill()
        {
            Objects.Drawables.DrawableSliderBody rightBody = null!;

            AddUntilStep("right slider registered", () =>
            {
                rightBody = playfield.AllHitObjects
                                     .OfType<Objects.Drawables.DrawableSliderBody>()
                                     .SingleOrDefault(b => b.HitObject.Side == HorizontalDirection.Right)!;
                return rightBody != null;
            });

            playThrough(3500);

            AddUntilStep("body rents paths while alive", () => rightBody.BodyPaths.Count > 0 && rightBody.GlowPaths.Count > 0);
            AddAssert("paths came from the shared pool", () => playfield.SliderPathPool.ConstructedPaths > 0);

            // Well past the body's hit-state fade and Expire(), so the container has run RemoveDrawable
            // (which is what calls OnKilled). playThrough is what moves the clock — an until-step alone
            // would spin without ever advancing time.
            playThrough(20000);

            AddUntilStep("paths are handed back once the body is killed", () =>
                !rightBody.IsAlive && rightBody.BodyPaths.Count == 0 && rightBody.GlowPaths.Count == 0
                                   && rightBody.EscapePaths.Count == 0 && rightBody.EscapeGlowPaths.Count == 0);

            // The returned instances must actually be reachable again, otherwise the pool leaks and every
            // slider still constructs its own — the exact cost this exists to remove.
            AddAssert("returned paths are reused rather than reconstructed", () =>
            {
                var pool = playfield.SliderPathPool;

                int pathsBefore = pool.ConstructedPaths;
                var path = pool.RentPath(rightBody.Thickness / 2);
                bool pathReused = pool.ConstructedPaths == pathsBefore;
                pool.Return(path);

                return pathReused;
            });
        }

        [Test]
        public void TestSliderBodyGlowIsGeometric()
        {
            Objects.Drawables.DrawableSliderBody rightBody = null!;

            AddUntilStep("right slider registered", () =>
            {
                rightBody = playfield.AllHitObjects
                                     .OfType<Objects.Drawables.DrawableSliderBody>()
                                     .SingleOrDefault(b => b.HitObject.Side == HorizontalDirection.Right)!;
                return rightBody != null;
            });

            playThrough(3500);

            AddUntilStep("glow paths mirror body paths", () =>
            {
                var bodyPaths = rightBody.BodyPaths.Where(p => p.Vertices.Count >= 2).ToList();
                var glowPaths = rightBody.GlowPaths.Where(p => p.Vertices.Count >= 2).ToList();

                return bodyPaths.Count > 0
                       && glowPaths.Count == bodyPaths.Count
                       && glowPaths.Zip(bodyPaths).All(pair => pair.First.Vertices.SequenceEqual(pair.Second.Vertices));
            });

            AddAssert("glow is wider than the line and additive", () => rightBody.GlowPaths.All(g =>
                g.PathRadius > rightBody.Thickness / 2 &&
                g.Blending == osu.Framework.Graphics.BlendingParameters.Additive));

            // The perf fix this pins: no per-frame blurred framebuffer for the body glow. The only
            // buffered containers left in the subtree belong to the contact spikes' (small) glow.
            AddAssert("no buffered blur outside contact spikes", () =>
                rightBody.ChildrenOfType<osu.Framework.Graphics.Containers.BufferedContainer>().Count() ==
                rightBody.ContactSpikes.ChildrenOfType<osu.Framework.Graphics.Containers.BufferedContainer>().Count());
        }

        [Test]
        public void TestEscapingSliderRendersAsGlowTube()
        {
            Objects.Drawables.DrawableSliderBody rightBody = null!;

            AddUntilStep("right slider registered", () =>
            {
                rightBody = playfield.AllHitObjects
                                     .OfType<Objects.Drawables.DrawableSliderBody>()
                                     .SingleOrDefault(b => b.HitObject.Side == HorizontalDirection.Right)!;
                return rightBody != null;
            });

            // Mid-slider with no input: the leading edge has escaped past the ring uncaught.
            playThrough(3500);

            AddUntilStep("escape slices present", () => rightBody.EscapePaths.Any(p => p.Vertices.Count >= 2));

            // The escaping portion must keep the tube look: every crisp escape slice has a glow twin.
            AddAssert("escape glow mirrors escape slices", () =>
            {
                var crisp = rightBody.EscapePaths.Where(p => p.Vertices.Count >= 2).ToList();
                var glow = rightBody.EscapeGlowPaths.Where(p => p.Vertices.Count >= 2).ToList();

                return crisp.Count > 0
                       && glow.Count == crisp.Count
                       && glow.Zip(crisp).All(pair => pair.First.Vertices.SequenceEqual(pair.Second.Vertices));
            });

            // The uncaught fade must dissolve over a band wider than the old hard clip at the
            // catcher radius (1.06x ring), so the tube reads as flying off rather than popping.
            AddAssert("escaping fade reaches past the catcher", () =>
            {
                float ringRadius = MathF.Min(playfield.DrawWidth, playfield.DrawHeight) / 2;

                return rightBody.EscapeGlowPaths
                                .Where(p => p.Vertices.Count >= 2)
                                .SelectMany(p => p.Vertices)
                                .Select(v => v.Length)
                                .DefaultIfEmpty(0)
                                .Max() > ringRadius * 1.15f;
            });
        }

        [Test]
        public void TestUncaughtDimIsEased()
        {
            Objects.Drawables.DrawableSliderBody rightBody = null!;

            AddUntilStep("right slider registered", () =>
            {
                rightBody = playfield.AllHitObjects
                                     .OfType<Objects.Drawables.DrawableSliderBody>()
                                     .SingleOrDefault(b => b.HitObject.Side == HorizontalDirection.Right)!;
                return rightBody != null;
            });

            playThrough(1900);
            AddAssert("full alpha before start", () => rightBody.Alpha == 1);

            // Two separate frames: the first Update past StartTime begins the dim fade, the second
            // lands mid-fade — the alpha must be easing, not snapped straight to the dim value.
            AddStep("step just past start", () => manualClock.CurrentTime = 2050);
            AddStep("step into the fade", () => manualClock.CurrentTime = 2125);
            AddAssert("dim is easing", () => rightBody.Alpha is > 0.4f and < 1f);

            AddStep("step past the fade", () => manualClock.CurrentTime = 2600);
            AddUntilStep("settled at dim", () => osu.Framework.Utils.Precision.AlmostEquals(rightBody.Alpha, 0.4f, 0.001f));
        }

        [Test]
        public void TestStickCentreSpikeFollowsActiveStickAndFitsGap()
        {
            StickIndicator rightIndicator = null!;
            StickCentreSpike centreSpike = null!;

            AddUntilStep("right centre spike loaded", () =>
            {
                rightIndicator = playfield.ChildrenOfType<StickIndicator>()
                                          .SingleOrDefault(i => i.Side == HorizontalDirection.Right)!;
                centreSpike = rightIndicator?.ChildrenOfType<StickCentreSpike>().SingleOrDefault()!;
                return centreSpike?.IsLoaded == true;
            });

            AddAssert("hidden while stick is in deadzone", () => !centreSpike.Active && centreSpike.Alpha == 0);
            AddAssert("narrower than slider contact gap", () => StickCentreSpike.HalfWidthDeg < Objects.Drawables.SliderContactSpikes.GapHalfWidthDeg);

            AddStep("activate right stick at east", () => setStick(HorizontalDirection.Right, 1, 0));
            AddUntilStep("centre spike appears at stick angle", () =>
                centreSpike.Active && centreSpike.Alpha > 0 && approximately(centreSpike.AngleRadians, 0));
            AddAssert("uses right side colour and blur", () =>
                centreSpike.SideColour.Equals(Constants.RightColour) &&
                centreSpike.ChildrenOfType<osu.Framework.Graphics.Containers.BufferedContainer>()
                           .Single().BlurSigma.X > 0);
            AddAssert("widest at centre and tapers to ring", () =>
            {
                var triangle = centreSpike.ChildrenOfType<osu.Framework.Graphics.Shapes.Triangle>().Single();
                float ringRadius = MathF.Min(rightIndicator.DrawWidth, rightIndicator.DrawHeight) / 2;
                return approximately(centreSpike.Blade.BaseRadius, 0) &&
                       approximately(centreSpike.Blade.TipRadius, ringRadius) &&
                       MathF.Abs(triangle.DrawHeight - ringRadius) < 1;
            });

            AddStep("rotate right stick north", () => setStick(HorizontalDirection.Right, 0, -1));
            AddUntilStep("centre spike follows stick", () => approximately(centreSpike.AngleRadians, MathF.PI / 2));

            AddStep("release right stick", () => setStick(HorizontalDirection.Right, 0, 0));
            AddUntilStep("centre spike hides in deadzone", () => !centreSpike.Active && centreSpike.Alpha == 0);
        }

        /// <summary>
        /// Walks the clock forward in sub-lifetime increments. A single large jump would take entries
        /// straight from future to past without them ever becoming alive, skipping the kill-path
        /// judgement — something continuous playback can never do.
        /// </summary>
        private void playThrough(double target)
        {
            AddUntilStep($"play through to {target}", () =>
            {
                manualClock.CurrentTime = Math.Min(target, manualClock.CurrentTime + 200);
                return manualClock.CurrentTime >= target;
            });
        }

        private void setStick(HorizontalDirection side, float x, float y)
        {
            var xSource = side == HorizontalDirection.Left
                ? JoystickAxisSource.GamePadLeftStickX
                : JoystickAxisSource.GamePadRightStickX;
            var ySource = side == HorizontalDirection.Left
                ? JoystickAxisSource.GamePadLeftStickY
                : JoystickAxisSource.GamePadRightStickY;

            input.Input(new JoystickAxisInput(new[]
            {
                new JoystickAxis(xSource, x),
                new JoystickAxis(ySource, y),
            }));
        }

        private static bool approximately(float first, float second) => MathF.Abs(first - second) < 0.001f;

        [Test]
        public void TestUntouchedNotesMiss()
        {
            playThrough(20000);

            AddUntilStep("all notes judged", () => playfield.AllHitObjects
                                                            .Where(h => h.HitObject is Note)
                                                            .All(h => h.Judged));

            AddAssert("cardinal notes missed", () => playfield.AllHitObjects
                                                              .Where(h => h.HitObject is CardinalNote)
                                                              .All(h => h.Result?.Type == HitResult.Miss));

            AddAssert("shoulder notes missed", () => playfield.AllHitObjects
                                                              .Where(h => h.HitObject is ShoulderNote)
                                                              .All(h => h.Result?.Type == HitResult.Miss));
        }

        [Test]
        public void TestShortHoldInheritsMissedHead()
        {
            playThrough(20000);

            // The 80ms hold at 11000 has no meaningful body, so its deferred tail judgement must
            // inherit the auto-missed head rather than waiting on catch records.
            AddUntilStep("short hold judged", () => shortHold()?.Judged == true);
            AddAssert("short hold missed", () => shortHold()?.Result?.Type == HitResult.Miss);
        }

        [Test]
        public void TestSliderChildrenJudgedWithoutInput()
        {
            playThrough(20000);

            AddUntilStep("slider children judged", () => playfield.AllHitObjects
                                                                  .OfType<Objects.Drawables.DrawableSliderBody>()
                                                                  .SelectMany(b => b.NestedHitObjects)
                                                                  .All(n => n.Judged));
        }

        [Test]
        public void TestCardinalNoteHitByButtonPress()
        {
            playThrough(1900);

            // The first cardinal note (2000ms, 90° → North) should judge as a hit on the Up press —
            // 100ms early falls inside the Near window.
            AddStep("press north", () => input.PressJoystickButton(JoystickButton.Hat1Up));
            AddStep("release north", () => input.ReleaseJoystickButton(JoystickButton.Hat1Up));

            AddUntilStep("first cardinal hit", () => firstCardinal()?.IsHit == true);
            AddAssert("later cardinal unjudged", () => playfield.AllHitObjects
                                                                .OfType<Objects.Drawables.DrawableCardinalNote>()
                                                                .Count(h => !h.Judged) == 2);
        }

        [Test]
        public void TestSpawnAnimationReplaysAfterRestart()
        {
            // Play past the first cardinal's hit so it spawns and judges, then "restart" (clock → 0).
            playThrough(2200);
            AddUntilStep("first cardinal judged", () => firstCardinal()?.Judged == true);

            AddStep("restart (clock to 0)", () => manualClock.CurrentTime = 0);

            // Re-enter the spawn window. LeadTime = TimeRange × (1 − SpawnHaloFraction) + SpawnDuration =
            // 700 × 0.88 + 125 = 741, so the first cardinal (StartTime 2000) spawns at 2000 − 741 = 1259;
            // 1360 lands 101ms into its 125ms tween — mid-scale-in.
            AddStep("seek into spawn window", () => manualClock.CurrentTime = 1360);
            AddUntilStep("spawn animation replays (scale mid-way)", () =>
            {
                var sprite = firstCardinal()?.ChildrenOfType<osu.Framework.Graphics.Sprites.Sprite>().FirstOrDefault();
                return sprite != null && sprite.Scale.X < 0.9f;
            });
        }

        [Test]
        public void TestCardinalHoldActivationDebugDisplay()
        {
            Objects.Drawables.DrawableCardinalHoldNote hold = null!;

            AddUntilStep("find west hold", () =>
            {
                hold = playfield.AllHitObjects
                                .OfType<Objects.Drawables.DrawableCardinalHoldNote>()
                                .SingleOrDefault(h => h.HitObject.StartTime == 6000)!;
                return hold != null;
            });

            playThrough(5900);
            AddAssert("display hidden before hold starts", () => holdActivationDisplay.Alpha, () => Is.Zero);

            playThrough(6100);
            AddUntilStep("display appears without press", () => holdActivationDisplay.Alpha == 1);
            AddAssert("display tracks active hold", () => ReferenceEquals(holdActivationDisplay.ActiveHold, hold));

            AddStep("press west", () => input.PressJoystickButton(JoystickButton.Hat1Left));
            AddAssert("display tracks pressed hold", () => ReferenceEquals(holdActivationDisplay.ActiveHold, hold));

            playThrough(6500);
            AddAssert("display mirrors half activation", () => holdActivationDisplay.DisplayedProgress,
                () => Is.EqualTo(0.5).Within(0.02));

            AddStep("release west", () => input.ReleaseJoystickButton(JoystickButton.Hat1Left));
            AddAssert("display remains after release", () => holdActivationDisplay.Alpha, () => Is.EqualTo(1));

            playThrough(7100);
            AddUntilStep("display hides after hold ends", () => holdActivationDisplay.Alpha == 0);
        }

        [Test]
        public void TestJudgementFeedbackAndRewind()
        {
            JudgementResult firstResult = null!;

            playThrough(1900);

            AddStep("press north", () => input.PressJoystickButton(JoystickButton.Hat1Up));
            AddStep("release north", () => input.ReleaseJoystickButton(JoystickButton.Hat1Up));

            AddUntilStep("first cardinal hit", () => firstCardinal()?.IsHit == true);
            AddUntilStep("feedback shown", () => judgementFeedback()?.ActiveMessages.Count == 1);
            AddStep("capture result", () => firstResult = firstCardinal()!.Result);
            AddAssert("feedback references result", () => ReferenceEquals(judgementFeedback()!.ActiveMessages.Single().Result, firstResult));
            AddAssert("feedback at north angle", () => judgementFeedback()!.ActiveMessages.Single().AngleDeg == 90);
            AddAssert("feedback rank is near", () => judgementFeedback()!.ActiveMessages.Single().RankText == "NEAR");
            AddAssert("feedback says early", () => judgementFeedback()!.ActiveMessages.Single().DetailText == "EARLY");

            AddStep("rewind before judgement", () => manualClock.CurrentTime = 1800);
            AddUntilStep("judgement reverted", () => firstCardinal()?.Judged == false);
            AddUntilStep("feedback removed", () => judgementFeedback()?.ActiveMessages.Count == 0);

            playThrough(1900);
            AddStep("press north again", () => input.PressJoystickButton(JoystickButton.Hat1Up));
            AddStep("release north again", () => input.ReleaseJoystickButton(JoystickButton.Hat1Up));
            AddUntilStep("feedback replayed", () => judgementFeedback()?.ActiveMessages.Count == 1);
            AddAssert("same result object can create fresh feedback", () => ReferenceEquals(judgementFeedback()!.ActiveMessages.Single().Result, firstResult));
        }

        [Test]
        public void TestDisplayJudgementsSuppressesIntegratedFeedback()
        {
            AddStep("disable judgement display", () => playfield.DisplayJudgements.Value = false);
            playThrough(1900);

            AddStep("press north", () => input.PressJoystickButton(JoystickButton.Hat1Up));
            AddStep("release north", () => input.ReleaseJoystickButton(JoystickButton.Hat1Up));

            AddUntilStep("first cardinal hit", () => firstCardinal()?.IsHit == true);
            AddAssert("feedback remains empty", () => judgementFeedback()?.ActiveMessages.Count == 0);
        }

        [Test]
        public void HittingAnObjectPlaysExactlyOneFamilyMember()
        {
            Garbus.Game.Gameplay.Objects.Drawables.DrawableHitObject? drawable = null;

            AddUntilStep("wait for a cardinal note drawable", () =>
            {
                drawable = playfield.AllHitObjects
                                    .FirstOrDefault(d => d.HitObject is CardinalNote);
                return drawable != null;
            });

            // Seek into the note's hit window and press its bound direction key — same mechanics as
            // TestCardinalNoteHitByButtonPress (walk the clock forward in sub-lifetime increments via
            // playThrough, then press 100ms early so the offset lands inside the Near window), but
            // generalised over whichever direction the located note happens to be (rather than
            // assuming North).
            AddUntilStep("play through to note", () =>
            {
                double target = ((CardinalNote)drawable!.HitObject).StartTime - 100;
                manualClock.CurrentTime = Math.Min(target, manualClock.CurrentTime + 200);
                return manualClock.CurrentTime >= target;
            });
            AddStep("press key", () => input.PressJoystickButton(cardinalButton(((CardinalNote)drawable!.HitObject).Direction)));
            AddStep("release key", () => input.ReleaseJoystickButton(cardinalButton(((CardinalNote)drawable!.HitObject).Direction)));

            AddUntilStep("note is hit", () => drawable!.IsHit);
            AddAssert("exactly one member played", () =>
                ((Garbus.Game.Objects.Drawables.DrawableGarbusHitObject<CardinalNote>)drawable!).SamplesPlayCount,
                () => Is.EqualTo(1));
        }

        private static JoystickButton cardinalButton(CardinalDirection direction) => direction switch
        {
            CardinalDirection.North => JoystickButton.Hat1Up,
            CardinalDirection.East => JoystickButton.Hat1Right,
            CardinalDirection.South => JoystickButton.Hat1Down,
            CardinalDirection.West => JoystickButton.Hat1Left,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };

        [Test]
        public void HittingASliderChildPlaysExactlyOneHitsound()
        {
            Objects.Drawables.DrawableSliderBody body = null!;
            Objects.Drawables.DrawableSliderChild child = null!;

            // The right-side slider at 2000ms (bundled test chart) has its first control point at
            // RotationOffset 0 / TimeOffset 1000, i.e. target angle 0 over the segment window
            // [2000, 3000]. Holding the right stick (this slider's Side) pinned at angle 0 for that
            // whole window should catch every frame and land a hit.
            AddUntilStep("wait for right slider body drawable", () =>
            {
                body = playfield.AllHitObjects
                                .OfType<Objects.Drawables.DrawableSliderBody>()
                                .FirstOrDefault(b => ((SliderBody)b.HitObject).Side == HorizontalDirection.Right)!;
                return body != null;
            });

            playThrough(1900);

            AddUntilStep("first child drawable present", () =>
            {
                var controlPoint = ((SliderBody)body.HitObject).Path.ControlPoints[0];
                child = body.NestedHitObjects
                            .OfType<Objects.Drawables.DrawableSliderChild>()
                            .FirstOrDefault(c => ((SliderChild)c.HitObject).ControlPoint == controlPoint)!;
                return child != null;
            });

            AddStep("hold right stick on target angle", () => input.Input(new JoystickAxisInput(new[]
            {
                new JoystickAxis(JoystickAxisSource.GamePadRightStickX, 1f),
                new JoystickAxis(JoystickAxisSource.GamePadRightStickY, 0f),
            })));

            AddUntilStep("play through segment", () =>
            {
                manualClock.CurrentTime = Math.Min(3000, manualClock.CurrentTime + 50);
                return manualClock.CurrentTime >= 3000;
            });

            AddStep("release right stick", () => input.Input(new JoystickAxisInput(new[]
            {
                new JoystickAxis(JoystickAxisSource.GamePadRightStickX, 0f),
                new JoystickAxis(JoystickAxisSource.GamePadRightStickY, 0f),
            })));

            AddUntilStep("child judged", () => child.Judged);
            AddAssert("child is hit", () => child.IsHit);
            AddAssert("child plays once", () => child.SamplesPlayCount, () => Is.EqualTo(1));
        }

        [Test]
        public void TestZeroDurationSliderResolvesFromMissedHead()
        {
            Objects.Drawables.DrawableSliderBody body = null!;

            // A slider whose only child sits at TimeOffset 0 (a zero-duration constant-radius arc, as a
            // slam's coincident children also are): its catch window [StartTime, StartTime] has zero
            // width, so its duration judgement resolves directly from the head reference. With no input,
            // both the catch-timed head and the zero-duration child must miss.
            AddStep("add zero-duration slider", () =>
            {
                var slider = new SliderBody
                {
                    StartTime = 5050,
                    AngleDeg = 0,
                    Side = HorizontalDirection.Left,
                    Path = new GarbusPath
                    {
                        ControlPoints = new osu.Framework.Bindables.BindableList<GarbusPathControlPoint>
                        {
                            new GarbusPathControlPoint { TimeOffset = 0, RotationOffset = 90 },
                        },
                    },
                };
                slider.ApplyDefaults();
                playfield.Add(PlayScreen.CreateDrawableRepresentation(slider));
            });

            AddUntilStep("zero-duration slider body present", () =>
            {
                body = playfield.AllHitObjects
                                .OfType<Objects.Drawables.DrawableSliderBody>()
                                .FirstOrDefault(b => b.HitObject.StartTime == 5050)!;
                return body != null;
            });

            playThrough(20000);

            AddUntilStep("child judged", () => body.NestedHitObjects
                                                   .OfType<Objects.Drawables.DrawableSliderChild>()
                                                   .All(c => c.Judged));
            AddAssert("child inherits missed head", () => body.NestedHitObjects
                                                               .OfType<Objects.Drawables.DrawableSliderChild>()
                                                               .All(c => !c.IsHit));
            AddAssert("missed child stays silent", () => body.NestedHitObjects
                                                              .OfType<Objects.Drawables.DrawableSliderChild>()
                                                              .All(c => c.SamplesPlayCount == 0));
        }

        [Test]
        public void TestHeadOnlySliderDisplaysCircle()
        {
            Objects.Drawables.DrawableSliderBody body = null!;

            // A slider with ZERO control points — just its head. It renders no path line, so it must show
            // a circle (body-line radius) to stay visible. StartTime 5050 sits off the playThrough grid.
            AddStep("add head-only slider", () =>
            {
                var slider = new SliderBody
                {
                    StartTime = 5050,
                    AngleDeg = 0,
                    Side = HorizontalDirection.Left,
                    Path = new GarbusPath
                    {
                        ControlPoints = new osu.Framework.Bindables.BindableList<GarbusPathControlPoint>(),
                    },
                };
                slider.ApplyDefaults();
                playfield.Add(PlayScreen.CreateDrawableRepresentation(slider));
            });

            AddUntilStep("head-only slider body present", () =>
            {
                body = playfield.AllHitObjects
                                .OfType<Objects.Drawables.DrawableSliderBody>()
                                .FirstOrDefault(b => b.HitObject.StartTime == 5050)!;
                return body != null;
            });

            // Walk the clock up to just before StartTime; the head must have emerged and be visible as a
            // glow disc (a dot-length GlowPath carrying the tube's full cross-section) before the ring.
            AddUntilStep("head glow disc visible before judgement", () =>
            {
                manualClock.CurrentTime = Math.Min(5000, manualClock.CurrentTime + 50);
                return body.HeadGlow.Alpha > 0 && body.HeadGlow.Vertices.Count >= 2 && manualClock.CurrentTime >= 5000;
            });

            // "Fake radius": the disc is as wide as a tube cross-section, not just the crisp line.
            AddAssert("disc carries tube fullness", () => body.HeadGlow.PathRadius > body.Thickness / 2);

            // Past the ring the disc dissolves over the same EscapeFadeScale runway as the tube,
            // instead of the old hard pop at the ring.
            AddStep("step past the ring", () => manualClock.CurrentTime = 5150);
            AddAssert("disc mid-dissolve past ring", () => body.HeadGlow.Alpha is > 0f and < 1f);

            // Catch timing requires real stick input; an untouched head misses after its 200ms late edge.
            playThrough(6000);
            AddUntilStep("head judged", () => body.NestedHitObjects
                                                  .OfType<Objects.Drawables.DrawableSliderHead>()
                                                  .All(h => h.Judged));
            AddAssert("untouched head missed", () => body.NestedHitObjects
                                                       .OfType<Objects.Drawables.DrawableSliderHead>()
                                                       .All(h => !h.IsHit));
            AddAssert("missed head stays silent", () => body.NestedHitObjects
                                                            .OfType<Objects.Drawables.DrawableSliderHead>()
                                                            .All(h => h.SamplesPlayCount == 0));
            AddUntilStep("body lifetime sentinel judged", () => body.Judged);
            AddAssert("unscored body stays silent", () => body.SamplesPlayCount, () => Is.Zero);
        }

        [Test]
        public void TestComboDisplayShowsCurrentComboAtCentre()
        {
            AddStep("set combo to 12", () => playfield.Combo.Value = 12);
            AddUntilStep("centre shows 12", () => comboDisplay()?.Text.ToString() == "12");
            AddAssert("centre visible", () => comboDisplay()?.Alpha > 0);
            AddAssert("centred in ring", () =>
            {
                var display = comboDisplay()!;
                return display.Anchor == Anchor.Centre && display.Origin == Anchor.Centre;
            });

            AddStep("reset combo to 0", () => playfield.Combo.Value = 0);
            AddUntilStep("centre hidden at 0", () => comboDisplay()?.Alpha == 0);
        }

        private ComboDisplay? comboDisplay() => playfield.ChildrenOfType<ComboDisplay>().SingleOrDefault();

        private JudgementFeedbackDisplay? judgementFeedback() => playfield.ChildrenOfType<JudgementFeedbackDisplay>().SingleOrDefault();

        [Test]
        public void TestHitSampleResolves()
        {
            AddAssert("soft-hitnormal sample resolves", () => samples.Get(@"Gameplay/soft-hitnormal") != null);
        }

        private Objects.Drawables.DrawableCardinalNote? firstCardinal()
            => playfield.AllHitObjects.OfType<Objects.Drawables.DrawableCardinalNote>().SingleOrDefault(h => h.HitObject.StartTime == 2000);

        private Objects.Drawables.DrawableCardinalHoldNote? shortHold()
            => playfield.AllHitObjects.OfType<Objects.Drawables.DrawableCardinalHoldNote>().SingleOrDefault(h => h.HitObject.Duration < 100);

        private Objects.Drawables.DrawableShoulderHoldNote? shoulderHold()
            => playfield.AllHitObjects.OfType<Objects.Drawables.DrawableShoulderHoldNote>().SingleOrDefault();

        [Test]
        public void TestShoulderHoldMissedWhenUntouched()
        {
            playThrough(20000);

            AddUntilStep("shoulder hold judged", () => shoulderHold()?.Judged == true);
            AddAssert("shoulder hold missed", () => shoulderHold()?.Result?.Type == HitResult.Miss);
        }

        [Test]
        public void TestShoulderHoldSectorTrailsDuringApproach()
        {
            // The shoulder hold at 13000ms (1000ms) must show its transparent sector trailing inward
            // toward the centre while the head is still approaching the ring — exactly like the cardinal
            // hold body. CircularProgress renders nothing when InnerRadius == 0, so the fill fraction must
            // be 1 - inner/outer (not inner/outer); an inverted value leaves the sector invisible until
            // the ring, then grows it out of the head toward centre.
            playThrough(12800); // StartTime 13000 → head still ~200ms out from the ring
            AddAssert("sector visible mid-approach", () =>
            {
                var sector = shoulderHold()!.ChildrenOfType<osu.Framework.Graphics.UserInterface.CircularProgress>().Single();
                return sector.InnerRadius > 0f;
            });

            // Partway through the hold the fill must span [tail, head]: the hole radius = (1-InnerRadius)*outer
            // equals the tail distance, so InnerRadius sits well above 0.5 here (the inverted value would be < 0.5).
            playThrough(13500);
            AddAssert("fill spans tail→head, not a thin edge band", () =>
            {
                var sector = shoulderHold()!.ChildrenOfType<osu.Framework.Graphics.UserInterface.CircularProgress>().Single();
                return sector.InnerRadius > 0.5f;
            });
        }

        [Test]
        public void TestShoulderHoldHeldByButtonPress()
        {
            // Right shoulder hold at 13000ms, 1000ms long. Joystick6 maps to ButtonR.
            playThrough(12900);
            AddStep("press right shoulder", () => input.PressJoystickButton(JoystickButton.Button6));
            playThrough(14100);
            AddStep("release right shoulder", () => input.ReleaseJoystickButton(JoystickButton.Button6));

            AddUntilStep("shoulder hold judged", () => shoulderHold()?.Judged == true);
            AddAssert("shoulder hold hit", () => shoulderHold()?.IsHit == true);
        }
    }
}
