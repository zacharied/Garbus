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
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests.Visual.HitObjects
{
    [TestFixture]
    public partial class TestSceneChordHighlight : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private GarbusPlayfield playfield = null!;

        private void buildScene(params GarbusHitObject[] hitObjects) => buildScene(1f, false, hitObjects);

        private void buildScene(float contentScale, params GarbusHitObject[] hitObjects) => buildScene(contentScale, false, hitObjects);

        // contentScale applies an ancestor scale to the whole playfield subtree, mimicking how the editor
        // mini preview renders the playfield downscaled (contentScale < 1) — the case the connector must
        // detect and compensate for. autoHit spawns the drawables the way the mini preview does (forced Hit
        // at apply time), which the connector must still show while the chord scrolls in.
        private void buildScene(float contentScale, bool autoHit, params GarbusHitObject[] hitObjects)
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
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Scale = new Vector2(contentScale),
                        Child = new GarbusInputManager
                        {
                            Child = playfield = new GarbusPlayfield { Size = Vector2.One },
                        },
                    },
                },
            };

            foreach (var h in hitObjects)
                playfield.Add(PlayScreen.CreateDrawableRepresentation(h, autoHit));

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
        public void ConnectorFadesOutAfterReachingRingWhileNotesAlive()
        {
            // The connector is a scroll-in aid: it fades out once the chord reaches the ring (StartTime),
            // driven purely by the clock — NOT by judgement. The notes stay alive throughout, proving the fade
            // is neither judgement- nor despawn-driven. Seeks alone (no elapsed real time) exercise it, which
            // is exactly what makes it robust under editor scrubbing.
            AddStep("two cardinals at 2000ms", () => buildScene(
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);

            AddStep("seek to just before the ring", () => manualClock.CurrentTime = 1950);
            AddUntilStep("both alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));
            AddUntilStep("full opacity while approaching", () => visiblePaths().Count() == 1 && singlePath().Alpha == 1);

            // A little past the ring: still present, but no longer full (proves it fades, not snaps).
            AddStep("seek just past the ring", () => manualClock.CurrentTime = 2040);
            AddAssert("notes still alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));
            AddAssert("connector fading", () => visiblePaths().Any() && singlePath().Alpha < 1 && singlePath().Alpha > 0);

            // Past the fade duration, notes still alive: connector fully gone.
            AddStep("seek past the fade", () => manualClock.CurrentTime = 2300);
            AddAssert("notes still alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));
            AddAssert("connector gone", () => !visiblePaths().Any());
        }

        // The overlay's own local→screen scale (draw-quad width vs local width).
        private float overlayScreenScale() => overlay.ScreenSpaceDrawQuad.Width / overlay.DrawWidth;

        [Test]
        public void ConnectorKeepsDesignRadiusWhenNotDownscaled()
        {
            // Rendered at ≥1:1 the connector uses its plain design radius — gameplay is unchanged. The 4×
            // ancestor scale guarantees the overlay is not downscaled regardless of the headless window size.
            AddStep("two cardinals, upscaled host", () => buildScene(4f,
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);
            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("connector visible", () => visiblePaths().Count() == 1);

            AddAssert("not downscaled", () => overlayScreenScale() >= 1f);
            AddAssert("uses design radius", () =>
                Precision.AlmostEquals(singlePath().PathRadius, ChordColours.ConnectorPathRadius, 0.001f));
        }

        [Test]
        public void ConnectorCompensatesForDownscaleToHoldOnScreenThickness()
        {
            // The mini preview renders the playfield heavily downscaled. Rather than a "mini" flag, the overlay
            // detects its own local→screen scale and grows the local line so the ON-SCREEN thickness stays at
            // the design radius instead of shrinking to sub-pixel.
            AddStep("two cardinals, 0.25x host", () => buildScene(0.25f,
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);
            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("connector visible", () => visiblePaths().Count() == 1);

            AddAssert("is downscaled", () => overlayScreenScale() < 1f);
            AddAssert("local line thicker than design", () => singlePath().PathRadius > ChordColours.ConnectorPathRadius);
            AddAssert("on-screen thickness held at design radius", () =>
                Precision.AlmostEquals(singlePath().PathRadius * overlayScreenScale(), ChordColours.ConnectorPathRadius, 0.05f));
        }

        [Test]
        public void ConnectorVisibleForAutoHitChordWhileApproaching()
        {
            // Mini-preview parity: with auto-hit drawables (as the preview spawns them), the connector must
            // still show while the chord scrolls in and clear once past the ring — the presentation depends
            // only on the clock and which members are live, never on judgement state.
            AddStep("two auto-hit cardinals at 2000ms", () => buildScene(1f, autoHit: true,
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);

            AddStep("seek to mid-approach", () => manualClock.CurrentTime = 1990);
            AddUntilStep("both alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().Count(d => d.IsAlive) == 2);
            AddUntilStep("connector visible while approaching", () => visiblePaths().Count() == 1 && singlePath().Alpha == 1);

            // Past the ring-arrival time the connector freezes and fades; well past the 200ms fade it is gone.
            AddUntilStep("connector clears after the ring", () =>
            {
                manualClock.CurrentTime = System.Math.Min(2400, manualClock.CurrentTime + 50);
                return manualClock.CurrentTime >= 2400 && !visiblePaths().Any();
            });
        }

        [Test]
        public void AutoHitConnectorClearsAndRestoresUnderScrubbing()
        {
            // The mini preview scrubs the editor clock (jumps, pauses, rewinds). Because the presentation is a
            // pure function of the clock, scrubbing past the ring clears the line immediately (a real-time fade
            // transform would be stuck under a paused clock) and rewinding restores it — no stale lines.
            AddStep("two auto-hit cardinals at 2000ms", () => buildScene(1f, autoHit: true,
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);

            AddStep("seek to approach", () => manualClock.CurrentTime = 1900);
            AddUntilStep("visible while approaching", () => visiblePaths().Count() == 1 && singlePath().Alpha == 1);

            // Jump well past the ring + fade and hold there (clock not advancing). Pure-time alpha is 0 at once.
            AddStep("jump past ring + fade", () => manualClock.CurrentTime = 2500);
            AddUntilStep("cleared when scrubbed past", () => !visiblePaths().Any());

            AddStep("seek back into the approach", () => manualClock.CurrentTime = 1900);
            AddUntilStep("visible again after rewind", () => visiblePaths().Count() == 1 && singlePath().Alpha == 1);

            AddStep("seek far before the chord is alive", () => manualClock.CurrentTime = 0);
            AddUntilStep("cleared when not alive", () => !visiblePaths().Any());
        }

        [Test]
        public void DeletingChordMemberDisposesConnector()
        {
            // Mirrors deleting a chord note live in the mini preview: breaking the chord (2 → 1) removes it
            // from the chord index, and the overlay must dispose the orphaned connector path rather than leave
            // it on screen forever at its last alpha.
            CardinalNote a = null!;
            CardinalNote b = null!;

            AddStep("two auto-hit cardinals at 2000ms", () =>
            {
                a = new CardinalNote { AngleDeg = 90, StartTime = 2000 };
                b = new CardinalNote { AngleDeg = 270, StartTime = 2000 };
                buildScene(1f, autoHit: true, a, b);
            });
            AddUntilStep("loaded", () => playfield.IsLoaded);

            AddStep("seek to approach", () => manualClock.CurrentTime = 1900);
            AddUntilStep("connector visible", () => visiblePaths().Count() == 1);

            AddStep("delete one member (breaks the chord)", () =>
            {
                var drawable = playfield.AllHitObjects.OfType<DrawableCardinalNote>().Single(d => d.HitObject == b);
                playfield.Remove(drawable);
                playfield.SetHitObjects(new GarbusHitObject[] { a }); // rebuild chord index without b
            });

            AddUntilStep("connector path disposed", () =>
                !overlay.ChildrenOfType<osu.Framework.Graphics.Lines.SmoothPath>().Any());
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
