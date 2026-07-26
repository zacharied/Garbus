using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using Garbus.Game.Core;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints;

/// <summary>
/// Base placement blueprint: every mouse move runs the composer's angle+time snap and, while still
/// waiting for the first click, writes the snapped (wrap-normalised) angle and time onto the pending
/// hit object.
/// </summary>
internal abstract partial class GarbusPlacementBlueprint<T> : HitObjectPlacementBlueprint
    where T : GarbusHitObject, IHasMutableAngle
{
    protected new T HitObject => (T)base.HitObject;

    [Resolved]
    protected GarbusHitObjectComposer? Composer { get; private set; }

    protected GarbusPlacementBlueprint(T hitObject)
        : base(hitObject)
    {
        RelativeSizeAxes = Axes.Both;
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        BeginPlacement(true);
        return true;
    }

    public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
    {
        var result = Composer?.FindSnappedAngleTimeAndPosition(screenSpacePosition) ?? new SnapResult(screenSpacePosition, fallbackTime);

        base.UpdateTimeAndPosition(result.ScreenSpacePosition, result.Time ?? fallbackTime);

        if (PlacementActive == PlacementState.Waiting && result is GarbusSnapResult garbus)
            HitObject.AngleDeg = garbus.AngleDeg;

        // Alt during placement picks the Right side; locked in once the first click moves
        // PlacementActive past Waiting, so it can't be nudged mid-drag by releasing/re-pressing Alt.
        if (PlacementActive == PlacementState.Waiting && HitObject is IHasSide hasSide)
        {
            hasSide.Side = GetContainingInputManager()?.CurrentState.Keyboard.AltPressed == true
                ? HorizontalDirection.Right
                : HorizontalDirection.Left;
        }

        // Shift during placement picks Anticlockwise for slam edges, mirroring the Alt/Side handling above.
        if (PlacementActive == PlacementState.Waiting && HitObject is GarbusSlamEdge slamEdge)
        {
            slamEdge.Direction = GetContainingInputManager()?.CurrentState.Keyboard.ShiftPressed == true
                ? RotationalDirection.Anticlockwise
                : RotationalDirection.Clockwise;
        }

        return result;
    }

    // Only replace an existing object occupying the same spot AND of the same type — objects of
    // different types are designed to stack (see ObjectStacking.md), so placing e.g. a slam on a
    // slider head must not delete the slider. (Mania scopes replacement to the column; our spatial
    // equivalent is the angle, plus the same-type constraint.)
    public override bool ReplacesExistingObject(GarbusHitObject existing) =>
        base.ReplacesExistingObject(existing)
        && existing.GetType() == HitObject.GetType()
        && existing is IHasAngle angled
        && EditorAngleMapping.NormalizeDeg(angled.AngleDeg) == EditorAngleMapping.NormalizeDeg(HitObject.AngleDeg);
}
