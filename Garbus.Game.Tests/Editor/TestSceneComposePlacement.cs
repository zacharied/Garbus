// Placement tests for GarbusHitObjectComposer + tools + placement blueprints.
// Hosts the full composer in a DI harness (the pattern from TestSceneComposerLifecycle /
// TestSceneEditorPlayfield) and drives it with a nested ManualInputManager, using
// positionAtAngle/screenPositionOf helpers.
//
// Auto-seek gotcha: HitObjectPlacementBlueprint.EndPlacement seeks the clock to the placed object,
// which scrolls it toward the judgement line — wait for the seek before asserting screen positions.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Blueprints;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Drawables;
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
        private const Key slider_tool_key = Key.Number4;
        private const Key shoulder_tool_key = Key.Number5;
        private const Key shoulder_hold_tool_key = Key.Number6;
        private const Key slam_centered_tool_key = Key.Number7;
        private const Key slam_edge_tool_key = Key.Number8;

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

        private void waitForComposer()
        {
            AddUntilStep("wait for composer", () => harness.Composer?.IsLoaded == true);
            AddStep("park cursor on playfield", () => input.MoveMouseTo(positionAtAngle(270)));
        }

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
        public void TestPlacementPreviewsUseEditorSprites()
        {
            waitForComposer();

            Key[] spritePreviewTools =
            {
                Key.Number2,
                Key.Number3,
                shoulder_tool_key,
                shoulder_hold_tool_key,
                slam_centered_tool_key,
                slam_edge_tool_key,
            };

            foreach (Key toolKey in spritePreviewTools)
            {
                Key selectedToolKey = toolKey;
                AddStep($"select placement tool {selectedToolKey}", () => input.Key(selectedToolKey));
                AddStep($"move over playfield for tool {selectedToolKey}", () => input.MoveMouseTo(positionAtAngle(270)));
                AddUntilStep($"tool {selectedToolKey} has sprite preview", () => placementSprite() != null);
            }
        }

        [Test]
        public void TestSlamPlacementPreviewReflectsSideAndDirection()
        {
            waitForComposer();
            AddStep("select edge slam tool", () => input.Key(slam_edge_tool_key));
            AddStep("hold alt and shift", () =>
            {
                input.PressKey(Key.LAlt);
                input.PressKey(Key.LShift);
            });
            AddStep("move over playfield", () => input.MoveMouseTo(positionAtAngle(270)));
            AddUntilStep("slam sprite preview loaded", () => placementSprite() != null);
            AddAssert("preview uses right-side colour", () => placementSprite()!.Colour.Equals(Constants.RightColour));
            AddAssert("preview points anticlockwise", () => placementSprite()!.Rotation, () => Is.EqualTo(90).Within(0.1f));

            AddStep("release modifiers", () =>
            {
                input.ReleaseKey(Key.LShift);
                input.ReleaseKey(Key.LAlt);
            });
            AddAssert("preview returns to left-side colour", () => placementSprite()!.Colour.Equals(Constants.LeftColour));
            AddAssert("preview points clockwise", () => placementSprite()!.Rotation, () => Is.EqualTo(-90).Within(0.1f));
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
            AddStep("select shoulder tool", () => input.Key(shoulder_tool_key));
            // Left strip is the West lane (180°); Right strip the East lane (0°).
            AddStep("move near left strip", () => input.MoveMouseTo(positionAtAngle(180)));
            AddStep("click", () => input.Click(MouseButton.Left));
            AddAssert("left shoulder placed", () => placedObject<ShoulderNote>()?.Side == HorizontalDirection.Left);
        }

        [Test]
        public void TestShoulderStripsSitOnWestEastLanes()
        {
            waitForComposer();
            AddStep("select shoulder tool", () => input.Key(shoulder_tool_key));
            AddStep("move near left strip", () => input.MoveMouseTo(positionAtAngle(180)));
            AddStep("click", () => input.Click(MouseButton.Left));
            AddAssert("left shoulder placed", () => placedObject<ShoulderNote>()?.Side == HorizontalDirection.Left);
            AddAssert("left shoulder drawn on West lane", () =>
                System.Math.Abs(shoulderDrawableXFraction(HorizontalDirection.Left) - EditorAngleMapping.ToX(180)) < 0.005f);

            AddStep("select shoulder tool", () => input.Key(shoulder_tool_key));
            AddStep("move near right strip", () => input.MoveMouseTo(positionAtAngle(0)));
            AddStep("click", () => input.Click(MouseButton.Left));
            AddAssert("right shoulder placed", () => placedObject<ShoulderNote>(1)?.Side == HorizontalDirection.Right);
            AddAssert("right shoulder drawn on East lane", () =>
                System.Math.Abs(shoulderDrawableXFraction(HorizontalDirection.Right) - EditorAngleMapping.ToX(0)) < 0.005f);
        }

        [Test]
        public void TestShoulderStripsInvariantUnderReverseAngleView()
        {
            waitForComposer();
            AddStep("select shoulder tool", () => input.Key(shoulder_tool_key));
            AddStep("move near left strip", () => input.MoveMouseTo(positionAtAngle(180)));
            AddStep("click", () => input.Click(MouseButton.Left));
            AddAssert("left shoulder placed", () => placedObject<ShoulderNote>()?.Side == HorizontalDirection.Left);

            float xBeforeReverse = 0;
            AddStep("record left strip X", () => xBeforeReverse = shoulderDrawableXFraction(HorizontalDirection.Left));

            AddStep("reverse angle view", () => composer.ReverseAngleView.Value = true);

            // West(180)/East(0) sit on the E–W reflection axis — the shoulder strips must not move when
            // the view direction flips, unlike the rest of the grid.
            AddAssert("left strip X unchanged under reverse", () =>
                System.Math.Abs(shoulderDrawableXFraction(HorizontalDirection.Left) - xBeforeReverse) < 0.005f);
            AddAssert("left strip still on West lane", () =>
                System.Math.Abs(shoulderDrawableXFraction(HorizontalDirection.Left) - EditorAngleMapping.ToX(180)) < 0.005f);
        }

        [Test]
        public void TestPlaceShoulderHoldWithDrag()
        {
            waitForComposer();
            AddStep("select shoulder hold tool", () => input.Key(shoulder_hold_tool_key));
            AddStep("move near right strip", () => input.MoveMouseTo(positionAtAngle(0, 0.6f)));
            AddStep("press", () => input.PressButton(MouseButton.Left));
            AddStep("drag upward", () => input.MoveMouseTo(positionAtAngle(0, 0.3f)));
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));
            AddAssert("shoulder hold placed with duration", () => placedObject<ShoulderHoldNote>()?.Duration > 0);
            AddAssert("right side", () => placedObject<ShoulderHoldNote>()?.Side == HorizontalDirection.Right);
        }

        [Test]
        public void TestPlaceSlams()
        {
            waitForComposer();
            AddStep("select center slam tool", () => input.Key(slam_centered_tool_key));
            AddStep("move to playfield", () => input.MoveMouseTo(positionAtAngle(270)));
            AddStep("click", () => input.Click(MouseButton.Left));
            AddAssert("center slam placed at 270", () => placedObject<GarbusSlamCentered>()?.AngleDeg, () => Is.EqualTo(270));

            AddStep("select edge slam tool", () => input.Key(slam_edge_tool_key));
            AddStep("move to playfield", () => input.MoveMouseTo(positionAtAngle(315, 0.4f)));
            AddStep("click", () => input.Click(MouseButton.Left));
            AddAssert("edge slam placed at 315", () => placedObject<GarbusSlamEdge>()?.AngleDeg, () => Is.EqualTo(315));
        }

        [Test]
        public void TestSlamOnSliderHeadKeepsSlider()
        {
            waitForComposer();
            // Keep the clock parked so both placements snap to the same time and overlap deterministically.
            AddStep("disable auto-seek", () => composer.AutoSeekOnPlacement.Value = false);

            AddStep("select slider tool", () => input.Key(slider_tool_key));
            AddStep("move to head", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("ctrl+left-click (head-only slider)", () =>
            {
                input.PressKey(Key.LControl);
                input.Click(MouseButton.Left);
                input.ReleaseKey(Key.LControl);
            });
            AddAssert("slider placed", () => placedObject<SliderBody>() != null);

            // A slam dropped on the slider's head shares its angle+time — different type, so it must stack,
            // not delete the slider.
            AddStep("select center slam tool", () => input.Key(slam_centered_tool_key));
            AddStep("move to same spot", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("click", () => input.Click(MouseButton.Left));

            AddAssert("slam placed at 270", () => placedObject<GarbusSlamCentered>()?.AngleDeg, () => Is.EqualTo(270));
            AddAssert("slider still present", () => placedObject<SliderBody>() != null);
        }

        [Test]
        public void TestSlamOnSlamStillReplaces()
        {
            waitForComposer();
            AddStep("disable auto-seek", () => composer.AutoSeekOnPlacement.Value = false);

            AddStep("select center slam tool", () => input.Key(slam_centered_tool_key));
            AddStep("move to spot", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("place first slam", () => input.Click(MouseButton.Left));
            AddAssert("one slam", () => editorChart.HitObjects.OfType<GarbusSlamCentered>().Count(), () => Is.EqualTo(1));

            // Same type, same angle+time → the placement replaces the existing slam rather than doubling up.
            AddStep("place second slam on top", () => input.Click(MouseButton.Left));
            AddAssert("still one slam (replaced)", () => editorChart.HitObjects.OfType<GarbusSlamCentered>().Count(), () => Is.EqualTo(1));
        }

        [Test]
        public void TestSliderOnSliderHeadKeepsBoth()
        {
            waitForComposer();
            AddStep("disable auto-seek", () => composer.AutoSeekOnPlacement.Value = false);

            AddStep("select slider tool", () => input.Key(slider_tool_key));
            AddStep("move to head", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("ctrl+left-click (head-only slider)", () =>
            {
                input.PressKey(Key.LControl);
                input.Click(MouseButton.Left);
                input.ReleaseKey(Key.LControl);
            });
            AddAssert("one slider", () => editorChart.HitObjects.OfType<SliderBody>().Count(), () => Is.EqualTo(1));

            // Sliders never replace each other — a second slider sharing the first's head angle+time stacks.
            AddStep("ctrl+left-click same spot again", () =>
            {
                input.PressKey(Key.LControl);
                input.Click(MouseButton.Left);
                input.ReleaseKey(Key.LControl);
            });
            AddAssert("both sliders kept", () => editorChart.HitObjects.OfType<SliderBody>().Count(), () => Is.EqualTo(2));
        }

        [Test]
        public void TestPlaceSliderMultiClick()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(slider_tool_key));
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
        public void TestSliderWaitingPreviewReflectsGestureAndSide()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(slider_tool_key));
            AddStep("move over playfield", () => input.MoveMouseTo(positionAtAngle(270)));
            AddUntilStep("slider preview loaded", () => sliderWaitingPreview()?.IsLoaded == true);
            AddAssert("normal gesture shows fading line", () =>
                sliderWaitingPreview() is { FadingLine.Alpha: 1, HeadOnlyDot.Alpha: 0 } preview
                && !preview.FadingLine.Colour.HasSingleColour
                && preview.FadingLine.DrawHeight > 0
                && preview.FadingLine.Colour.TopLeft.Alpha == 0
                && preview.FadingLine.Colour.BottomLeft.Alpha == 1);
            AddAssert("starts with left colour", () => sliderWaitingPreview()!.PreviewColour.Equals(Constants.LeftColour));
            AddAssert("line draws with left colour", () =>
                sliderWaitingPreview()!.FadingLine.DrawColourInfo.Colour.BottomLeft.SRGB.Equals((Colour4)Constants.LeftColour));
            AddAssert("yellow cursor box remains visible", () =>
                sliderPlacement()!.ChildrenOfType<EditSquarePiece>().Single().Alpha == 1);

            AddStep("hold alt", () => input.PressKey(Key.LAlt));
            AddAssert("line changes to right colour", () => sliderWaitingPreview()!.PreviewColour.Equals(Constants.RightColour));

            AddStep("hold ctrl", () => input.PressKey(Key.LControl));
            AddAssert("head-only gesture shows dot", () =>
                sliderWaitingPreview() is { FadingLine.Alpha: 0, HeadOnlyDot.Alpha: 1 });
            AddAssert("dot keeps right colour", () => sliderWaitingPreview()!.PreviewColour.Equals(Constants.RightColour));
            AddAssert("dot draws with right colour", () =>
                sliderWaitingPreview()!.HeadOnlyDot.DrawColourInfo.Colour.TopLeft.SRGB.Equals((Colour4)Constants.RightColour));

            AddStep("release alt", () => input.ReleaseKey(Key.LAlt));
            AddAssert("dot returns to left colour", () =>
                sliderWaitingPreview()!.HeadOnlyDot.DrawColourInfo.Colour.TopLeft.SRGB.Equals((Colour4)Constants.LeftColour));
            AddAssert("yellow box remains visible with ctrl", () =>
                sliderPlacement()!.ChildrenOfType<EditSquarePiece>().Single().Alpha == 1);
            AddStep("release ctrl", () => input.ReleaseKey(Key.LControl));
        }

        [Test]
        public void TestSliderNodeAtEarlierTimeRejected()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(slider_tool_key));
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

        [Test]
        public void TestPlaceSliderWithHorizontalSegment()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(slider_tool_key));
            AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.7f)));
            AddStep("click body", () => input.Click(MouseButton.Left));
            AddStep("move to node 1", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
            AddStep("click node 1", () => input.Click(MouseButton.Left));
            // node 2 at the SAME time as node 1 (same yFrac) but a different angle — a horizontal arc.
            AddStep("move to node 2 (same time, new angle)", () => input.MoveMouseTo(positionAtAngle(0, 0.5f)));
            AddStep("click node 2", () => input.Click(MouseButton.Left));
            AddStep("right click to commit", () => input.Click(MouseButton.Right));

            AddAssert("slider placed", () => placedObject<SliderBody>() != null);
            AddAssert("two control points", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(2));
            AddAssert("last two nodes share a time (horizontal arc)", () =>
            {
                var cps = placedObject<SliderBody>()!.Path.ControlPoints;
                return cps[0].TimeOffset == cps[1].TimeOffset && cps[0].TimeOffset > 0;
            });
        }

        [Test]
        public void TestPlaceSliderRejectsThreeNodesAtSameTime()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(slider_tool_key));
            AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.7f)));
            AddStep("click body", () => input.Click(MouseButton.Left));
            AddStep("move to node 1", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
            AddStep("click node 1", () => input.Click(MouseButton.Left));
            AddStep("move to node 2 (same time)", () => input.MoveMouseTo(positionAtAngle(0, 0.5f)));
            AddStep("click node 2", () => input.Click(MouseButton.Left));
            // a THIRD node at the same time must be rejected (two zero-length links in a row).
            AddStep("move to node 3 (same time)", () => input.MoveMouseTo(positionAtAngle(45, 0.5f)));
            AddStep("click node 3 (rejected)", () => input.Click(MouseButton.Left));
            AddStep("right click to commit", () => input.Click(MouseButton.Right));

            AddAssert("only two control points (third rejected)",
                () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(2));
        }

        [Test]
        public void TestPlaceSliderWithLeadingArcCommits()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(slider_tool_key));
            AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("click body", () => input.Click(MouseButton.Left));
            // node 1 at the head's time (offset 0) — a leading horizontal arc.
            AddStep("move to node 1 at head time", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
            AddStep("click node 1", () => input.Click(MouseButton.Left));
            // node 2 strictly later, so the path has a real duration and can commit.
            AddStep("move to node 2 (later)", () => input.MoveMouseTo(positionAtAngle(0, 0.3f)));
            AddStep("click node 2", () => input.Click(MouseButton.Left));
            AddStep("right click to commit", () => input.Click(MouseButton.Right));

            AddAssert("slider placed", () => placedObject<SliderBody>() != null);
            AddAssert("first node at head time (leading arc)",
                () => placedObject<SliderBody>()!.Path.ControlPoints[0].TimeOffset, () => Is.EqualTo(0.0));
            AddAssert("second node later",
                () => placedObject<SliderBody>()!.Path.ControlPoints[1].TimeOffset, () => Is.GreaterThan(0.0));
        }

        [Test]
        public void TestPlaceSliderZeroDurationCommits()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(slider_tool_key));
            AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("click body", () => input.Click(MouseButton.Left));
            // the only node sits at the head's time (offset 0): a zero-duration constant-radius arc.
            AddStep("move to node at head time", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
            AddStep("click node", () => input.Click(MouseButton.Left));
            AddStep("right click to commit", () => input.Click(MouseButton.Right));

            AddAssert("slider placed", () => placedObject<SliderBody>() != null);
            AddAssert("one control point at head time",
                () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));
            AddAssert("zero duration", () => placedObject<SliderBody>()!.Duration, () => Is.EqualTo(0.0));
        }

        [Test]
        public void TestCtrlClickPlacesHeadOnlySlider()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(slider_tool_key));
            AddStep("move to head", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("ctrl+left-click", () =>
            {
                input.PressKey(Key.LControl);
                input.Click(MouseButton.Left);
                input.ReleaseKey(Key.LControl);
            });

            AddAssert("slider placed", () => placedObject<SliderBody>() != null);
            AddAssert("zero control points",
                () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(0));
            AddAssert("zero duration", () => placedObject<SliderBody>()!.Duration, () => Is.EqualTo(0.0));
            AddAssert("head at 270", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
        }

        [Test]
        public void TestPlainRightClickDoesNotPlaceHeadOnly()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(slider_tool_key));
            AddStep("move to head", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("plain left-click (start)", () => input.Click(MouseButton.Left));
            AddStep("right-click with no nodes", () => input.Click(MouseButton.Right));

            AddAssert("no slider placed", () => placedObject<SliderBody>() == null);
        }

        [Test]
        public void TestToolSwitchDoesNotCommitHeadOnly()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(slider_tool_key));
            AddStep("move to head", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("plain left-click (start)", () => input.Click(MouseButton.Left));
            AddStep("switch to select tool (auto-commit path)", () => input.Key(Key.Number1));

            AddAssert("no slider placed", () => placedObject<SliderBody>() == null);
        }

        // ------------------------------------------------------------------
        // placed-object query helpers
        // ------------------------------------------------------------------

        private T? placedObject<T>() where T : GarbusHitObject => editorChart.HitObjects.OfType<T>().FirstOrDefault();

        private T? placedObject<T>(int index) where T : GarbusHitObject => editorChart.HitObjects.OfType<T>().ElementAtOrDefault(index);

        private EditorSpritePiece? placementSprite() =>
            composer.BlueprintContainer.CurrentPlacement?.ChildrenOfType<EditorSpritePiece>().FirstOrDefault();

        private SliderPlacementPreview? sliderWaitingPreview() =>
            sliderPlacement()?.ChildrenOfType<SliderPlacementPreview>().FirstOrDefault();

        private SliderPlacementBlueprint? sliderPlacement() =>
            composer.BlueprintContainer.CurrentPlacement as SliderPlacementBlueprint;

        /// <summary>The rendered x-fraction (<see cref="Drawable.X"/>, relative-positioned) of the placed
        /// shoulder note's editor drawable for the given side.</summary>
        private float shoulderDrawableXFraction(HorizontalDirection side) =>
            playfield.ChildrenOfType<EditorDrawableShoulderNote>().First(d => d.HitObject.Side == side).X;

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
