// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/BacSelectionHandler.cs).
// BacSelectionHandler → GarbusSelectionHandler; base EditorSelectionHandler is Garbus's vendored
// Edit.Compose type; EditorBeatmap → EditorChart (resolved via base); BigAssCircleHitObjectComposer →
// GarbusHitObjectComposer; HitObject → GarbusHitObject; BacSlamEdge → GarbusSlamEdge;
// GetContextMenuItemsForSelection uses IEnumerable<SelectionBlueprint<GarbusHitObject>> matching osu's
// real virtual signature on the vendored base; OsuColour/OsuSpriteText/OsuFont replaced by plain
// SpriteText with the yellow/gray colours inlined (osu's OsuColour.YellowDark #E7CF43, Gray0 #000000).

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using Garbus.Game.Core;
using Garbus.Game.Edit.Blueprints;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit;

public partial class GarbusSelectionHandler : EditorSelectionHandler
{
    [Resolved]
    private GarbusHitObjectComposer composer { get; set; } = null!;

    private readonly Bindable<TernaryState> selectionAnticlockwiseState = new Bindable<TernaryState>();

    private readonly Bindable<TernaryState> selectionRightSideState = new Bindable<TernaryState>();

    // Replaces the framework SelectionBox for slider selections (see Update) — the AABB box spans a huge,
    // useless area since a slider can sweep well past 360°.
    private SliderCountChip countChip = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        // right-aligned so it sits just to the LEFT of the final node it anchors to.
        AddInternal(countChip = new SliderCountChip { Alpha = 0, Origin = Anchor.CentreRight });

        selectionAnticlockwiseState.ValueChanged += state =>
        {
            switch (state.NewValue)
            {
                case TernaryState.False:
                    setEdgeSlamDirection(RotationalDirection.Clockwise);
                    break;

                case TernaryState.True:
                    setEdgeSlamDirection(RotationalDirection.Anticlockwise);
                    break;
            }
        };

        selectionRightSideState.ValueChanged += state =>
        {
            switch (state.NewValue)
            {
                case TernaryState.False:
                    setSliderSide(HorizontalDirection.Left);
                    break;

                case TernaryState.True:
                    setSliderSide(HorizontalDirection.Right);
                    break;
            }
        };
    }

    private void setEdgeSlamDirection(RotationalDirection direction)
    {
        if (SelectedItems.OfType<GarbusSlamEdge>().All(s => s.Direction == direction))
            return;

        EditorChart.PerformOnSelection(h =>
        {
            if (h is GarbusSlamEdge slam)
                slam.Direction = direction;
        });
    }

    private void setSliderSide(HorizontalDirection side)
    {
        if (SelectedItems.OfType<SliderBody>().All(s => s.Side == side))
            return;

        EditorChart.PerformOnSelection(h =>
        {
            if (h is SliderBody slider)
                slider.Side = side;
        });
    }

    /// <summary>Flips the selected slider(s) to the opposite side, mirroring the "Right side" context-menu toggle.</summary>
    private void toggleSliderSide() =>
        setSliderSide(selectionRightSideState.Value == TernaryState.True ? HorizontalDirection.Left : HorizontalDirection.Right);

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Repeat || e.AltPressed || e.SuperPressed)
            return base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Q when e is { ControlPressed: false, ShiftPressed: false }:
                composer.ReverseAngleView.Value = !composer.ReverseAngleView.Value;
                return true;

            case Key.S when e is { ControlPressed: false, ShiftPressed: false } && SelectedItems.OfType<SliderBody>().Any():
                toggleSliderSide();
                return true;

            case Key.F when e is { ControlPressed: true, ShiftPressed: false } && SelectedItems.Count > 0:
                composer.BeginFlipAroundAngle(flipAroundAngle);
                return true;

            case Key.F when e is { ControlPressed: false, ShiftPressed: false } && SelectedItems.Count > 0:
                flipSelection();
                return true;
        }

        return base.OnKeyDown(e);
    }

    protected override IEnumerable<MenuItem> GetContextMenuItemsForSelection(IEnumerable<SelectionBlueprint<GarbusHitObject>> selection)
    {
        yield return new GarbusMenuItem("Flip around angle...", MenuItemType.Standard,
            () => composer.BeginFlipAroundAngle(flipAroundAngle));
        yield return new GarbusMenuItem("Flip selection", MenuItemType.Standard, flipSelection);

        if (selection.All(s => s.Item is GarbusSlamEdge))
        {
            yield return new TernaryStateToggleMenuItem("Anticlockwise")
            {
                State = { BindTarget = selectionAnticlockwiseState },
            };
        }

        if (selection.All(s => s.Item is SliderBody))
        {
            yield return new TernaryStateToggleMenuItem("Right side")
            {
                State = { BindTarget = selectionRightSideState },
            };
        }

        foreach (var item in base.GetContextMenuItemsForSelection(selection))
            yield return item;
    }

    protected override void UpdateTernaryStates()
    {
        base.UpdateTernaryStates();

        selectionAnticlockwiseState.Value = GetStateFromSelection(
            EditorChart.SelectedHitObjects.OfType<GarbusSlamEdge>(),
            s => s.Direction == RotationalDirection.Anticlockwise);

        selectionRightSideState.Value = GetStateFromSelection(
            EditorChart.SelectedHitObjects.OfType<SliderBody>(),
            s => s.Side == HorizontalDirection.Right);
    }

    public override bool HandleMovement(MoveSelectionEvent<GarbusHitObject> moveEvent)
    {
        var playfield = composer.Playfield;

        // Convert the (already snapped) horizontal screen delta to a whole-degree rotation. The axis
        // wraps, so unlike mania's column clamping every selected object can rotate freely.
        float localDeltaX = playfield.ToLocalSpace(moveEvent.Blueprint.ScreenSpaceSelectionPoint + moveEvent.ScreenSpaceDelta).X
                            - playfield.ToLocalSpace(moveEvent.Blueprint.ScreenSpaceSelectionPoint).X;
        int deltaDeg = (int)Math.Round(localDeltaX / playfield.DrawWidth * EditorAngleMapping.TOTAL_DEGREES);

        // The snapped target can sit a full wrap (±360°) from the object's primary copy — the cursor
        // hovering the ghost twin of an object on the far side of the seam. Reduce to the minimal
        // equivalent rotation so "already there" is 0 (no update fired at all) rather than a spurious
        // ±360 that mutates + rebuilds every selected object on every mouse-move event.
        deltaDeg = EditorAngleMapping.MinimalDiff(0, deltaDeg);

        if (deltaDeg != 0)
        {
            EditorChart.PerformOnSelection(h =>
            {
                if (h is IHasMutableAngle mutable)
                    mutable.AngleDeg = EditorAngleMapping.NormalizeDeg(mutable.AngleDeg + deltaDeg);
            });
        }

        // Return true regardless so a pure time move (no angle change) still applies.
        return true;
    }

    private enum FlipMode
    {
        /// <summary>Pivot is the selection's own centre — node subsets reflect about their offset bbox.</summary>
        SelectionCentre,

        /// <summary>Pivot is an externally chosen angle — node subsets reflect about that mirror axis.</summary>
        AroundPivot,
    }

    private void flipSelection() => flip(ComputeSelectionReflectionSum(), FlipMode.SelectionCentre, 0);

    private void flipAroundAngle(int pivotDeg) =>
        flip(EditorAngleMapping.NormalizeDeg(2 * pivotDeg), FlipMode.AroundPivot, pivotDeg);

    /// <summary>
    /// Reflects every selected handle. Point objects, ShoulderNote, and whole sliders reflect in absolute
    /// space about <paramref name="sumDeg"/> (= 2·pivot). A slider with selected nodes reflects only those
    /// nodes, in head-relative offset space, about a fixed offset-sum — an exact involution that preserves
    /// winding (see reflectSelectedNodes). One change transaction ⇒ a single undo step.
    /// </summary>
    private void flip(int sumDeg, FlipMode mode, int pivotDeg)
    {
        if (EditorChart.SelectedHitObjects.Count == 0)
            return;

        EditorChart.BeginChange();

        foreach (var blueprint in SelectedBlueprints)
        {
            var h = blueprint.Item;
            bool changed = true;

            switch (h)
            {
                case ShoulderNote shoulder:
                    // No mutable angle: reflect its derived E/W angle, re-derive Side by hemisphere. Note this
                    // is only an involution for axis-aligned pivots (0/90/180/270); an oblique pivot snaps the
                    // shoulder to the nearer hemisphere and is not reversible (shoulders live only on E/W).
                    int a = EditorAngleMapping.NormalizeDeg(sumDeg - shoulder.Side.ToAngleDeg());
                    shoulder.Side = inEastHemisphere(a) ? HorizontalDirection.Right : HorizontalDirection.Left;
                    break;

                case SliderBody slider when blueprint is SliderSelectionBlueprint sb && sb.SelectedNodes.Count > 0:
                    reflectSelectedNodes(slider, sb, mode, pivotDeg);
                    break;

                case SliderBody slider:
                    // Rigid whole-slider mirror: reflect the head, negate every offset (preserves winding).
                    slider.AngleDeg = EditorAngleMapping.NormalizeDeg(sumDeg - slider.AngleDeg);
                    foreach (var cp in slider.Path.ControlPoints)
                        cp.RotationOffset = -cp.RotationOffset;
                    break;

                case IHasMutableAngle mutable:
                    mutable.AngleDeg = EditorAngleMapping.NormalizeDeg(sumDeg - mutable.AngleDeg);
                    if (h is GarbusSlamEdge slam)
                        slam.Direction = slam.Direction == RotationalDirection.Clockwise
                            ? RotationalDirection.Anticlockwise
                            : RotationalDirection.Clockwise;
                    break;

                default:
                    changed = false;
                    break;
            }

            if (changed)
                EditorChart.Update(h);
        }

        EditorChart.EndChange();
    }

    /// <summary>
    /// Reflects a slider's selected nodes in head-relative offset space about a fixed sum <c>S</c>
    /// (<c>newOffset = S − offset</c>) — a winding-preserving involution. For a selection-centre flip
    /// <c>S</c> is the nodes' own offset bounding box (<c>min + max</c>); for a pivot flip <c>S</c> is twice
    /// the pivot reduced to the reflection axis relative to the head. The slider head is left fixed.
    /// (In a mixed selection the nodes reflect about their own local centre rather than the whole
    /// selection's centre — an accepted, minor divergence; node editing is inherently local.)
    /// </summary>
    private static void reflectSelectedNodes(SliderBody slider, SliderSelectionBlueprint sb, FlipMode mode, int pivotDeg)
    {
        int s;

        if (mode == FlipMode.SelectionCentre)
        {
            int minOff = int.MaxValue, maxOff = int.MinValue;
            foreach (var cp in sb.SelectedNodes)
            {
                minOff = Math.Min(minOff, cp.RotationOffset);
                maxOff = Math.Max(maxOff, cp.RotationOffset);
            }

            s = minOff + maxOff;
        }
        else
        {
            // Fold the pivot's head-relative angle onto the mirror axis: φ and φ+180 are the same line.
            int axisOff = EditorAngleMapping.MinimalDiff(slider.AngleDeg, pivotDeg);
            if (axisOff > 90)
                axisOff -= 180;
            else if (axisOff <= -90)
                axisOff += 180;

            s = 2 * axisOff;
        }

        foreach (var cp in sb.SelectedNodes)
            cp.RotationOffset = s - cp.RotationOffset;
    }

    /// <summary>The reflection sum whose pivot is the centre of the selection's handle-set angular bbox.</summary>
    private int ComputeSelectionReflectionSum() => EditorAngleMapping.ReflectionSum(handleAngles());

    /// <summary>
    /// The angular "handles" the flip acts on: one per point object; per selected node for a slider with a
    /// node selection; head + every node for a whole-selected slider.
    /// </summary>
    private IEnumerable<int> handleAngles()
    {
        foreach (var blueprint in SelectedBlueprints)
        {
            switch (blueprint.Item)
            {
                case SliderBody slider when blueprint is SliderSelectionBlueprint sb && sb.SelectedNodes.Count > 0:
                    foreach (var cp in sb.SelectedNodes)
                        yield return EditorAngleMapping.NormalizeDeg(slider.AngleDeg + cp.RotationOffset);
                    break;

                case SliderBody slider:
                    yield return EditorAngleMapping.NormalizeDeg(slider.AngleDeg);
                    foreach (var cp in slider.Path.ControlPoints)
                        yield return EditorAngleMapping.NormalizeDeg(slider.AngleDeg + cp.RotationOffset);
                    break;

                case IHasAngle angled:
                    yield return EditorAngleMapping.NormalizeDeg(angled.AngleDeg);
                    break;
            }
        }
    }

    /// <summary>East hemisphere (cos θ ≥ 0): the N/S ties (90°, 270°) resolve to East.</summary>
    private static bool inEastHemisphere(int angleDeg)
    {
        int a = EditorAngleMapping.NormalizeDeg(angleDeg);
        return a <= 90 || a >= 270;
    }

    protected override void Update()
    {
        base.Update();

        var sliders = SelectedBlueprints.OfType<SliderSelectionBlueprint>().ToList();

        // Only when the whole selection is slider(s): the framework SelectionBox's AABB is meaningless for
        // a path that can exceed 360°, so hide it and show just the count chip 20px left of the final node.
        if (sliders.Count > 0 && sliders.Count == SelectedBlueprints.Count)
        {
            SelectionBox.Alpha = 0;

            countChip.Text = SelectedItems.Count.ToString();
            countChip.Position = ToLocalSpace(sliders[0].FinalNodeScreenPosition) - new Vector2(20, 0);
            countChip.Alpha = 1;
        }
        else
        {
            // leave SelectionBox visibility to the base's own logic for non-slider selections.
            countChip.Alpha = 0;
        }
    }

    /// <summary>The small numbered chip shown at the top of a slider selection in place of the SelectionBox.</summary>
    public partial class SliderCountChip : CompositeDrawable
    {
        private readonly Box background;
        private readonly SpriteText text;

        public string Text
        {
            set => text.Text = value;
        }

        public SliderCountChip()
        {
            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                // osu's OsuColour.YellowDark (#E7CF43).
                background = new Box { RelativeSizeAxes = Axes.Both, Colour = new Colour4(231, 207, 67, 255) },
                text = new SpriteText
                {
                    Padding = new MarginPadding(2),
                    Font = FontUsage.Default.With(size: 11),
                    Colour = Colour4.Black,
                },
            };
        }
    }
}
