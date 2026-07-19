// Pins the judgement spec's note-lock (docs/rules-specs/Judgement.md): an input resolves against
// the oldest eligible object in its lane whose window contains it; eligibility ends only by
// judgement (including early-miss presses) or the object's own late window elapsing — hitting a
// later object never force-misses an earlier one.

using System;
using System.Linq;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
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
    public partial class TestSceneNoteLock : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private ManualInputManager input = null!;
        private GarbusPlayfield playfield = null!;

        [Resolved]
        private Gameplay.UI.Scrolling.GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        private void createPlayfield(params GarbusHitObject[] hitObjects)
        {
            AddStep("create playfield", () =>
            {
                scrollingInfo.TimeRange.Value = 700;
                manualClock = new ManualClock { Rate = 1 };

                foreach (var hitObject in hitObjects)
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

                foreach (var hitObject in hitObjects)
                    playfield.Add(PlayScreen.CreateDrawableRepresentation(hitObject));
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);
        }

        /// <summary>Walks the clock forward in sub-lifetime increments (see TestSceneGameplay.playThrough).</summary>
        private void seekTo(double target)
        {
            AddUntilStep($"seek to {target}", () =>
            {
                manualClock.CurrentTime = Math.Min(target, manualClock.CurrentTime + 200);
                return manualClock.CurrentTime >= target;
            });
        }

        private void pressNorth()
        {
            AddStep("press north", () => input.PressJoystickButton(JoystickButton.Hat1Up));
            AddStep("release north", () => input.ReleaseJoystickButton(JoystickButton.Hat1Up));
        }

        private Objects.Drawables.DrawableCardinalNote note(double startTime)
            => playfield.AllHitObjects.OfType<Objects.Drawables.DrawableCardinalNote>().Single(h => h.HitObject.StartTime == startTime);

        [Test]
        public void TestOldestContainingObjectTakesThePress()
        {
            // Two same-lane (North) notes with overlapping windows.
            createPlayfield(
                new CardinalNote { StartTime = 2000, AngleDeg = 90 },
                new CardinalNote { StartTime = 2050, AngleDeg = 90 });

            // 2055 is inside BOTH windows: note1 offset +55 (Perfect), note2 offset +5 (CriticalPerfect).
            // Oldest-first: note1 must take the press; under the old mania policy note2 would take it
            // and note1 would be force-missed.
            seekTo(2055);
            pressNorth();

            AddUntilStep("older note judged", () => note(2000).Judged);
            AddAssert("older note took the press (Perfect)", () => note(2000).Result?.Type == HitResult.Perfect);
            AddAssert("newer note untouched", () => !note(2050).Judged);

            // The newer note stays eligible until its own edge: a second press at +100 lands Near.
            seekTo(2150);
            pressNorth();

            AddUntilStep("newer note judged", () => note(2050).Judged);
            AddAssert("newer note hit (Near)", () => note(2050).Result?.Type == HitResult.Near);
            AddAssert("older note was never force-missed", () => note(2000).IsHit);
        }

        [Test]
        public void TestEarlyMissPressJudgesImmediately()
        {
            createPlayfield(new CardinalNote { StartTime = 2000, AngleDeg = 90 });

            // -150: outside Near (110) but inside the early-only Miss window (200).
            seekTo(1850);
            pressNorth();

            AddUntilStep("note judged before its time", () => note(2000).Judged);
            AddAssert("early-miss press registered a Miss", () => note(2000).Result?.Type == HitResult.Miss);
            AddAssert("clock still before StartTime", () => manualClock.CurrentTime < 2000);
        }

        [Test]
        public void TestAutoMissAtLateEligibilityEdge()
        {
            createPlayfield(new CardinalNote { StartTime = 2000, AngleDeg = 90 });

            // Just inside the Near late edge (110): still eligible, unjudged.
            seekTo(2105);
            AddAssert("still eligible inside the Near edge", () => !note(2000).Judged);

            // Just past it: auto-missed (not at osu's 136/173).
            AddStep("step past the edge", () => manualClock.CurrentTime = 2115);
            AddUntilStep("auto-missed past the edge", () => note(2000).Result?.Type == HitResult.Miss);
        }
    }
}
