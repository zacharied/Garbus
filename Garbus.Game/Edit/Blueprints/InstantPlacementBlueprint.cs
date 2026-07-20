// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/InstantPlacementBlueprint.cs).
// BacHitObject → GarbusHitObject; EditorDrawableCardinalNote from Garbus.Game.Edit.Drawables;
// SnapResult from Garbus.Game.Edit.Compose. Otherwise verbatim.

using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints;

/// <summary>
/// Placement for single-press objects (cardinal notes, slams): an outline follows the snapped cursor
/// and a left click places immediately.
/// </summary>
internal abstract partial class InstantPlacementBlueprint<T> : GarbusPlacementBlueprint<T>
    where T : GarbusHitObject, IHasMutableAngle
{
    private readonly PlacementSpritePreview sprite;
    private readonly EditSquarePiece piece;

    protected InstantPlacementBlueprint(T hitObject)
        : base(hitObject)
    {
        InternalChildren = new Drawable[]
        {
            sprite = new PlacementSpritePreview(hitObject),
            piece = new EditSquarePiece
            {
                Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE),
                Origin = Anchor.Centre,
            },
        };
    }

    public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
    {
        var result = base.UpdateTimeAndPosition(screenSpacePosition, fallbackTime);
        sprite.Position = piece.Position = ToLocalSpace(result.ScreenSpacePosition);
        return result;
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        base.OnMouseDown(e);
        EndPlacement(true);
        return true;
    }
}
