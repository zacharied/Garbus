// Gameplay lifecycle tests over the ported playfield stack — the Garbus replacement for what BAC
// covered through osu.Game's PlayerTestScene. Drives the playfield with a manual clock so headless
// runs can seek deterministically instead of playing out in real time.

using System;
using System.Linq;
using Garbus.Game.Charts;
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

        [Resolved]
        private ISampleStore samples { get; set; } = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create playfield with test chart", () =>
            {
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
                        Child = new GarbusInputManager
                        {
                            Child = playfield = new GarbusPlayfield
                            {
                                Size = Vector2.One,
                            },
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
            // 100ms early falls inside the Ok window of the default hit windows.
            AddStep("press north", () => input.PressJoystickButton(JoystickButton.Hat1Up));
            AddStep("release north", () => input.ReleaseJoystickButton(JoystickButton.Hat1Up));

            AddUntilStep("first cardinal hit", () => firstCardinal()?.IsHit == true);
            AddAssert("later cardinal unjudged", () => playfield.AllHitObjects
                                                                .OfType<Objects.Drawables.DrawableCardinalNote>()
                                                                .Count(h => !h.Judged) == 2);
        }

        [Test]
        public void TestHitSampleResolves()
        {
            AddAssert("soft-hitnormal sample resolves", () => samples.Get(@"Gameplay/soft-hitnormal") != null);
        }

        private Objects.Drawables.DrawableCardinalNote? firstCardinal()
            => playfield.AllHitObjects.OfType<Objects.Drawables.DrawableCardinalNote>().SingleOrDefault(h => h.HitObject.StartTime == 2000);

        private Objects.Drawables.DrawableCardinalHoldNote? shortHold()
            => playfield.AllHitObjects.OfType<Objects.Drawables.DrawableCardinalHoldNote>().SingleOrDefault(h => h.HitObject.Duration < 100);
    }
}
