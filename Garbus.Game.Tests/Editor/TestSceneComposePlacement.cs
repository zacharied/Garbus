// Placement tests for GarbusHitObjectComposer + tools + placement blueprints.
// Ported from BigAssCircle's TestSceneBacEditor placement patterns (positionAtAngle/screenPositionOf
// helpers). Hosts the full composer in a DI harness (the pattern from TestSceneComposerLifecycle /
// TestSceneEditorPlayfield) and drives it with a nested ManualInputManager.
//
// Auto-seek gotcha: HitObjectPlacementBlueprint.EndPlacement seeks the clock to the placed object,
// which scrolls it toward the judgement line — wait for the seek before asserting screen positions.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Core;
using Garbus.Game.Edit;
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
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneComposePlacement : GarbusTestScene
    {
        private ComposePlacementHarness harness = null!;
        private EditorChart editorChart = null!;

        private GarbusHitObjectComposer composer => harness.Composer;
        private GarbusEditorPlayfield playfield => composer.Playfield;
        private ManualInputManager input => harness.Input;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            editorChart = new EditorChart(chart);

            Child = harness = new ComposePlacementHarness(editorChart) { RelativeSizeAxes = Axes.Both };
        });

        private void waitForComposer() => AddUntilStep("wait for composer", () => harness.Composer?.IsLoaded == true);

        /// <summary>Screen position of an absolute angle on the grid (origin-independent via the mapping).</summary>
        private Vector2 positionAtAngle(float angleDeg, float yFrac = 0.5f)
        {
            var quad = playfield.ScreenSpaceDrawQuad;
            float xFrac = EditorAngleMapping.ToX(angleDeg);
            return new Vector2(quad.TopLeft.X + quad.Width * xFrac, quad.TopLeft.Y + quad.Height * yFrac);
        }

        [Test]
        public void TestPlaceCardinalNote()
        {
            waitForComposer();
            AddStep("select note tool", () => input.Key(Key.Number2));
            // slightly off the South line (270°), should snap onto it at the default 45° increment.
            AddStep("move near south line", () => input.MoveMouseTo(positionAtAngle(264)));
            AddStep("click", () => input.Click(MouseButton.Left));
            AddAssert("note placed at 270", () => placedObject<CardinalNote>()?.AngleDeg, () => Is.EqualTo(270));
        }

        [Test]
        public void TestPlacementInHitZoneRejectsNegativeTime()
        {
            waitForComposer();
            AddStep("select note tool", () => input.Key(Key.Number2));

            // A point deep in the hit zone (just above the playfield bottom, well below the judgement
            // line). At editor time 0 this maps to a past — i.e. negative — time.
            Vector2 hitZonePos = Vector2.Zero;
            AddStep("target bottom of hit zone", () =>
            {
                var quad = playfield.ScreenSpaceDrawQuad;
                float x = quad.TopLeft.X + quad.Width * EditorAngleMapping.ToX(270);
                hitZonePos = new Vector2(x, quad.BottomLeft.Y - 4);
            });
            AddAssert("point maps to negative time", () => playfield.TimeAtScreenSpacePosition(hitZonePos) < 0);

            AddStep("move into hit zone", () => input.MoveMouseTo(hitZonePos));
            AddStep("click", () => input.Click(MouseButton.Left));

            // The placement must be rejected: no object may be committed before time zero.
            AddAssert("no object placed before time zero", () => editorChart.HitObjects.All(h => h.StartTime >= 0));
        }

        [Test]
        public void TestPlaceHoldNoteWithDrag()
        {
            waitForComposer();
            AddStep("select hold tool", () => input.Key(Key.Number3));
            AddStep("move to playfield", () => input.MoveMouseTo(positionAtAngle(270, 0.6f)));
            AddStep("press", () => input.PressButton(MouseButton.Left));
            // downward scrolling: dragging upward extends toward later times.
            AddStep("drag upward", () => input.MoveMouseTo(positionAtAngle(270, 0.3f)));
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));
            AddAssert("hold placed with duration", () => placedObject<CardinalHoldNote>()?.Duration > 0);
        }

        [Test]
        public void TestPlaceShoulderNotePicksNearerSide()
        {
            waitForComposer();
            AddStep("select shoulder tool", () => input.Key(Key.Number4));
            // Left strip is the West–South boundary (225°); Right strip the East–North boundary (45°).
            AddStep("move near left strip", () => input.MoveMouseTo(positionAtAngle(225)));
            AddStep("click", () => input.Click(MouseButton.Left));
            AddAssert("left shoulder placed", () => placedObject<ShoulderNote>()?.Side == HorizontalDirection.Left);
        }

        [Test]
        public void TestPlaceShoulderHoldWithDrag()
        {
            waitForComposer();
            AddStep("select shoulder hold tool", () => input.Key(Key.Number5));
            AddStep("move near right strip", () => input.MoveMouseTo(positionAtAngle(45, 0.6f)));
            AddStep("press", () => input.PressButton(MouseButton.Left));
            AddStep("drag upward", () => input.MoveMouseTo(positionAtAngle(45, 0.3f)));
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));
            AddAssert("shoulder hold placed with duration", () => placedObject<ShoulderHoldNote>()?.Duration > 0);
            AddAssert("right side", () => placedObject<ShoulderHoldNote>()?.Side == HorizontalDirection.Right);
        }

        [Test]
        public void TestPlaceSlams()
        {
            waitForComposer();
            AddStep("select center slam tool", () => input.Key(Key.Number6));
            AddStep("move to playfield", () => input.MoveMouseTo(positionAtAngle(270)));
            AddStep("click", () => input.Click(MouseButton.Left));
            AddAssert("center slam placed at 270", () => placedObject<GarbusSlamCentered>()?.AngleDeg, () => Is.EqualTo(270));

            AddStep("select edge slam tool", () => input.Key(Key.Number7));
            AddStep("move to playfield", () => input.MoveMouseTo(positionAtAngle(315, 0.4f)));
            AddStep("click", () => input.Click(MouseButton.Left));
            AddAssert("edge slam placed at 315", () => placedObject<GarbusSlamEdge>()?.AngleDeg, () => Is.EqualTo(315));
        }

        [Test]
        public void TestPlaceSliderMultiClick()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(Key.Number8));
            AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.7f)));
            AddStep("click body", () => input.Click(MouseButton.Left));
            AddStep("move to first node", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
            AddStep("click node 1", () => input.Click(MouseButton.Left));
            AddStep("move to second node", () => input.MoveMouseTo(positionAtAngle(270, 0.3f)));
            AddStep("click node 2", () => input.Click(MouseButton.Left));
            AddStep("right click to commit", () => input.Click(MouseButton.Right));

            AddAssert("slider placed", () => placedObject<SliderBody>() != null);
            AddAssert("slider at 270", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
            AddAssert("at least one node", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.GreaterThanOrEqualTo(1));
            AddAssert("node times ascend", () =>
            {
                var cps = placedObject<SliderBody>()!.Path.ControlPoints;
                return cps.Count < 2 || (cps[0].TimeOffset > 0 && cps[1].TimeOffset > cps[0].TimeOffset);
            });
        }

        [Test]
        public void TestSliderNodeAtEarlierTimeRejected()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(Key.Number8));
            // body at a LATER time (low on screen, downward scrolling = later).
            AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.4f)));
            AddStep("click body", () => input.Click(MouseButton.Left));
            // a node EARLIER than the body (higher on screen) must be rejected (time must advance).
            AddStep("move to earlier node", () => input.MoveMouseTo(positionAtAngle(315, 0.7f)));
            AddStep("click earlier node", () => input.Click(MouseButton.Left));
            AddStep("right click to commit (no nodes → discarded)", () => input.Click(MouseButton.Right));

            // With no valid node, the slider is not committed (IsValidForPlacement requires ≥1 control point).
            AddAssert("no slider placed", () => placedObject<SliderBody>() == null);
        }

        // ------------------------------------------------------------------
        // placed-object query helpers
        // ------------------------------------------------------------------

        private T? placedObject<T>() where T : GarbusHitObject => editorChart.HitObjects.OfType<T>().FirstOrDefault();

        // ------------------------------------------------------------------
        // Harness: caches the DI deps the composer tree requires, then hosts
        // the real GarbusHitObjectComposer as its child.
        // ------------------------------------------------------------------

        private partial class ComposePlacementHarness : Container
        {
            private readonly EditorChart editorChart;
            private DependencyContainer dependencies = null!;

            public GarbusHitObjectComposer Composer { get; private set; } = null!;
            public ManualInputManager Input { get; private set; } = null!;

            public ComposePlacementHarness(EditorChart editorChart)
            {
                this.editorChart = editorChart;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                var beatDivisor = new BindableBeatDivisor(4);
                var editorClock = new EditorClock(editorChart.ControlPointInfo, 60000, beatDivisor);
                editorClock.ChangeSource(new TrackVirtual(60000));

                dependencies.Cache(editorChart);
                dependencies.Cache(editorClock);
                dependencies.Cache(beatDivisor);
                // IEditorChangeHandler is [Resolved(CanBeNull = true)] on the placement blueprint — omit it.

                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = Input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    // Wire the composer subtree to the EditorClock (as ComposeTab does in production) so
                    // the playfield's time→position mapping uses editor time, not the ambient wall
                    // clock. Without this the playfield runs on session time and the hit zone never maps
                    // to the negative times this scene needs to exercise.
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Clock = dependencies.Get<EditorClock>(),
                        Child = Composer = new GarbusHitObjectComposer { RelativeSizeAxes = Axes.Both },
                    },
                };
                // EditorClock must be in the hierarchy to tick; add it alongside the composer.
                AddInternal(dependencies.Get<EditorClock>());
            }
        }
    }
}
