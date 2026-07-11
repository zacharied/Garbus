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
using Garbus.Game.Core;
using Garbus.Game.Edit.Blueprints;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Objects;
using osuTK;

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

    protected override IEnumerable<MenuItem> GetContextMenuItemsForSelection(IEnumerable<SelectionBlueprint<GarbusHitObject>> selection)
    {
        yield return new GarbusMenuItem("Flip around angle...", MenuItemType.Standard,
            () => composer.BeginFlipAroundAngle(Flip));
        yield return new GarbusMenuItem("Flip selection", MenuItemType.Standard,
            () => Flip(ComputeSelectionReflectionSum()));

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

    /// <summary>
    /// Reflects every selected handle about the pivot encoded by <paramref name="sumDeg"/> (= 2·φ):
    /// <c>θ → NormalizeDeg(sumDeg − θ)</c>. A true mirror — slider handedness reverses. A slider with
    /// selected nodes reflects only those nodes (head anchored); a whole-selected slider mirrors rigidly.
    /// One change transaction ⇒ a single undo step.
    /// </summary>
    private void Flip(int sumDeg)
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
                    // No mutable angle: reflect its derived E/W angle, re-derive Side by hemisphere.
                    int a = EditorAngleMapping.NormalizeDeg(sumDeg - shoulder.Side.ToAngleDeg());
                    shoulder.Side = inEastHemisphere(a) ? HorizontalDirection.Right : HorizontalDirection.Left;
                    break;

                case SliderBody slider when blueprint is SliderSelectionBlueprint sb && sb.SelectedNodes.Count > 0:
                    // Node subset: head fixed, reflect each selected node's absolute angle, store minimal offset.
                    foreach (var cp in sb.SelectedNodes)
                    {
                        int abs = EditorAngleMapping.NormalizeDeg(slider.AngleDeg + cp.RotationOffset);
                        int newAbs = EditorAngleMapping.NormalizeDeg(sumDeg - abs);
                        cp.RotationOffset = EditorAngleMapping.MinimalDiff(slider.AngleDeg, newAbs);
                    }
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
