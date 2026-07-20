// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/ShoulderNotePlacementBlueprint.cs).
// BigAssCircleHitObjectComposer → GarbusHitObjectComposer; BacEditorPlayfield → GarbusEditorPlayfield;
// HorizontalDirection from Garbus.Game.Core; HitObjectPlacementBlueprint / SnapResult from Edit.Compose;
// EditorDrawableCardinalNote from Edit.Drawables. ReplacesExistingObject overrides the vendored base's
// GarbusHitObject overload. Logic identical.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using Garbus.Game.Core;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints;

/// <summary>
/// Shoulder note placement: x snaps to the nearer of the two shoulder lane strips (which picks the
/// note's <see cref="ShoulderNote.Side"/>); time from the usual beat snap. Instant place on click.
/// </summary>
internal partial class ShoulderNotePlacementBlueprint : HitObjectPlacementBlueprint
{
    protected new ShoulderNote HitObject => (ShoulderNote)base.HitObject;

    [Resolved]
    private GarbusHitObjectComposer? composer { get; set; }

    private readonly PlacementSpritePreview sprite;
    private readonly EditSquarePiece piece;

    public ShoulderNotePlacementBlueprint()
        : base(new ShoulderNote { Side = HorizontalDirection.Left })
    {
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            sprite = new PlacementSpritePreview(HitObject),
            piece = new EditSquarePiece
            {
                Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE),
                Origin = Anchor.Centre,
                Colour = Color4.MediumPurple,
            },
        };
    }

    public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
    {
        var result = composer?.FindSnappedAngleTimeAndPosition(screenSpacePosition) ?? new SnapResult(screenSpacePosition, fallbackTime);

        base.UpdateTimeAndPosition(result.ScreenSpacePosition, result.Time ?? fallbackTime);

        if (composer != null)
        {
            var playfield = composer.Playfield;

            if (PlacementActive == PlacementState.Waiting)
            {
                // pick the side whose lane strip is angularly closer to the cursor.
                float cursorAngle = EditorAngleMapping.ToAngle(playfield.ToLocalSpace(screenSpacePosition).X / playfield.DrawWidth);
                HitObject.Side = wrapDistance(cursorAngle, GarbusEditorPlayfield.LEFT_SHOULDER_ANGLE_DEG) <= wrapDistance(cursorAngle, GarbusEditorPlayfield.RIGHT_SHOULDER_ANGLE_DEG)
                    ? HorizontalDirection.Left
                    : HorizontalDirection.Right;
            }

            sprite.Position = piece.Position = new Vector2(
                GarbusEditorPlayfield.ShoulderXFraction(HitObject.Side) * DrawWidth,
                ToLocalSpace(result.ScreenSpacePosition).Y);
        }

        return result;
    }

    private static float wrapDistance(float a, float b)
    {
        float d = Math.Abs(EditorAngleMapping.NormalizeDeg(a - b));
        return Math.Min(d, 360 - d);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        BeginPlacement(true);
        EndPlacement(true);
        return true;
    }

    // only replace another shoulder note on the same side, not anything sharing the beat.
    public override bool ReplacesExistingObject(GarbusHitObject existing) =>
        base.ReplacesExistingObject(existing) && existing is ShoulderNote shoulder && shoulder.Side == HitObject.Side;
}
