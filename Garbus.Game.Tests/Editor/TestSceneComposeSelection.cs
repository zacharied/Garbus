// Selection tests for GarbusHitObjectComposer + selection blueprints + GarbusSelectionHandler.
// Ported from BigAssCircle's TestSceneBacEditor selection coverage (TestSelectAndDragNote,
// TestSelectViaGhostTwin, TestSliderSelectionIsPathPrecise, TestInsertSliderNodeWithHotkey,
// TestSliderHidesSelectionBoxAndShowsChip) plus a delete + undo-restore proof.
//
// Harness mirrors TestSceneComposePlacement but additionally caches a GarbusChartChangeHandler (for the
// undo test) and drives the composer's scroll off the EditorClock (Composer input manager Clock =
// EditorClock) so stopping/seeking the clock deterministically pins the timeline drawables — otherwise
// the composer scrolls on the real frame clock and objects never hold still to be clicked. After each
// placement, settleWith() stops the clock on the object's StartTime, parking it mid-playfield.

using System;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Blueprints;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osu.Framework.Utils;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneComposeSelection : GarbusTestScene
    {
        private ComposeSelectionHarness harness = null!;
        private EditorChart editorChart = null!;
        private GarbusChartChangeHandler changeHandler = null!;

        private GarbusHitObjectComposer composer => harness.Composer;
        private GarbusEditorPlayfield playfield => composer.Playfield;
        private ManualInputManager input => harness.Input;
        private EditorClock editorClock => harness.EditorClock;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            editorChart = new EditorChart(chart);
            changeHandler = new GarbusChartChangeHandler(editorChart);

            Child = harness = new ComposeSelectionHarness(editorChart, changeHandler) { RelativeSizeAxes = Axes.Both };
        });

        private void waitForComposer() => AddUntilStep("wait for composer", () => harness.Composer?.IsLoaded == true);

        /// <summary>Screen position of an absolute angle on the grid (origin-independent via the mapping).</summary>
        private Vector2 positionAtAngle(float angleDeg, float yFrac = 0.5f)
        {
            var quad = playfield.ScreenSpaceDrawQuad;
            float xFrac = EditorAngleMapping.ToX(angleDeg);
            return new Vector2(quad.TopLeft.X + quad.Width * xFrac, quad.TopLeft.Y + quad.Height * yFrac);
        }

        /// <summary>
        /// The current on-screen centre of an object's editor drawable — the point the user visibly clicks
        /// to select it. Read straight off the drawable (rather than recomputing from the container) so the
        /// click lands on the sprite regardless of the drawable's exact vertical origin offset.
        /// </summary>
        private Vector2 screenPositionOf(GarbusHitObject hitObject)
        {
            var drawable = composer.HitObjects.First(d => d.HitObject == hitObject);
            return drawable.ScreenSpaceDrawQuad.Centre;
        }

        private T? placedObject<T>() where T : GarbusHitObject => editorChart.HitObjects.OfType<T>().FirstOrDefault();

        /// <summary>
        /// Re-targets the cursor onto <paramref name="target"/> (a live position, recomputed each frame)
        /// until some hit-object blueprint is hovered, then clicks. Robust against the drawable settling by
        /// a pixel or two after the seek.
        /// </summary>
        private void hoverThenClick(Func<Vector2> target)
        {
            AddUntilStep("hover blueprint", () =>
            {
                input.MoveMouseTo(target());
                return composer.ChildrenOfType<HitObjectSelectionBlueprint>().Any(b => b.IsHovered);
            });
            AddStep("click", () => input.Click(MouseButton.Left));
        }

        private void placeNoteAt(float angleDeg)
        {
            AddStep("select note tool", () => input.Key(Key.Number2));
            AddStep($"move to {angleDeg}", () => input.MoveMouseTo(positionAtAngle(angleDeg)));
            AddStep("click to place", () => input.Click(MouseButton.Left));
            AddAssert("note placed", () => placedObject<CardinalNote>() != null);
            settleWith(() => placedObject<GarbusHitObject>()!.StartTime);
        }

        /// <summary>
        /// Stops the clock and hard-seeks exactly onto <paramref name="objectTime"/> (clearing the
        /// post-placement smooth-seek transform). The composer's scroll runs off the EditorClock, so at the
        /// object's own StartTime it sits mid-playfield and holds still, keeping click positions valid.
        /// </summary>
        private void settleWith(Func<double> objectTime)
        {
            // Stop the clock and hard-seek exactly onto the object's start time (which clears the
            // post-placement smooth-seek transform). At its StartTime the object sits mid-playfield and
            // stays put, so click positions read off its drawable each frame remain valid.
            AddStep("stop and seek onto object", () =>
            {
                editorClock.Stop();
                editorClock.Seek(objectTime());
            });
            AddUntilStep("drawable position stable", () =>
            {
                var draw = composer.HitObjects.FirstOrDefault();
                if (draw == null) return false;
                var now = draw.ScreenSpaceDrawQuad.Centre;
                bool stable = Precision.AlmostEquals(now, lastStable, 0.01f);
                lastStable = now;
                return stable;
            });
        }

        private Vector2 lastStable;

        [Test]
        public void TestSelectNoteByClick()
        {
            waitForComposer();
            placeNoteAt(270);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
            AddAssert("note selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<CardinalNote>());
        }

        [Test]
        public void TestDragBoxSelectsObjects()
        {
            waitForComposer();

            AddStep("add two notes at same time + park clock", () =>
            {
                editorChart.Add(new CardinalNote { StartTime = 2000, AngleDeg = 180 });
                editorChart.Add(new CardinalNote { StartTime = 2000, AngleDeg = 0 });
                editorClock.Stop();
                editorClock.Seek(2000);
            });
            AddUntilStep("two drawables exist", () => composer.HitObjects.Count() == 2);
            AddStep("switch to select tool", () => input.Key(Key.Number1));

            AddStep("press on empty space above-left of notes", () =>
            {
                var (min, _) = notesBounds();
                input.MoveMouseTo(new Vector2(min.X - 30, min.Y - 30));
                input.PressButton(MouseButton.Left);
            });
            AddStep("drag box across both notes", () =>
            {
                var (_, max) = notesBounds();
                input.MoveMouseTo(new Vector2(max.X + 30, max.Y + 30));
            });
            // The editor clock is stopped (normal while editing). The drag box must still become visible —
            // it previously used a 250ms timed fade that never advanced on the frozen editor clock.
            AddAssert("drag box is visible while clock stopped", () =>
            {
                var db = composer.ChildrenOfType<ScrollingDragBox>().Single();
                return db.State == Visibility.Visible && db.Alpha > 0.9f && db.Box.DrawSize.X > 0 && db.Box.DrawSize.Y > 0;
            });
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));

            AddAssert("both notes selected by drag box", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(2));
        }

        /// <summary>Screen-space bounding corners (min, max) of the two placed note drawables.</summary>
        private (Vector2 min, Vector2 max) notesBounds()
        {
            var centres = composer.HitObjects.Select(d => d.ScreenSpaceDrawQuad.Centre).ToList();
            float minX = centres.Min(c => c.X), minY = centres.Min(c => c.Y);
            float maxX = centres.Max(c => c.X), maxY = centres.Max(c => c.Y);
            return (new Vector2(minX, minY), new Vector2(maxX, maxY));
        }

        [Test]
        public void TestDragRotatesBySnappedIncrement()
        {
            waitForComposer();
            placeNoteAt(270);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
            AddAssert("note selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<CardinalNote>());

            AddStep("start drag", () =>
            {
                input.MoveMouseTo(screenPositionOf(placedObject<CardinalNote>()!));
                input.PressButton(MouseButton.Left);
            });
            AddStep("drag one 45° increment right", () => input.MoveMouseTo(
                input.CurrentState.Mouse.Position + new Vector2(playfield.ScreenSpaceDrawQuad.Width * 45f / EditorAngleMapping.TOTAL_DEGREES, 0)));
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));
            AddAssert("note rotated to 315", () => placedObject<CardinalNote>()?.AngleDeg, () => Is.EqualTo(315));
        }

        [Test]
        public void TestSelectViaGhostTwin()
        {
            waitForComposer();
            // 105° is within GHOST_DEGREES of the left edge (90°), so once snapped it shows a twin in the right band.
            placeNoteAt(105);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() =>
            {
                var main = screenPositionOf(placedObject<CardinalNote>()!);
                float wrapOffset = playfield.ScreenSpaceDrawQuad.Width * 360f / EditorAngleMapping.TOTAL_DEGREES;
                return main + new Vector2(wrapOffset, 0);
            });
            AddAssert("note selected via twin", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<CardinalNote>());
        }

        [Test]
        public void TestGhostTwinHiddenUntilSelected()
        {
            waitForComposer();
            // 105° is within GHOST_DEGREES of the left edge (90°), so once snapped it shows a twin in the right band.
            placeNoteAt(105);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            AddUntilStep("twin outline exists", () => blueprintFor(placedObject<CardinalNote>()!).ChildrenOfType<EditSquarePiece>().Count() > 1);
            AddAssert("outlines hidden while not selected",
                () => blueprintFor(placedObject<CardinalNote>()!).ChildrenOfType<EditSquarePiece>().All(p => p.Alpha == 0));

            hoverThenClick(() =>
            {
                var main = screenPositionOf(placedObject<CardinalNote>()!);
                float wrapOffset = playfield.ScreenSpaceDrawQuad.Width * 360f / EditorAngleMapping.TOTAL_DEGREES;
                return main + new Vector2(wrapOffset, 0);
            });
            AddAssert("outlines visible once selected",
                () => blueprintFor(placedObject<CardinalNote>()!).ChildrenOfType<EditSquarePiece>().All(p => p.Alpha == 1));
        }

        private HitObjectSelectionBlueprint blueprintFor(GarbusHitObject hitObject)
            => composer.ChildrenOfType<HitObjectSelectionBlueprint>().Single(b => b.Item == hitObject);

        [Test]
        public void TestDeleteRemovesSelection()
        {
            waitForComposer();
            placeNoteAt(270);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
            AddAssert("note selected", () => editorChart.SelectedHitObjects.Count == 1);

            AddStep("press delete", () => input.Key(Key.Delete));
            AddAssert("note removed", () => placedObject<CardinalNote>() == null);
            AddAssert("selection cleared", () => editorChart.SelectedHitObjects.Count == 0);
        }

        [Test]
        public void TestBlueprintRemainsSelectableAfterUpdate()
        {
            // Pins the stale-DrawableObject fix: EditorChart.Update makes the composer swap the drawable
            // (remove + re-create). Without the HitObjectUpdated → TransferBlueprintFor refresh in
            // EditorBlueprintContainer, the blueprint would keep pointing at the disposed old drawable and
            // become unclickable. After an update the note must still be selectable.
            waitForComposer();
            placeNoteAt(270);

            AddStep("update the note (swaps its drawable)", () => editorChart.Update(placedObject<CardinalNote>()!));
            // re-settle: the swapped-in drawable starts life afresh; hold it still again.
            settleWith(() => placedObject<GarbusHitObject>()!.StartTime);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
            AddAssert("note still selectable after update", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<CardinalNote>());
        }

        [Test]
        public void TestUndoRestoresDeletedSelection()
        {
            waitForComposer();
            placeNoteAt(270);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
            AddStep("press delete", () => input.Key(Key.Delete));
            AddAssert("note removed", () => placedObject<CardinalNote>() == null);

            AddStep("undo", () => changeHandler.RestoreState(-1));
            AddAssert("note restored", () => placedObject<CardinalNote>() != null);
            AddAssert("restored at 270", () => placedObject<CardinalNote>()!.AngleDeg, () => Is.EqualTo(270));
        }

        // ------------------------------------------------------------------
        // Incremental drags (Phase4-Issues.md: drag positioning bug-out + GC churn).
        // A real drag is many small mouse moves, each of which may update the object and
        // (currently) recreate its drawable — unlike TestDragRotatesBySnappedIncrement's single jump.
        // ------------------------------------------------------------------

        /// <summary>One small mouse step to the right, in degrees of playfield width.</summary>
        private void dragStepRight(float degrees) => input.MoveMouseTo(
            input.CurrentState.Mouse.Position + new Vector2(playfield.ScreenSpaceDrawQuad.Width * degrees / EditorAngleMapping.TOTAL_DEGREES, 0));

        [Test]
        public void TestLongIncrementalDragTracksCursor()
        {
            waitForComposer();
            placeNoteAt(270);

            int updateCount = 0;
            AddStep("count updates", () => editorChart.HitObjectUpdated += _ => updateCount++);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
            AddAssert("note selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<CardinalNote>());

            AddStep("press mouse on note", () =>
            {
                input.MoveMouseTo(screenPositionOf(placedObject<CardinalNote>()!));
                input.PressButton(MouseButton.Left);
            });
            // +90° total in 30 small steps, like a real mouse drag.
            AddRepeatStep("drag 3° right", () => dragStepRight(3), 30);
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));

            AddAssert("note followed cursor to 0", () => placedObject<CardinalNote>()?.AngleDeg, () => Is.EqualTo(0));
            AddAssert("single drawable remains", () => composer.HitObjects.Count(), () => Is.EqualTo(1));
            AddAssert("update churn bounded", () => updateCount, () => Is.LessThanOrEqualTo(8));
        }

        [Test]
        public void TestIncrementalDragAcrossSeamTracksCursor()
        {
            waitForComposer();
            // 45° is grid-degrees 315; dragging +90° crosses the wrap seam (grid 360/0 at absolute
            // 90°) and continues through the right ghost band. The note must land on 135 (the
            // cursor's snapped angle) — and the drag must not fire an update per mouse-move event
            // while the cursor sits a full wrap (360°) from the primary copy: that raw delta must
            // reduce to "no change", not to a fresh update+drawable-recreate every event (the
            // Phase4 GC-storm feeder).
            placeNoteAt(45);

            int updateCount = 0;
            AddStep("count updates", () => editorChart.HitObjectUpdated += _ => updateCount++);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
            AddAssert("note selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<CardinalNote>());

            AddStep("press mouse on note", () =>
            {
                input.MoveMouseTo(screenPositionOf(placedObject<CardinalNote>()!));
                input.PressButton(MouseButton.Left);
            });
            AddRepeatStep("drag 3° right", () => dragStepRight(3), 30);
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));

            AddAssert("note followed cursor across seam to 135", () => placedObject<CardinalNote>()?.AngleDeg, () => Is.EqualTo(135));
            AddAssert("single drawable remains", () => composer.HitObjects.Count(), () => Is.EqualTo(1));
            AddAssert("update churn bounded", () => updateCount, () => Is.LessThanOrEqualTo(8));
        }

        [Test]
        public void TestUpdateRefreshesDrawableInPlace()
        {
            // EditorChart.Update must NOT recreate the drawable: DrawableHitObject already re-applies
            // itself in place via HitObject.DefaultsApplied (rebuilding nested drawables), the scrolling
            // container re-layouts through the same event, and the editor visuals read the hit object
            // live every frame. Recreation churned framebuffer-backed visuals per update — at drag rates
            // that was the slider node-drag GC storm (ISSUES.md).
            waitForComposer();
            placeNoteAt(270);

            Gameplay.Objects.Drawables.DrawableHitObject drawable = null!;
            AddStep("capture drawable", () => drawable = composer.HitObjects.Single());
            AddStep("update note", () => editorChart.Update(placedObject<CardinalNote>()!));
            AddAssert("same drawable instance", () => composer.HitObjects.Single(), () => Is.SameAs(drawable));
        }

        [Test]
        public void TestRemovedObjectDrawableIsDisposed()
        {
            // Pins the zombie-drawable fix on the delete path: non-pooled drawables are detached with
            // RemoveInternal(…, false), so the composer must Dispose() them explicitly — an undisposed
            // drawable stays subscribed to HitObject.DefaultsApplied forever.
            waitForComposer();
            placeNoteAt(270);

            Gameplay.Objects.Drawables.DrawableHitObject oldDrawable = null!;
            AddStep("capture drawable", () => oldDrawable = composer.HitObjects.Single());
            AddStep("remove note", () => editorChart.Remove(placedObject<CardinalNote>()!));
            AddAssert("old drawable disposed", () =>
                (bool)typeof(Drawable).GetProperty("IsDisposed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
                    .GetValue(oldDrawable)!);
        }

        [Test]
        public void TestSliderNodeDragDoesNotRecreateDrawable()
        {
            // The ISSUES.md repro: a slider wrapping past 360°, its child node dragged back and forth on
            // the x-axis. Every drag event updates the slider; the drawable (whose polyline wrap copies
            // are framebuffer-backed) must survive the whole drag as the same instance instead of being
            // torn down and rebuilt per mouse-move event.
            waitForComposer();
            placeDiagonalSlider();

            AddStep("wrap slider past 360°", () =>
            {
                placedObject<SliderBody>()!.Path.ControlPoints[0].RotationOffset = 400;
                editorChart.Update(placedObject<SliderBody>()!);
            });

            Gameplay.Objects.Drawables.DrawableHitObject drawable = null!;
            AddStep("capture drawable", () => drawable = composer.HitObjects.Single());

            AddStep("select slider via head", () =>
            {
                var blueprint = composer.ChildrenOfType<SliderSelectionBlueprint>().Single();
                input.MoveMouseTo(blueprint.ScreenSpaceSelectionPoint);
                input.Click(MouseButton.Left);
            });
            AddAssert("slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());

            AddStep("press mouse on node handle", () =>
            {
                // A slider wrapped past 360° has multiple visible wrap-copy handles per node — grab the
                // primary (raw) one to drag.
                var handle = composer.ChildrenOfType<NodeDragPiece>().First(h => h.WrapK == 0);
                input.MoveMouseTo(handle);
                input.PressButton(MouseButton.Left);
            });
            AddRepeatStep("wiggle right", () => dragStepRight(4), 8);
            AddRepeatStep("wiggle left", () => dragStepRight(-4), 8);
            AddRepeatStep("wiggle right again", () => dragStepRight(4), 8);
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));

            AddAssert("drawable never recreated", () => composer.HitObjects.Single(), () => Is.SameAs(drawable));
            AddAssert("slider intact", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));
        }

        [Test]
        public void TestExtendingSliderDurationKeepsBodyAlive()
        {
            // GAR-4: dragging a slider's terminal node to a much later time extends its duration.
            // The editor drawable swallows its own LifetimeEnd (it never self-expires), so the
            // scrolling container is the sole lifetime authority — but it only recomputed the
            // top-level object's lifetime on add / full layout invalidation, never when the end
            // time changed. The body therefore kept the pre-edit lifetime and vanished the moment
            // the playhead passed the OLD terminal time, even though it now extends much further.
            waitForComposer();

            AddStep("add short slider + park clock at start", () =>
            {
                var path = new GarbusPath { ControlPoints = new osu.Framework.Bindables.BindableList<GarbusPathControlPoint>() };
                path.ControlPoints.Add(new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 45 });
                editorChart.Add(new SliderBody { StartTime = 2000, AngleDeg = 270, Side = HorizontalDirection.Left, Path = path });
                editorClock.Stop();
                editorClock.Seek(2000);
            });
            AddUntilStep("drawable exists", () => composer.HitObjects.Any());

            // Extend the terminal node far beyond the 5000ms scroll window (old end 2500 → new end 12000).
            AddStep("extend terminal node to +10000ms", () =>
            {
                var slider = placedObject<SliderBody>()!;
                slider.Path.ControlPoints[0].TimeOffset = 10000;
                editorChart.Update(slider);
            });

            // Seek to a time well inside the new duration but far past old-end + scroll window (7500).
            // The body extends to StartTime+10000 = 12000, so at 11500 it must still be visible (alive).
            AddStep("seek near new end", () => editorClock.Seek(11500));
            AddAssert("slider body still alive", () =>
                composer.HitObjects.OfType<EditorDrawableSliderBody>().Any(d => d.IsAlive));
        }

        [Test]
        public void TestHoldNoteSelectableByHead()
        {
            // ISSUES.md: the hold head sprite is centred on the start line, so its bottom half hangs
            // below the drawable's duration rectangle — clicking there must still select the note.
            waitForComposer();

            AddStep("add hold + park clock", () =>
            {
                editorChart.Add(new CardinalHoldNote { StartTime = 2000, Duration = 1000, AngleDeg = 270 });
                editorClock.Stop();
                editorClock.Seek(2000);
            });

            AddUntilStep("drawable exists", () => composer.HitObjects.Any());
            AddStep("switch to select tool", () => input.Key(Key.Number1));

            AddStep("click lower half of head (below start line)", () =>
            {
                var quad = composer.HitObjects.Single().ScreenSpaceDrawQuad;
                // bottom edge = start time; the head sprite extends half a note below it.
                var target = new Vector2(quad.Centre.X, quad.BottomLeft.Y + 8);
                input.MoveMouseTo(target);
                input.Click(MouseButton.Left);
            });

            AddAssert("hold selected via head", () =>
                editorChart.SelectedHitObjects.SingleOrDefault() is CardinalHoldNote);
        }

        [Test]
        public void TestShoulderHoldSelectableByHead()
        {
            // Mirrors TestHoldNoteSelectableByHead for the shoulder-lane hold variant.
            waitForComposer();

            AddStep("add shoulder hold + park clock", () =>
            {
                editorChart.Add(new ShoulderHoldNote { StartTime = 2000, Duration = 1000, Side = HorizontalDirection.Right });
                editorClock.Stop();
                editorClock.Seek(2000);
            });

            AddUntilStep("drawable exists", () => composer.HitObjects.Any());
            AddStep("switch to select tool", () => input.Key(Key.Number1));

            AddStep("click lower half of head (below start line)", () =>
            {
                var quad = composer.HitObjects.Single().ScreenSpaceDrawQuad;
                // bottom edge = start time; the head sprite extends half a note below it.
                var target = new Vector2(quad.Centre.X, quad.BottomLeft.Y + 8);
                input.MoveMouseTo(target);
                input.Click(MouseButton.Left);
            });

            AddAssert("shoulder hold selected via head", () =>
                editorChart.SelectedHitObjects.SingleOrDefault() is ShoulderHoldNote);
        }

        [Test]
        public void TestNoHitsoundWhileScrubbing()
        {
            // ISSUES.md: objects must not play hitsounds while the compose view is scrubbed (clock
            // stopped, playhead seeking across objects) — only when the playhead crosses them with
            // the clock actually running.
            waitForComposer();

            AddStep("add note ahead + park clock", () =>
            {
                editorClock.Stop();
                editorClock.Seek(0);
                editorChart.Add(new CardinalNote { StartTime = 8000, AngleDeg = 270 });
            });

            AddUntilStep("drawable exists", () => composer.HitObjects.Any());

            AddStep("scrub past the note", () => editorClock.Seek(9000));
            AddUntilStep("note judged", () => composer.HitObjects.Single().Judged);
            AddAssert("no hitsound while scrubbing", () =>
                composer.HitObjects.Single().ChildrenOfType<Gameplay.Audio.HitSoundContainer>().Single().PlayCount, () => Is.Zero);

            AddStep("add second note + play through it", () =>
            {
                editorChart.Add(new CardinalNote { StartTime = 9500, AngleDeg = 0 });
                editorClock.Seek(9400);
                editorClock.Start();
            });

            AddUntilStep("clock passed second note", () => editorClock.CurrentTime > 9600);
            AddStep("stop clock", () => editorClock.Stop());

            AddAssert("hitsound played during playback", () =>
                composer.HitObjects.Single(d => d.HitObject.StartTime == 9500)
                        .ChildrenOfType<Gameplay.Audio.HitSoundContainer>().Single().PlayCount, () => Is.EqualTo(1));
        }

        [Test]
        public void TestNoHitsoundForSliderNodesWhileScrubbing()
        {
            // ISSUES.md follow-up: slider nodes are nested-stub drawables that bypassed the editor
            // base's scrub gate and still sounded on wheel-seeks.
            waitForComposer();

            AddStep("add slider ahead + park clock", () =>
            {
                var path = new GarbusPath { ControlPoints = new osu.Framework.Bindables.BindableList<GarbusPathControlPoint>() };
                path.ControlPoints.Add(new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 45 });
                editorChart.Add(new SliderBody { StartTime = 8000, AngleDeg = 270, Side = HorizontalDirection.Left, Path = path });
                editorClock.Stop();
                editorClock.Seek(0);
            });

            AddUntilStep("drawable exists", () => composer.HitObjects.Any());

            AddStep("scrub past the whole slider", () => editorClock.Seek(9000));
            AddUntilStep("slider judged", () => composer.HitObjects.Single().Judged);
            AddAssert("no hitsound from slider or its nodes", () =>
                composer.HitObjects.Single().ChildrenOfType<Gameplay.Audio.HitSoundContainer>()
                        .Sum(s => s.PlayCount), () => Is.Zero);
        }

        [Test]
        public void TestDirectionToggleButtonFlipsView()
        {
            waitForComposer();

            AddAssert("starts CCW / not reversed", () => !composer.ReverseAngleView.Value);
            AddAssert("button shows CCW", () =>
                composer.ChildrenOfType<AngleDirectionToggleButton>().Single().LabelText == "⇄ CCW");

            AddStep("click the toggle", () =>
            {
                var button = composer.ChildrenOfType<AngleDirectionToggleButton>().Single();
                input.MoveMouseTo(button.ScreenSpaceDrawQuad.Centre);
                input.Click(MouseButton.Left);
            });

            AddAssert("now reversed", () => composer.ReverseAngleView.Value);
            AddAssert("button shows CW", () =>
                composer.ChildrenOfType<AngleDirectionToggleButton>().Single().LabelText == "⇄ CW");
        }

        // ------------------------------------------------------------------
        // Slider selection: path-precise + T-key node insertion.
        // ------------------------------------------------------------------

        private void placeDiagonalSlider()
        {
            AddStep("select slider tool", () => input.Key(Key.Number8));
            // head at South (270°, early), one node at East (0°, later).
            AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.7f)));
            AddStep("click body", () => input.Click(MouseButton.Left));
            AddStep("move to node", () => input.MoveMouseTo(positionAtAngle(0, 0.4f)));
            AddStep("click node", () => input.Click(MouseButton.Left));
            AddStep("right click to commit", () => input.Click(MouseButton.Right));
            AddAssert("slider placed", () => placedObject<SliderBody>() != null);
            settleWith(() => placedObject<SliderBody>()!.StartTime);
            AddStep("switch to select tool", () => input.Key(Key.Number1));
        }

        /// <summary>Screen positions of a single-node slider's head and its node.</summary>
        private (Vector2 head, Vector2 node) sliderEndsScreen()
        {
            var slider = placedObject<SliderBody>()!;
            var cp = slider.Path.ControlPoints[0];
            var container = playfield.HitObjectContainer;

            Vector2 headScreen = container.ScreenSpacePositionAtTime(slider.StartTime);
            headScreen.X = container.ToScreenSpace(new Vector2(EditorAngleMapping.ToX(slider.AngleDeg) * container.DrawWidth, 0)).X;

            Vector2 nodeScreen = container.ScreenSpacePositionAtTime(slider.StartTime + cp.TimeOffset);
            nodeScreen.X = container.ToScreenSpace(new Vector2(EditorAngleMapping.ToX(slider.AngleDeg + cp.RotationOffset) * container.DrawWidth, 0)).X;

            return (headScreen, nodeScreen);
        }

        /// <summary>The single-node slider's one control point.</summary>
        private GarbusPathControlPoint firstControlPoint() => placedObject<SliderBody>()!.Path.ControlPoints[0];

        /// <summary>Adds a second control point directly (later in time, different angle) and refreshes.</summary>
        private void addSecondNode()
        {
            AddStep("add second node", () =>
            {
                var slider = placedObject<SliderBody>()!;
                slider.Path.ControlPoints.Add(new GarbusPathControlPoint { TimeOffset = slider.Path.ControlPoints[0].TimeOffset + 250, RotationOffset = 90 });
                editorChart.Update(slider);
            });
            settleWith(() => placedObject<SliderBody>()!.StartTime);
        }

        /// <summary>Screen centre of the node handle for control-point index <paramref name="i"/> that
        /// currently sits inside the main grid — the one users visibly click. Wound sliders (offset ≥ 360°)
        /// draw their raw copy off-screen; the on-grid wrap copy is what actually renders in the playfield.</summary>
        private Vector2 nodeHandleScreen(int i)
        {
            var playfieldQuad = playfield.ScreenSpaceDrawQuad;
            var candidates = composer.ChildrenOfType<NodeDragPiece>().Where(h => h.CpIndex == i).ToList();
            var onGrid = candidates.FirstOrDefault(h => playfieldQuad.Contains(h.ScreenSpaceDrawQuad.Centre));
            return (onGrid ?? candidates[0]).ScreenSpaceDrawQuad.Centre;
        }

        private SliderSelectionBlueprint sliderBlueprint() => composer.ChildrenOfType<SliderSelectionBlueprint>().Single();

        /// <summary>A flip context-menu item by its exact label, or null (requires a selected blueprint hovered).</summary>
        private GarbusMenuItem? flipMenuItem(string text)
        {
            var handler = composer.ChildrenOfType<GarbusSelectionHandler>().Single();
            return handler.ContextMenuItems
                          .OfType<GarbusMenuItem>()
                          .FirstOrDefault(i => i.Text.Value.ToString() == text);
        }

        /// <summary>Places a slam-edge (instant placement) at an angle and returns to the select tool.</summary>
        private void placeSlamEdgeAt(float angleDeg)
        {
            AddStep("select slam-edge tool", () => input.Key(Key.Number7));
            AddStep("place slam edge", () =>
            {
                input.MoveMouseTo(positionAtAngle(angleDeg, 0.5f));
                input.Click(MouseButton.Left);
            });
            AddAssert("slam edge placed", () => placedObject<GarbusSlamEdge>() != null);
            settleWith(() => placedObject<GarbusSlamEdge>()!.StartTime);
            AddStep("switch to select tool", () => input.Key(Key.Number1));
        }

        /// <summary>Places a centered slam (instant placement) at an angle and returns to the select tool.</summary>
        private void placeSlamCenteredAt(float angleDeg)
        {
            AddStep("select slam-centered tool", () => input.Key(Key.Number6));
            AddStep("place slam centered", () =>
            {
                input.MoveMouseTo(positionAtAngle(angleDeg, 0.5f));
                input.Click(MouseButton.Left);
            });
            AddAssert("slam centered placed", () => placedObject<GarbusSlamCentered>() != null);
            settleWith(() => placedObject<GarbusSlamCentered>()!.StartTime);
            AddStep("switch to select tool", () => input.Key(Key.Number1));
        }

        /// <summary>Selects the slider by clicking the midpoint of its head→node line.</summary>
        private void selectSliderOnLine()
        {
            AddStep("select slider on its line", () =>
            {
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo((headScreen + nodeScreen) / 2);
                input.Click(MouseButton.Left);
            });
            AddAssert("slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
        }

        /// <summary>Screen X of the primary (WrapK 0) node handle for control-point <paramref name="i"/>, or null.</summary>
        private float? primaryNodeHandleX(int i)
        {
            var handle = composer.ChildrenOfType<NodeDragPiece>().FirstOrDefault(h => h.CpIndex == i && h.WrapK == 0);
            return handle?.ScreenSpaceDrawQuad.Centre.X;
        }

        [Test]
        public void TestSliderNodeHandleRendersAtItsTrueAngleUnderReversedView()
        {
            waitForComposer();

            // Head at West (180°, on the E–W reflection axis so its own column is identical in both views),
            // one node +45° CCW → absolute 225°, which stays mid-grid (no ghost band) in both directions.
            AddStep("add slider + park clock", () =>
            {
                var path = new BindableList<GarbusPathControlPoint>
                {
                    new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 45 },
                };
                editorChart.Add(new SliderBody
                {
                    StartTime = 2000,
                    AngleDeg = 180,
                    Side = HorizontalDirection.Left,
                    Path = new GarbusPath { ControlPoints = path },
                });
                editorClock.Stop();
                editorClock.Seek(2000);
            });
            AddUntilStep("slider drawable exists", () => composer.HitObjects.Any());
            settleWith(() => placedObject<SliderBody>()!.StartTime);

            AddStep("select slider via head", () =>
            {
                input.MoveMouseTo(sliderBlueprint().ScreenSpaceSelectionPoint);
                input.Click(MouseButton.Left);
            });
            AddAssert("slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());

            // Normal (CCW) view: the node (absolute 225°) renders at the 225° column, to the RIGHT of the head.
            AddUntilStep("node handle at 225° column (normal)", () =>
                primaryNodeHandleX(0) is float x && Precision.AlmostEquals(x, positionAtAngle(225).X, 3f));
            AddAssert("node right of head (normal)", () =>
                primaryNodeHandleX(0) > positionAtAngle(180).X);

            AddStep("reverse the view (CW)", () => composer.ReverseAngleView.Value = true);

            // Reversed view: the node must STILL render at its true 225° column — now to the LEFT of the head.
            // Before the fix the handle stayed at head + 45·pxPerDeg (the CCW side): inverted horizontal motion.
            AddUntilStep("node handle at 225° column (reversed)", () =>
                primaryNodeHandleX(0) is float x && Precision.AlmostEquals(x, positionAtAngle(225).X, 3f));
            AddAssert("node left of head (reversed)", () =>
                primaryNodeHandleX(0) < positionAtAngle(180).X);
        }

        [Test]
        public void TestClickingNodeOnUnselectedSliderSelectsWholeSlider()
        {
            waitForComposer();
            placeDiagonalSlider();

            // slider is not selected; clicking its node handle position must select the whole slider, no node.
            AddStep("click the node position", () =>
            {
                var (_, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo(nodeScreen);
                input.Click(MouseButton.Left);
            });
            AddAssert("whole slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
            AddAssert("no node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
        }

        [Test]
        public void TestClickingHandleOnSelectedSliderSelectsNode()
        {
            waitForComposer();
            placeDiagonalSlider();
            selectSliderOnLine();

            AddStep("click the node handle", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(0));
                input.Click(MouseButton.Left);
            });
            AddAssert("slider still selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
            AddAssert("that node selected", () => sliderBlueprint().SelectedNodes.Single(), () => Is.SameAs(firstControlPoint()));
        }

        [Test]
        public void TestClickingGhostBandCloneOfNodeSelectsIt()
        {
            // Regression: when a slider crosses the seam, its outline draws once per visible wrap copy
            // (raw + ghost-band clones). Historically only ONE of those copies got a NodeDragPiece —
            // whichever landed in the main grid — so clicking a visible clone on the far seam did
            // nothing. Every visible wrap copy must now be independently clickable.
            waitForComposer();

            // Head near the left seam (grid 20°, absolute 110°) with a node RotationOffset of −50° puts the
            // node's raw copy in the left ghost band and its k=+1 clone deep into the right main-grid area.
            AddStep("add seam-crossing slider + park clock", () =>
            {
                var path = new osu.Framework.Bindables.BindableList<GarbusPathControlPoint>
                {
                    new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = -50 },
                };
                editorChart.Add(new SliderBody
                {
                    StartTime = 2000,
                    AngleDeg = 110, // grid 20
                    Side = HorizontalDirection.Left,
                    Path = new GarbusPath { ControlPoints = path },
                });
                editorClock.Stop();
                editorClock.Seek(2000);
            });
            AddUntilStep("drawable exists", () => composer.HitObjects.Any());
            settleWith(() => placedObject<SliderBody>()!.StartTime);
            AddStep("switch to select tool", () => input.Key(Key.Number1));
            AddStep("select slider via head", () =>
            {
                input.MoveMouseTo(sliderBlueprint().ScreenSpaceSelectionPoint);
                input.Click(MouseButton.Left);
            });
            AddAssert("slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());

            AddAssert("multiple wrap-copy handles exist", () =>
                composer.ChildrenOfType<NodeDragPiece>().Count(h => h.CpIndex == 0), () => Is.GreaterThan(1));

            AddStep("click a non-primary wrap copy of the node", () =>
            {
                var clone = composer.ChildrenOfType<NodeDragPiece>().First(h => h.CpIndex == 0 && h.WrapK != 0);
                input.MoveMouseTo(clone.ScreenSpaceDrawQuad.Centre);
                input.Click(MouseButton.Left);
            });
            AddAssert("that node is selected via the clone", () =>
                sliderBlueprint().SelectedNodes.SingleOrDefault(), () => Is.SameAs(firstControlPoint()));
        }

        [Test]
        public void TestCtrlClickTogglesNodesInSelection()
        {
            waitForComposer();
            placeDiagonalSlider();
            addSecondNode();
            selectSliderOnLine();

            AddStep("click node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
            AddStep("ctrl+click node 1", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(1));
                input.PressKey(Key.LControl);
                input.Click(MouseButton.Left);
                input.ReleaseKey(Key.LControl);
            });
            AddAssert("both nodes selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(2));

            AddStep("ctrl+click node 1 again", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(1));
                input.PressKey(Key.LControl);
                input.Click(MouseButton.Left);
                input.ReleaseKey(Key.LControl);
            });
            AddAssert("node 1 toggled off", () => sliderBlueprint().SelectedNodes.Single(), () => Is.SameAs(placedObject<SliderBody>()!.Path.ControlPoints[0]));
        }

        [Test]
        public void TestClickingLineClearsNodeSelection()
        {
            waitForComposer();
            placeDiagonalSlider();
            selectSliderOnLine();

            AddStep("click node handle", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
            AddAssert("node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

            AddStep("click the line", () =>
            {
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo((headScreen + nodeScreen) / 2);
                input.Click(MouseButton.Left);
            });
            AddAssert("slider still selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
            AddAssert("node selection cleared", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
        }

        [Test]
        public void TestDeselectingSliderClearsNodeSelection()
        {
            waitForComposer();
            placeDiagonalSlider();
            selectSliderOnLine();

            AddStep("click node handle", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
            AddAssert("node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

            AddStep("deselect all", () => editorChart.SelectedHitObjects.Clear());
            AddAssert("node selection cleared", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
        }

        [Test]
        public void TestSliderSelectableOnlyOnPolylineAndNodes()
        {
            waitForComposer();
            placeDiagonalSlider();

            // a corner of the bounding box (head x, node y) is well off the diagonal line but inside its AABB.
            AddStep("click empty space in bounds", () =>
            {
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo(new Vector2(headScreen.X, nodeScreen.Y));
                input.Click(MouseButton.Left);
            });
            AddAssert("slider NOT selected off-line", () => !editorChart.SelectedHitObjects.Contains(placedObject<SliderBody>()!));

            // the midpoint of head→node lies on the segment.
            AddStep("click on the line", () =>
            {
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo((headScreen + nodeScreen) / 2);
                input.Click(MouseButton.Left);
            });
            AddAssert("slider selected on-line", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
        }

        [Test]
        public void TestTInsertsTimeOrderedNode()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(Key.Number8));
            AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.7f)));
            AddStep("click body", () => input.Click(MouseButton.Left));
            AddStep("move to node", () => input.MoveMouseTo(positionAtAngle(270, 0.3f)));
            AddStep("click node", () => input.Click(MouseButton.Left));
            AddStep("right click to commit", () => input.Click(MouseButton.Right));
            AddAssert("slider placed", () => placedObject<SliderBody>() != null);

            settleWith(() => placedObject<SliderBody>()!.StartTime);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            AddStep("move to slider line", () =>
            {
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo((headScreen + nodeScreen) / 2);
            });
            AddStep("click to select", () => input.Click(MouseButton.Left));
            AddAssert("slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());

            AddStep("move cursor to mid-duration", () =>
            {
                var slider = placedObject<SliderBody>()!;
                var container = playfield.HitObjectContainer;
                var screen = container.ScreenSpacePositionAtTime(slider.StartTime + slider.Duration / 2);
                screen.X = positionAtAngle(315).X;
                input.MoveMouseTo(screen);
            });
            AddStep("press T", () => input.Key(Key.T));
            AddAssert("node inserted", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(2));
            AddAssert("nodes remain time-ordered", () =>
            {
                var cps = placedObject<SliderBody>()!.Path.ControlPoints;
                return cps[0].TimeOffset < cps[1].TimeOffset;
            });
        }

        [Test]
        public void TestTInsertAtExistingNodeTimeCreatesArc()
        {
            waitForComposer();
            placeDiagonalSlider();   // one node at time > 0
            selectSliderOnLine();

            // Move the cursor to the existing node's exact time but a different angle column, then press T.
            AddStep("move cursor to node time, new angle", () =>
            {
                var slider = placedObject<SliderBody>()!;
                var container = playfield.HitObjectContainer;
                var screen = container.ScreenSpacePositionAtTime(slider.StartTime + slider.Path.ControlPoints[0].TimeOffset);
                screen.X = positionAtAngle(315).X;
                input.MoveMouseTo(screen);
            });
            AddStep("press T", () => input.Key(Key.T));

            AddAssert("second node inserted",
                () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(2));
            AddAssert("the two nodes share a time (horizontal arc), not collapsed onto the head", () =>
            {
                var cps = placedObject<SliderBody>()!.Path.ControlPoints;
                return cps[0].TimeOffset == cps[1].TimeOffset && cps[0].TimeOffset > 0;
            });
        }

        [Test]
        public void TestSliderSideContextMenuTogglesAndUndoes()
        {
            waitForComposer();
            placeDiagonalSlider();

            AddStep("select slider on its line", () =>
            {
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo((headScreen + nodeScreen) / 2);
                input.Click(MouseButton.Left);
            });
            AddAssert("slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
            AddAssert("slider starts Left", () => placedObject<SliderBody>()!.Side, () => Is.EqualTo(HorizontalDirection.Left));

            TernaryStateToggleMenuItem sideItem = null!;
            AddUntilStep("Side menu item available while hovered", () =>
            {
                // keep the cursor on the line so the blueprint stays hovered (ContextMenuItems gates on hover).
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo((headScreen + nodeScreen) / 2);
                sideItem = sliderSideMenuItem();
                return sideItem != null;
            });
            AddAssert("item reflects Left (unchecked)", () => sideItem.State.Value, () => Is.EqualTo(TernaryState.False));

            AddStep("invoke Side toggle", () => sideItem.Action.Value?.Invoke());
            AddAssert("slider now Right", () => placedObject<SliderBody>()!.Side, () => Is.EqualTo(HorizontalDirection.Right));
            AddUntilStep("polyline recoloured to Right", () =>
            {
                var visual = composer.ChildrenOfType<SliderPolylineVisual>().FirstOrDefault();
                return visual != null && visual.Colour.Equals(Constants.RightColour);
            });

            AddStep("undo", () => changeHandler.RestoreState(-1));
            AddAssert("slider back to Left", () => placedObject<SliderBody>()!.Side, () => Is.EqualTo(HorizontalDirection.Left));

            AddStep("redo", () => changeHandler.RestoreState(1));
            AddAssert("slider Right again", () => placedObject<SliderBody>()!.Side, () => Is.EqualTo(HorizontalDirection.Right));
        }

        [Test]
        public void TestSKeyTogglesSlamSide()
        {
            // The S-key side toggle applies to every IHasSide object, not just sliders — here a centered slam.
            waitForComposer();
            placeSlamCenteredAt(270);

            hoverThenClick(() => screenPositionOf(placedObject<GarbusSlamCentered>()!));
            AddAssert("slam selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<GarbusSlamCentered>());
            AddAssert("slam starts Left", () => placedObject<GarbusSlamCentered>()!.Side, () => Is.EqualTo(HorizontalDirection.Left));

            AddStep("press S", () => input.Key(Key.S));
            AddAssert("slam now Right", () => placedObject<GarbusSlamCentered>()!.Side, () => Is.EqualTo(HorizontalDirection.Right));

            AddStep("press S again", () => input.Key(Key.S));
            AddAssert("slam back to Left", () => placedObject<GarbusSlamCentered>()!.Side, () => Is.EqualTo(HorizontalDirection.Left));

            AddStep("undo", () => changeHandler.RestoreState(-1));
            AddAssert("slam Right after undo", () => placedObject<GarbusSlamCentered>()!.Side, () => Is.EqualTo(HorizontalDirection.Right));
        }

        [Test]
        public void TestSKeyDoesNotToggleShoulderSide()
        {
            // Shoulders have a Side but it's positional (it picks the lane/button), so they intentionally
            // don't implement IHasSide and the S-key must leave them untouched.
            waitForComposer();

            AddStep("select shoulder tool", () => input.Key(Key.Number4));
            AddStep("place shoulder (West)", () =>
            {
                input.MoveMouseTo(positionAtAngle(180, 0.5f));
                input.Click(MouseButton.Left);
            });
            AddAssert("shoulder placed", () => placedObject<ShoulderNote>() != null);
            settleWith(() => placedObject<ShoulderNote>()!.StartTime);
            AddStep("switch to select tool", () => input.Key(Key.Number1));

            hoverThenClick(() => screenPositionOf(placedObject<ShoulderNote>()!));
            AddAssert("shoulder selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(1));

            HorizontalDirection sideBefore = default;
            AddStep("snapshot side", () => sideBefore = placedObject<ShoulderNote>()!.Side);

            AddStep("press S", () => input.Key(Key.S));
            AddAssert("shoulder side unchanged", () => placedObject<ShoulderNote>()!.Side, () => Is.EqualTo(sideBefore));
        }

        [Test]
        public void TestSideContextMenuAbsentForNote()
        {
            waitForComposer();
            placeNoteAt(270);

            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
            AddAssert("note selected", () => editorChart.SelectedHitObjects.Count == 1);

            AddAssert("no Side item for a note", () =>
            {
                var (headScreen, _) = (screenPositionOf(placedObject<CardinalNote>()!), Vector2.Zero);
                input.MoveMouseTo(headScreen);
                return sliderSideMenuItem() == null;
            });
        }

        private TernaryStateToggleMenuItem? sliderSideMenuItem()
        {
            var handler = composer.ChildrenOfType<GarbusSelectionHandler>().Single();
            return handler.ContextMenuItems
                          .OfType<TernaryStateToggleMenuItem>()
                          .FirstOrDefault(i => i.Text.Value.ToString() == "Right side");
        }

        [Test]
        public void TestSliderHidesSelectionBoxAndShowsChip()
        {
            waitForComposer();
            placeDiagonalSlider();

            AddStep("select slider on its line", () =>
            {
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo((headScreen + nodeScreen) / 2);
                input.Click(MouseButton.Left);
            });
            AddAssert("slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());

            Func<GarbusSelectionHandler> handler = () => composer.ChildrenOfType<GarbusSelectionHandler>().Single();
            AddUntilStep("compose selection box hidden", () => handler().ChildrenOfType<SelectionBox>().Single().Alpha == 0);
            AddUntilStep("count chip shown", () => handler().ChildrenOfType<GarbusSelectionHandler.SliderCountChip>().Single().Alpha == 1);
        }

        [Test]
        public void TestGroupDragMovesAllSelectedNodesByTheSameAngle()
        {
            waitForComposer();
            placeDiagonalSlider();      // node 0 at rotation offset from head
            addSecondNode();            // node 1 at +250ms, rotation 90
            selectSliderOnLine();

            AddStep("select node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
            AddStep("ctrl+select node 1", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(1));
                input.PressKey(Key.LControl);
                input.Click(MouseButton.Left);
                input.ReleaseKey(Key.LControl);
            });
            AddAssert("both selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(2));

            int rot0Before = 0, rot1Before = 0;
            AddStep("snapshot rotations", () =>
            {
                var cps = placedObject<SliderBody>()!.Path.ControlPoints;
                rot0Before = cps[0].RotationOffset;
                rot1Before = cps[1].RotationOffset;
            });

            AddStep("press mouse on node 0 handle", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(0));
                input.PressButton(MouseButton.Left);
            });
            AddRepeatStep("drag 3° right", () => dragStepRight(3), 15);   // +45°
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));

            AddAssert("both nodes rotated by the same +45°", () =>
            {
                var cps = placedObject<SliderBody>()!.Path.ControlPoints;
                return cps[0].RotationOffset == rot0Before + 45 && cps[1].RotationOffset == rot1Before + 45;
            });
        }

        [Test]
        public void TestDraggingUnselectedHandleMovesOnlyThatNode()
        {
            waitForComposer();
            placeDiagonalSlider();
            addSecondNode();
            selectSliderOnLine();

            int rot1Before = 0;
            AddStep("snapshot node 1 rotation", () => rot1Before = placedObject<SliderBody>()!.Path.ControlPoints[1].RotationOffset);

            // No node explicitly selected yet; pressing node 0's handle selects it, then dragging moves only it.
            AddStep("press mouse on node 0 handle", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(0));
                input.PressButton(MouseButton.Left);
            });
            AddRepeatStep("drag 3° right", () => dragStepRight(3), 15);
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));

            AddAssert("only node 0 selected", () => sliderBlueprint().SelectedNodes.Single(), () => Is.SameAs(placedObject<SliderBody>()!.Path.ControlPoints[0]));
            AddAssert("node 1 unchanged", () => placedObject<SliderBody>()!.Path.ControlPoints[1].RotationOffset, () => Is.EqualTo(rot1Before));
        }

        [Test]
        public void TestDraggingNodeOntoNeighbourTimeCreatesArc()
        {
            waitForComposer();
            placeDiagonalSlider();   // one node at time > 0
            addSecondNode();         // second node at node0 + 250ms
            selectSliderOnLine();

            AddStep("select node 1", () => { input.MoveMouseTo(nodeHandleScreen(1)); input.Click(MouseButton.Left); });
            AddAssert("node 1 selected",
                () => sliderBlueprint().SelectedNodes.Single(), () => Is.SameAs(placedObject<SliderBody>()!.Path.ControlPoints[1]));

            AddStep("press node 1 handle", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(1));
                input.PressButton(MouseButton.Left);
            });
            // Drag node 1 up in time onto node 0's time (same beat) — collapsing the link to a horizontal arc.
            AddStep("drag onto node 0's time", () =>
            {
                var slider = placedObject<SliderBody>()!;
                var container = playfield.HitObjectContainer;
                var target = container.ScreenSpacePositionAtTime(slider.StartTime + slider.Path.ControlPoints[0].TimeOffset);
                target.X = nodeHandleScreen(1).X;
                input.MoveMouseTo(target);
            });
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));

            AddAssert("nodes now share a time (horizontal arc)", () =>
            {
                var cps = placedObject<SliderBody>()!.Path.ControlPoints;
                return cps[0].TimeOffset == cps[1].TimeOffset && cps[0].TimeOffset > 0;
            });
        }

        [Test]
        public void TestDeleteRemovesSelectedNodeNotWholeSlider()
        {
            waitForComposer();
            placeDiagonalSlider();
            addSecondNode();            // now two control points
            selectSliderOnLine();

            var node0 = new System.Func<GarbusPathControlPoint>(() => placedObject<SliderBody>()!.Path.ControlPoints[0]);

            AddStep("select node 1", () => { input.MoveMouseTo(nodeHandleScreen(1)); input.Click(MouseButton.Left); });
            AddAssert("one node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

            var remaining = new GarbusPathControlPoint[1];
            AddStep("remember node 0", () => remaining[0] = node0());

            AddStep("press delete", () => input.Key(Key.Delete));
            AddAssert("slider survives", () => placedObject<SliderBody>() != null);
            AddAssert("one control point left", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));
            AddAssert("the other node remains", () => placedObject<SliderBody>()!.Path.ControlPoints[0], () => Is.SameAs(remaining[0]));
            AddAssert("node selection cleared", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
        }

        [Test]
        public void TestDeleteUndoRestoresNode()
        {
            waitForComposer();
            placeDiagonalSlider();
            addSecondNode();
            selectSliderOnLine();

            AddStep("select node 1", () => { input.MoveMouseTo(nodeHandleScreen(1)); input.Click(MouseButton.Left); });
            AddStep("press delete", () => input.Key(Key.Delete));
            AddAssert("one control point left", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));

            AddStep("undo", () => changeHandler.RestoreState(-1));
            AddAssert("both control points restored", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(2));
        }

        [Test]
        public void TestDeletingLastNodeRemovesWholeSlider()
        {
            waitForComposer();
            placeDiagonalSlider();      // single control point
            selectSliderOnLine();

            AddStep("select the only node", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
            AddAssert("node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

            AddStep("press delete", () => input.Key(Key.Delete));
            AddAssert("slider removed entirely", () => placedObject<SliderBody>() == null);
        }

        [Test]
        public void TestDeleteWithNoNodeSelectedRemovesWholeSlider()
        {
            waitForComposer();
            placeDiagonalSlider();
            selectSliderOnLine();       // slider selected, but no node picked

            AddAssert("no node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
            AddStep("press delete", () => input.Key(Key.Delete));
            AddAssert("whole slider removed", () => placedObject<SliderBody>() == null);
        }

        [Test]
        public void TestQuickDeleteHoveredNodeRemovesOnlyThatNode()
        {
            waitForComposer();
            placeDiagonalSlider();
            addSecondNode();
            selectSliderOnLine();

            AddStep("shift+right-click node 1 handle", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(1));
                input.PressKey(Key.LShift);
                input.Click(MouseButton.Right);
                input.ReleaseKey(Key.LShift);
            });
            AddAssert("slider survives", () => placedObject<SliderBody>() != null);
            AddAssert("one control point left", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));
        }

        [Test]
        public void TestQuickDeleteOnLineRemovesWholeSlider()
        {
            waitForComposer();
            placeDiagonalSlider();
            addSecondNode();
            selectSliderOnLine();

            AddStep("shift+right-click the line (not a handle)", () =>
            {
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo((headScreen + nodeScreen) / 2);
                input.PressKey(Key.LShift);
                input.Click(MouseButton.Right);
                input.ReleaseKey(Key.LShift);
            });
            AddAssert("whole slider removed", () => placedObject<SliderBody>() == null);
        }

        // ------------------------------------------------------------------
        // Flip: node-aware reflection primitive + "Flip selection" menu item.
        // ------------------------------------------------------------------

        [Test]
        public void TestFlipSelectionMirrorsWholeSlider()
        {
            waitForComposer();
            placeDiagonalSlider(); // head South (270), one node East (0) → RotationOffset 90.
            selectSliderOnLine();

            AddAssert("head 270 before", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
            AddAssert("offset 90 before", () => firstControlPoint().RotationOffset, () => Is.EqualTo(90));

            GarbusMenuItem flip = null!;
            AddUntilStep("Flip selection available", () =>
            {
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo((headScreen + nodeScreen) / 2); // keep a selected blueprint hovered
                flip = flipMenuItem("Flip selection")!;
                return flip != null;
            });

            AddStep("invoke Flip selection", () => flip.Action.Value?.Invoke());
            // Rigid mirror about the swept-extent centre: head and node swap absolute angles, offset negates.
            AddAssert("head now 0", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(0));
            AddAssert("offset now -90", () => firstControlPoint().RotationOffset, () => Is.EqualTo(-90));

            AddStep("undo", () => changeHandler.RestoreState(-1));
            AddAssert("head restored", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
            AddAssert("offset restored", () => firstControlPoint().RotationOffset, () => Is.EqualTo(90));

            AddStep("redo", () => changeHandler.RestoreState(1));
            AddAssert("head re-flipped", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(0));
        }

        [Test]
        public void TestFlipSelectionSingleNodeIsNoOp()
        {
            waitForComposer();
            placeDiagonalSlider();
            selectSliderOnLine();

            AddStep("select node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
            AddAssert("one node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

            GarbusMenuItem flip = null!;
            AddUntilStep("Flip selection available", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(0)); // node handle keeps the slider blueprint hovered
                flip = flipMenuItem("Flip selection")!;
                return flip != null;
            });

            // Mirroring a single node about itself changes nothing: head fixed, offset unchanged.
            AddStep("invoke Flip selection", () => flip.Action.Value?.Invoke());
            AddAssert("head unchanged", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
            AddAssert("offset unchanged", () => firstControlPoint().RotationOffset, () => Is.EqualTo(90));
        }

        [Test]
        public void TestFlipSelectionFlipsSlamEdgeDirection()
        {
            waitForComposer();
            placeSlamEdgeAt(45);

            hoverThenClick(() => screenPositionOf(placedObject<GarbusSlamEdge>()!));
            AddAssert("slam edge selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(1));

            int angleBefore = 0;
            AddStep("snapshot angle", () => angleBefore = placedObject<GarbusSlamEdge>()!.AngleDeg);
            AddAssert("starts clockwise", () => placedObject<GarbusSlamEdge>()!.Direction, () => Is.EqualTo(RotationalDirection.Clockwise));

            GarbusMenuItem flip = null!;
            AddUntilStep("Flip selection available", () =>
            {
                input.MoveMouseTo(screenPositionOf(placedObject<GarbusSlamEdge>()!));
                flip = flipMenuItem("Flip selection")!;
                return flip != null;
            });

            AddStep("invoke Flip selection", () => flip.Action.Value?.Invoke());
            // Single object: angle unchanged (mirror about itself), handedness reverses.
            AddAssert("angle unchanged", () => placedObject<GarbusSlamEdge>()!.AngleDeg, () => Is.EqualTo(angleBefore));
            AddAssert("now anticlockwise", () => placedObject<GarbusSlamEdge>()!.Direction, () => Is.EqualTo(RotationalDirection.Anticlockwise));

            AddStep("undo", () => changeHandler.RestoreState(-1));
            AddAssert("clockwise again", () => placedObject<GarbusSlamEdge>()!.Direction, () => Is.EqualTo(RotationalDirection.Clockwise));
        }

        [Test]
        public void TestFlipSelectionSwapsTwoNotes()
        {
            waitForComposer();
            // Default AngleSnap (45°) would round 60/120 to 45/135; use a finer snap so placement lands
            // exactly on the angles the identity-swap assertions below depend on.
            AddStep("use 15° angle snap", () => composer.AngleSnap.Value = 15);
            placeNoteAt(60);
            placeNoteAt(120);

            // Capture the two note instances by their starting angle so identity (not list order) proves the swap.
            CardinalNote note60 = null!, note120 = null!;
            AddStep("capture notes", () =>
            {
                var notes = editorChart.HitObjects.OfType<CardinalNote>().ToList();
                note60 = notes.Single(n => n.AngleDeg == 60);
                note120 = notes.Single(n => n.AngleDeg == 120);
            });

            AddStep("select all", () =>
            {
                input.PressKey(Key.LControl);
                input.Key(Key.A);
                input.ReleaseKey(Key.LControl);
            });
            AddAssert("two selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(2));

            GarbusMenuItem flip = null!;
            AddUntilStep("Flip selection available", () =>
            {
                input.MoveMouseTo(screenPositionOf(note120));
                flip = flipMenuItem("Flip selection")!;
                return flip != null;
            });

            // Handle set {60,120}, centre 90, sum 180: the two notes exchange angles. A pure swap preserves the
            // angle *set*, so identity (these specific instances) is what proves the flip — not a set comparison.
            AddStep("invoke Flip selection", () => flip.Action.Value?.Invoke());
            AddAssert("the 60 note is now 120", () => note60.AngleDeg, () => Is.EqualTo(120));
            AddAssert("the 120 note is now 60", () => note120.AngleDeg, () => Is.EqualTo(60));
        }

        [Test]
        public void TestFlipMenuItemsPresentForNote()
        {
            waitForComposer();
            placeNoteAt(270);
            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
            AddAssert("note selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(1));

            AddAssert("both flip items present", () =>
            {
                input.MoveMouseTo(screenPositionOf(placedObject<CardinalNote>()!));
                return flipMenuItem("Flip selection") != null && flipMenuItem("Flip around angle...") != null;
            });
        }

        [Test]
        public void TestFlipAroundAngleReflectsNote()
        {
            waitForComposer();
            placeNoteAt(90); // North
            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
            AddAssert("note selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(1));

            GarbusMenuItem flip = null!;
            AddUntilStep("Flip around angle available", () =>
            {
                input.MoveMouseTo(screenPositionOf(placedObject<CardinalNote>()!));
                flip = flipMenuItem("Flip around angle...")!;
                return flip != null;
            });

            AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
            AddStep("move pivot bar to East (0°)", () => input.MoveMouseTo(positionAtAngle(0, 0.5f)));
            AddStep("click to commit", () => input.Click(MouseButton.Left));

            // Reflect 90 about pivot 0 (sum 0) → 270 (South).
            AddAssert("note now South (270)", () => placedObject<CardinalNote>()!.AngleDeg, () => Is.EqualTo(270));
        }

        [Test]
        public void TestFlipAroundAngleEscapeCancels()
        {
            waitForComposer();
            placeNoteAt(90);
            AddStep("switch to select tool", () => input.Key(Key.Number1));
            hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));

            GarbusMenuItem flip = null!;
            AddUntilStep("Flip around angle available", () =>
            {
                input.MoveMouseTo(screenPositionOf(placedObject<CardinalNote>()!));
                flip = flipMenuItem("Flip around angle...")!;
                return flip != null;
            });

            AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
            AddStep("move pivot bar to East", () => input.MoveMouseTo(positionAtAngle(0, 0.5f)));
            AddStep("press Escape", () => input.Key(Key.Escape));
            AddStep("click where the bar was", () => input.Click(MouseButton.Left));

            AddAssert("note unchanged (still 90)", () => placedObject<CardinalNote>()!.AngleDeg, () => Is.EqualTo(90));
        }

        [Test]
        public void TestFlipAroundAngleReflectsSelectedNode()
        {
            waitForComposer();
            placeDiagonalSlider(); // head 270, node 0 at absolute 0 (offset 90)
            selectSliderOnLine();
            AddStep("select node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
            AddAssert("one node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

            GarbusMenuItem flip = null!;
            AddUntilStep("Flip around angle available", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(0));
                flip = flipMenuItem("Flip around angle...")!;
                return flip != null;
            });

            AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
            AddStep("move pivot bar to North (90°)", () => input.MoveMouseTo(positionAtAngle(90, 0.5f)));
            AddStep("click to commit", () => input.Click(MouseButton.Left));

            // Node abs 0 reflected about pivot 90 (sum 180) → abs 180; offset from head 270 = MinimalDiff(270,180) = -90.
            AddAssert("head unchanged", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
            AddAssert("node offset now -90", () => firstControlPoint().RotationOffset, () => Is.EqualTo(-90));
        }

        [Test]
        public void TestFlipAroundAngleWoundNodeIsInvolution()
        {
            waitForComposer();
            placeDiagonalSlider();   // head 270, node 0 at absolute 0 (offset 90)
            selectSliderOnLine();

            // Wind the node the long way (offset 90 -> 450, same absolute angle, +1 full turn).
            AddStep("wind node to +450", () =>
            {
                firstControlPoint().RotationOffset = 450;
                editorChart.Update(placedObject<SliderBody>()!);
            });
            AddStep("select node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
            AddAssert("one node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

            GarbusMenuItem flip = null!;
            AddUntilStep("Flip around angle available", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(0));
                flip = flipMenuItem("Flip around angle...")!;
                return flip != null;
            });

            // First flip about North (90). axisOff = fold(MinimalDiff(270,90)=180) = 0, S = 0 → 450 -> -450.
            AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
            AddStep("move pivot to North (90)", () => input.MoveMouseTo(positionAtAngle(90, 0.5f)));
            AddStep("commit", () => input.Click(MouseButton.Left));
            AddAssert("offset reflected to -450 (winding preserved, not collapsed)",
                () => firstControlPoint().RotationOffset, () => Is.EqualTo(-450));

            // Second identical flip returns exactly to +450 (true involution).
            AddStep("select node 0 again", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
            AddUntilStep("Flip around angle available", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(0));
                flip = flipMenuItem("Flip around angle...")!;
                return flip != null;
            });
            AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
            AddStep("move pivot to North (90)", () => input.MoveMouseTo(positionAtAngle(90, 0.5f)));
            AddStep("commit", () => input.Click(MouseButton.Left));
            AddAssert("offset back to +450", () => firstControlPoint().RotationOffset, () => Is.EqualTo(450));
        }

        [Test]
        public void TestFlipSelectionWoundNodeIsNoOp()
        {
            waitForComposer();
            placeDiagonalSlider();   // head 270, node offset 90
            selectSliderOnLine();

            AddStep("wind node to +450", () =>
            {
                firstControlPoint().RotationOffset = 450;
                editorChart.Update(placedObject<SliderBody>()!);
            });
            AddStep("select node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });

            GarbusMenuItem flip = null!;
            AddUntilStep("Flip selection available", () =>
            {
                input.MoveMouseTo(nodeHandleScreen(0));
                flip = flipMenuItem("Flip selection")!;
                return flip != null;
            });

            // Single selected node: S = 2*offset, so newOffset = offset — an exact no-op preserving winding.
            AddStep("invoke Flip selection", () => flip.Action.Value?.Invoke());
            AddAssert("offset unchanged (+450, winding preserved)",
                () => firstControlPoint().RotationOffset, () => Is.EqualTo(450));
        }

        [Test]
        public void TestFlipAroundAngleFlipsShoulderSideAcrossVerticalAxis()
        {
            waitForComposer();

            // Place a shoulder on the West side; read back whatever Side placement assigned.
            AddStep("select shoulder tool", () => input.Key(Key.Number4));
            AddStep("place shoulder (West)", () =>
            {
                input.MoveMouseTo(positionAtAngle(180, 0.5f));
                input.Click(MouseButton.Left);
            });
            AddAssert("shoulder placed", () => placedObject<ShoulderNote>() != null);
            settleWith(() => placedObject<ShoulderNote>()!.StartTime);
            AddStep("switch to select tool", () => input.Key(Key.Number1));

            hoverThenClick(() => screenPositionOf(placedObject<ShoulderNote>()!));
            AddAssert("shoulder selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(1));

            HorizontalDirection sideBefore = default;
            AddStep("snapshot side", () => sideBefore = placedObject<ShoulderNote>()!.Side);

            // Flip about North (90°): a vertical-ish axis swaps East<->West, so Side must flip.
            GarbusMenuItem flip = null!;
            AddUntilStep("Flip around angle available", () =>
            {
                input.MoveMouseTo(screenPositionOf(placedObject<ShoulderNote>()!));
                flip = flipMenuItem("Flip around angle...")!;
                return flip != null;
            });
            AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
            AddStep("move pivot to North (90°)", () => input.MoveMouseTo(positionAtAngle(90, 0.5f)));
            AddStep("commit", () => input.Click(MouseButton.Left));
            AddAssert("side flipped", () => placedObject<ShoulderNote>()!.Side, () => Is.Not.EqualTo(sideBefore));

            // Flip again about East (0°): a horizontal axis (N<->S) leaves E/W untouched, so Side is unchanged.
            HorizontalDirection sideMid = default;
            AddStep("snapshot side", () => sideMid = placedObject<ShoulderNote>()!.Side);
            AddUntilStep("Flip around angle available again", () =>
            {
                input.MoveMouseTo(screenPositionOf(placedObject<ShoulderNote>()!));
                flip = flipMenuItem("Flip around angle...")!;
                return flip != null;
            });
            AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
            AddStep("move pivot to East (0°)", () => input.MoveMouseTo(positionAtAngle(0, 0.5f)));
            AddStep("commit", () => input.Click(MouseButton.Left));
            AddAssert("side unchanged", () => placedObject<ShoulderNote>()!.Side, () => Is.EqualTo(sideMid));
        }

        [Test]
        public void TestReverseAngleViewMovesNorthToCentre()
        {
            waitForComposer();
            placeNoteAt(90); // North

            AddAssert("North starts off-centre", () =>
                Math.Abs(EditorAngleMapping.ToX(90) - 0.5f) > 0.1f);

            AddStep("reverse the view", () => composer.ReverseAngleView.Value = true);

            AddAssert("Direction flipped", () => EditorAngleMapping.Direction, () => Is.EqualTo(-1));
            AddAssert("North now centred", () =>
                Math.Abs(EditorAngleMapping.ToX(90) - 0.5f) < 1e-4f);
            AddUntilStep("North drawable tracks to centre", () =>
            {
                var d = composer.HitObjects.OfType<EditorDrawableCardinalNote>().FirstOrDefault();
                return d != null && Math.Abs(d.X - 0.5f) < 0.01f;
            });
            AddUntilStep("grid label at centre reads N", () => centreCardinalLabel() == "N");
        }

        [Test]
        public void TestReverseAngleViewFlipsGridLabelAtCentre()
        {
            // Guards against the AngleGrid drawing frozen labels: before the toggle the grid centre must
            // read South, and it must flip to North once the view is reversed — this inspects the actual
            // rendered SpriteText output of GarbusEditorPlayfield's AngleGrid, not just the angle-mapping
            // math, so a grid that never regenerates (or reads a stale Direction) fails this test.
            waitForComposer();

            AddUntilStep("grid label at centre reads S", () => centreCardinalLabel() == "S");

            AddStep("reverse the view", () => composer.ReverseAngleView.Value = true);

            AddUntilStep("grid label at centre reads N", () => centreCardinalLabel() == "N");
        }

        [Test]
        public void TestSlamEdgeArrowFlipsWithViewDirection()
        {
            // The edge-slam arrow points sideways along the timeline in its rotational sense. Because the
            // reversed view reflects the angle axis (CCW↔CW), the arrow must flip horizontally too —
            // otherwise a clockwise slam still visually points the CCW way under the CW view.
            waitForComposer();
            placeSlamEdgeAt(45);

            AddAssert("placed clockwise", () => placedObject<GarbusSlamEdge>()!.Direction,
                () => Is.EqualTo(RotationalDirection.Clockwise));

            // Normal (CCW) view: a clockwise slam points left (Rotation -90).
            AddUntilStep("arrow points left (normal)", () =>
                slamArrowRotation() is float r && Precision.AlmostEquals(r, -90, 0.1f));

            AddStep("reverse the view (CW)", () => composer.ReverseAngleView.Value = true);

            // Reversed view reflects the axis: the same clockwise slam now points right (Rotation +90).
            AddUntilStep("arrow points right (reversed)", () =>
                slamArrowRotation() is float r && Precision.AlmostEquals(r, 90, 0.1f));
        }

        /// <summary>The live <c>Rotation</c> of the edge-slam arrow sprite, or <c>null</c> if none placed.</summary>
        private float? slamArrowRotation() => composer.HitObjects
            .OfType<EditorDrawableGarbusSlamEdge>()
            .FirstOrDefault()?
            .ChildrenOfType<EditorSpritePiece>()
            .FirstOrDefault()?.Rotation;

        [Test]
        public void TestSlamEdgeArrowRecoloursLiveWithSideChange()
        {
            // Like TestSliderSideContextMenuTogglesAndUndoes, but for the slam-edge arrow: EditorChart.Update
            // re-Apply()s the drawable in place rather than recreating it, so the arrow's Colour must be read
            // live from HitObject.Side every frame, not just once in CreateVisual().
            waitForComposer();
            placeSlamEdgeAt(45);

            AddAssert("placed Left", () => placedObject<GarbusSlamEdge>()!.Side, () => Is.EqualTo(HorizontalDirection.Left));
            AddUntilStep("arrow coloured Left", () =>
                composer.ChildrenOfType<EditorDrawableGarbusSlamEdge>().FirstOrDefault()?
                    .ChildrenOfType<EditorSpritePiece>().FirstOrDefault()?.Colour.Equals(Constants.LeftColour) == true);

            AddStep("flip side to Right", () =>
            {
                placedObject<GarbusSlamEdge>()!.Side = HorizontalDirection.Right;
                editorChart.Update(placedObject<GarbusSlamEdge>()!);
            });
            AddUntilStep("arrow recoloured to Right", () =>
                composer.ChildrenOfType<EditorDrawableGarbusSlamEdge>().FirstOrDefault()?
                    .ChildrenOfType<EditorSpritePiece>().FirstOrDefault()?.Colour.Equals(Constants.RightColour) == true);
        }

        /// <summary>
        /// The <c>Text</c> of the single-letter cardinal <see cref="SpriteText"/> the AngleGrid draws
        /// nearest the playfield's horizontal centre (x ≈ 0.5), or <c>null</c> if none is close enough.
        /// </summary>
        private string? centreCardinalLabel() => composer.Playfield
            .ChildrenOfType<SpriteText>()
            .Where(t => t.Text.ToString().Length == 1 && "NESW".Contains(t.Text.ToString()))
            .OrderBy(t => Math.Abs(t.X - 0.5f))
            .FirstOrDefault(t => Math.Abs(t.X - 0.5f) < 0.02f)?.Text.ToString();

        // ------------------------------------------------------------------
        // Harness: caches the DI deps the composer tree requires, then hosts
        // the real GarbusHitObjectComposer as its child.
        // ------------------------------------------------------------------

        private partial class ComposeSelectionHarness : Container
        {
            private readonly EditorChart editorChart;
            private readonly GarbusChartChangeHandler changeHandler;
            private DependencyContainer dependencies = null!;

            public GarbusHitObjectComposer Composer { get; private set; } = null!;
            public ManualInputManager Input { get; private set; } = null!;
            public EditorClock EditorClock { get; private set; } = null!;

            public ComposeSelectionHarness(EditorChart editorChart, GarbusChartChangeHandler changeHandler)
            {
                this.editorChart = editorChart;
                this.changeHandler = changeHandler;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                var beatDivisor = new BindableBeatDivisor(4);
                EditorClock = new EditorClock(editorChart.ControlPointInfo, 60000, beatDivisor);
                EditorClock.ChangeSource(new TrackVirtual(60000));

                dependencies.Cache(editorChart);
                dependencies.Cache(EditorClock);
                dependencies.Cache(beatDivisor);
                dependencies.CacheAs<IEditorChangeHandler>(changeHandler);

                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = Input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    // Drive the composer's scrolling off the EditorClock (as the real editor transport does)
                    // so stopping/seeking the clock deterministically positions the timeline drawables.
                    Clock = EditorClock,
                    Child = Composer = new GarbusHitObjectComposer { RelativeSizeAxes = Axes.Both },
                };
                AddInternal(EditorClock);
            }
        }
    }
}
