// Behaviour of the general presentation-only autoHit drawable capability: a pure function of clock
// time (deterministic hit animation, statelessness under seek/rewind) that never judges or scores.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.Screens;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneAutoHit : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private GarbusPlayfield playfield = null!;
        private readonly List<JudgementResult> results = new List<JudgementResult>();

        [Resolved]
        private Gameplay.UI.Scrolling.GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create non-interactive playfield", () =>
            {
                scrollingInfo.TimeRange.Value = 700;
                results.Clear();
                manualClock = new ManualClock { Rate = 1 };

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = playfield = new GarbusPlayfield(interactive: false) { Size = Vector2.One },
                };
                playfield.NewResult += (_, r) => results.Add(r);

                var note = new CardinalNote { StartTime = 2000, AngleDeg = CardinalDirection.North.ToDegrees() };
                note.ApplyDefaults();
                playfield.Add(PlayScreen.CreateDrawableRepresentation(note, autoHit: true));
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);
        }

        private DrawableCardinalNote? note()
            => playfield.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault();

        [Test]
        public void TestAutoHitPlaysHitVisualAtHitTimeAndRevertsOnRewind()
        {
            AddStep("seek just before hit", () => manualClock.CurrentTime = 1900);
            AddUntilStep("note alive and not faded", () => note()?.IsAlive == true && note()!.Alpha > 0.9f);

            // The note's entry lives for exactly one more TimeRange past its hit time (deterministic,
            // GarbusScrollingHitObjectContainer.setComputedLifetime: GetEndTime() + timeRange = 2700 here) —
            // AutoHit never extends that (Task 1's LifetimeEnd swallow keeps it owned by the container), so
            // this must land after the 350ms fade completes (2350) but before the entry dies (2700), or the
            // drawable stops updating and freezes at its last alive state instead of showing the completed fade.
            AddStep("seek past hit + animation", () => manualClock.CurrentTime = 2500);
            AddUntilStep("hit animation ran (faded out)", () => note() != null && note()!.ChildrenOfType<osu.Framework.Graphics.Sprites.Sprite>().First().Alpha < 0.05f);

            AddStep("rewind before hit", () => manualClock.CurrentTime = 1900);
            AddUntilStep("hit animation unplayed (visible again)", () => note() != null && note()!.ChildrenOfType<osu.Framework.Graphics.Sprites.Sprite>().First().Alpha > 0.9f);
        }

        [Test]
        public void TestAutoHitProducesNoJudgementResult()
        {
            AddStep("seek far past the note", () => manualClock.CurrentTime = 10000);
            AddStep("rewind to start", () => manualClock.CurrentTime = 0);
            AddStep("seek forward again", () => manualClock.CurrentTime = 10000);
            AddAssert("never judged", () => note() == null || !note()!.Judged);
            AddAssert("no results emitted", () => results.Count, () => Is.Zero);
        }

        [Test]
        public void TestAutoHitLifetimeEndIsDeterministicAcrossScrub()
        {
            double endAfterForward = 0;
            AddStep("seek past hit", () => manualClock.CurrentTime = 3200);
            AddUntilStep("note applied", () => note()?.Entry != null);
            AddStep("capture lifetime end", () => endAfterForward = note()!.Entry!.LifetimeEnd);
            AddAssert("lifetime end is finite (not immortal)", () => !double.IsInfinity(endAfterForward) && endAfterForward < double.MaxValue);

            AddStep("rewind then forward again", () =>
            {
                manualClock.CurrentTime = 0;
                manualClock.CurrentTime = 3200;
            });
            AddAssert("lifetime end unchanged (path-independent)",
                () => Math.Abs(note()!.Entry!.LifetimeEnd - endAfterForward) < 0.001);
        }
    }
}
