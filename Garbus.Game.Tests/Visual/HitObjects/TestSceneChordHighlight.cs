using System.Linq;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Visual.HitObjects
{
    [TestFixture]
    public partial class TestSceneChordHighlight : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private GarbusPlayfield playfield = null!;

        private void buildScene(params GarbusHitObject[] hitObjects)
        {
            manualClock = new ManualClock { Rate = 1 };

            foreach (var h in hitObjects)
                h.ApplyDefaults();

            Child = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = new GarbusInputManager
                    {
                        Child = playfield = new GarbusPlayfield { Size = Vector2.One },
                    },
                },
            };

            foreach (var h in hitObjects)
                playfield.Add(PlayScreen.CreateDrawableRepresentation(h));

            playfield.SetHitObjects(hitObjects);
        }

        private DrawableCardinalNote cardinalAt(int angle) =>
            playfield.AllHitObjects.OfType<DrawableCardinalNote>().Single(d => d.HitObject.AngleDeg == angle);

        [Test]
        public void CoincidentPairIsYellow()
        {
            AddStep("two cardinals at 2000ms", () => buildScene(
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);

            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("both alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));

            AddAssert("north yellow", () => cardinalAt(90).Colour, () => Is.EqualTo((ColourInfo)ChordColours.Highlight));
            AddAssert("south yellow", () => cardinalAt(270).Colour, () => Is.EqualTo((ColourInfo)ChordColours.Highlight));
        }

        [Test]
        public void LoneCardinalIsWhite()
        {
            AddStep("single cardinal", () => buildScene(new CardinalNote { AngleDeg = 90, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);
            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().Any(d => d.IsAlive));

            AddAssert("white", () => cardinalAt(90).Colour, () => Is.EqualTo((ColourInfo)Colour4.White));
        }

        private ChordConnectorOverlay overlay =>
            playfield.ChildrenOfType<ChordConnectorOverlay>().Single();

        private System.Collections.Generic.IEnumerable<osu.Framework.Graphics.Lines.SmoothPath> visiblePaths() =>
            overlay.ChildrenOfType<osu.Framework.Graphics.Lines.SmoothPath>().Where(p => p.IsPresent);

        [Test]
        public void ConnectorAppearsForAlivePairAndClearsAfterDespawn()
        {
            AddStep("two cardinals at 2000ms", () => buildScene(
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);

            AddAssert("no connector before spawn", () => !visiblePaths().Any());

            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("both alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));
            AddUntilStep("connector visible", () => visiblePaths().Count() == 1);

            // Walk well past the notes so they auto-miss and despawn; the connector must clear.
            AddUntilStep("play past despawn", () =>
            {
                manualClock.CurrentTime = System.Math.Min(6000, manualClock.CurrentTime + 200);
                return manualClock.CurrentTime >= 6000
                       && playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => !d.IsAlive);
            });
            AddUntilStep("connector cleared", () => !visiblePaths().Any());
        }

        private osu.Framework.Graphics.Lines.SmoothPath singlePath() =>
            overlay.ChildrenOfType<osu.Framework.Graphics.Lines.SmoothPath>().Single();

        [Test]
        public void ConnectorFadesOutAtJudgementWhileNotesStillAlive()
        {
            AddStep("two cardinals at 2000ms", () => buildScene(
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);

            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("both alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));
            AddUntilStep("connector at full opacity", () => visiblePaths().Count() == 1 && singlePath().Alpha == 1);

            // Step past the 173ms miss window so the notes auto-miss (judged) but are still mid-fade (alive).
            AddUntilStep("all judged, still alive", () =>
            {
                manualClock.CurrentTime = System.Math.Min(2400, manualClock.CurrentTime + 40);
                var notes = playfield.AllHitObjects.OfType<DrawableCardinalNote>().ToList();
                return notes.All(d => d.State.Value != Gameplay.Objects.Drawables.ArmedState.Idle && d.IsAlive);
            });

            // A little into the fade: still present, but no longer at full opacity (proves it fades, not snaps).
            AddStep("advance into fade", () => manualClock.CurrentTime += 60);
            AddAssert("connector fading", () => visiblePaths().Any() && singlePath().Alpha < 1);

            // Past the fade duration but well before the notes' 1000ms miss fade ends: connector fully gone.
            AddStep("advance past fade", () => manualClock.CurrentTime += 400);
            AddAssert("notes still alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));
            AddAssert("connector gone", () => !visiblePaths().Any());
        }

        [Test]
        public void ConnectorHasVertexPerMemberForThreeNoteChord()
        {
            AddStep("three cardinals at 2000ms", () => buildScene(
                new CardinalNote { AngleDeg = 0, StartTime = 2000 },
                new CardinalNote { AngleDeg = 120, StartTime = 2000 },
                new CardinalNote { AngleDeg = 240, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);
            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("all alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));

            // Closed triangle: 3 members + repeat of the first vertex = 4 points.
            AddUntilStep("closed triangle", () => visiblePaths().Any()
                && visiblePaths().Single().Vertices.Count == 4);
        }
    }
}
