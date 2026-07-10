// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/BacSelectionBlueprint.cs).
// BacSelectionBlueprint → GarbusSelectionBlueprint; BacEditorPlayfield → GarbusEditorPlayfield;
// HitObjectSelectionBlueprint<T> / EditSquarePiece from the Garbus Edit.Compose / Edit.Blueprints.Components;
// ScrollingHitObjectContainer from Garbus.Game.Gameplay.UI.Scrolling; Playfield from Gameplay.UI.
//
// NOTE on the stale-DrawableObject gap: this base positions itself directly off the HitObjectContainer
// (angle → x, time → y) every frame. Positional input still routes through DrawableObject (the base
// accessors: ReceivePositionalInputAt / ScreenSpaceSelectionPoint / SelectionQuad all read it) — that
// stays correct because the composer's HitObjectUpdated → TransferBlueprintFor refresh re-points
// DrawableObject at the freshly-created drawable within the same event dispatch, and the new drawable
// is laid out in the same frame's update pass (see EditorBlueprintContainer).

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Objects;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Gameplay.UI.Scrolling;
using osuTK;

namespace Garbus.Game.Edit.Blueprints;

/// <summary>
/// Base selection blueprint: positions itself each frame at the object's (angle → x, time → y) point on
/// the editor timeline, mirroring how the editor drawables themselves are laid out (origin at the
/// bottom-centre, since the timeline scrolls downward).
///
/// When the object is within reach of a ghost band, a twin outline is drawn at the wrapped position and
/// positional input there is accepted too (hit-testing is done by translating the query point back onto
/// the main copy), which is what makes the band clones selectable and draggable.
/// </summary>
internal abstract partial class GarbusSelectionBlueprint<T> : HitObjectSelectionBlueprint<T>
    where T : GarbusHitObject, IHasAngle
{
    [Resolved]
    private Playfield playfield { get; set; } = null!;

    protected ScrollingHitObjectContainer HitObjectContainer => ((GarbusEditorPlayfield)playfield).HitObjectContainer;

    private Drawable? twin;

    protected GarbusSelectionBlueprint(T hitObject)
        : base(hitObject)
    {
        RelativeSizeAxes = Axes.None;
        // Matches the drawables: single-press outlines straddle their time line; duration blueprints
        // override to BottomCentre so their height spans start → end exactly.
        Origin = Anchor.Centre;
    }

    protected override void Update()
    {
        base.Update();

        var container = HitObjectContainer;

        var screen = container.ScreenSpacePositionAtTime(HitObject.StartTime);
        float localX = ComputeXFraction() * container.DrawWidth;
        screen.X = container.ToScreenSpace(new Vector2(localX, 0)).X;

        Position = Parent!.ToLocalSpace(screen) - AnchorPosition;

        updateTwin();
    }

    private void updateTwin()
    {
        if (TwinXFraction() is float twinX)
        {
            if (twin == null)
                AddInternal(twin = CreateTwinVisual());

            // blueprint space shares the playfield's scale, so the offset in container-local pixels applies directly.
            twin.X = (twinX - ComputeXFraction()) * HitObjectContainer.DrawWidth;
            twin.Show();
        }
        else
            twin?.Hide();
    }

    /// <summary>The outline shown over the ghost twin. Sized to the blueprint by default.</summary>
    protected virtual Drawable CreateTwinVisual() => new EditSquarePiece { RelativeSizeAxes = Axes.Both };

    /// <summary>The x position (as a fraction of the full editor width). Defaults to the object's angle.</summary>
    protected virtual float ComputeXFraction() => EditorAngleMapping.ToX(HitObject.AngleDeg);

    /// <summary>Where the ghost twin sits (x-fraction of the full width), or null when no twin is visible.</summary>
    protected virtual float? TwinXFraction() => EditorAngleMapping.GhostTwinX(HitObject.AngleDeg);

    public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
    {
        if (base.ReceivePositionalInputAt(screenSpacePos))
            return true;

        // accept input on the ghost twin by translating the query back onto the main copy.
        if (TwinXFraction() is float twinX)
            return base.ReceivePositionalInputAt(screenSpacePos - twinScreenOffset(twinX));

        return false;
    }

    private Vector2 twinScreenOffset(float twinX)
    {
        var container = HitObjectContainer;
        float offsetLocal = (twinX - ComputeXFraction()) * container.DrawWidth;
        return container.ToScreenSpace(new Vector2(offsetLocal, 0)) - container.ToScreenSpace(Vector2.Zero);
    }
}
