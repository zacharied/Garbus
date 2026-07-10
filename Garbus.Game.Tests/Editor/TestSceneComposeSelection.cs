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
using Garbus.Game.Edit.Compose;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
            // 150° is within GHOST_DEGREES of the left edge (135°), so once snapped it shows a twin in the right band.
            placeNoteAt(150);

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
        // Slider selection: path-precise + T-key node insertion.
        // ------------------------------------------------------------------

        private void placeDiagonalSlider()
        {
            AddStep("select slider tool", () => input.Key(Key.Number7));
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
            AddStep("select slider tool", () => input.Key(Key.Number7));
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
