// Shoulder hold placement: click begins, drag stretches the duration, release commits. The side is picked
// from the nearer shoulder lane strip while waiting (as ShoulderNotePlacementBlueprint); the duration drag
// mirrors CardinalHoldNotePlacementBlueprint.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using Garbus.Game.Core;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints;

internal partial class ShoulderHoldNotePlacementBlueprint : HitObjectPlacementBlueprint
{
    protected new ShoulderHoldNote HitObject => (ShoulderHoldNote)base.HitObject;

    [Resolved]
    private GarbusHitObjectComposer? composer { get; set; }

    private readonly Box bodyPiece;
    private readonly PlacementSpritePreview headSprite;
    private readonly EditSquarePiece headPiece;
    private readonly EditSquarePiece tailPiece;

    private double originalStartTime;

    protected override bool IsValidForPlacement => base.IsValidForPlacement
        && (PlacementActive == PlacementState.Waiting || Precision.DefinitelyBigger(HitObject.Duration, 0));

    public ShoulderHoldNotePlacementBlueprint()
        : base(new ShoulderHoldNote { Side = HorizontalDirection.Left })
    {
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            bodyPiece = new Box
            {
                Origin = Anchor.BottomCentre,
                Width = 12,
                Colour = Color4.MediumPurple,
                Alpha = 0.4f,
            },
            headSprite = new PlacementSpritePreview(HitObject),
            headPiece = new EditSquarePiece
            {
                Origin = Anchor.Centre,
                Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE),
                Colour = Color4.MediumPurple,
            },
            tailPiece = new EditSquarePiece
            {
                Origin = Anchor.Centre,
                Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE, 10),
                Colour = Color4.MediumPurple,
            },
        };
    }

    protected override void Update()
    {
        base.Update();

        if (composer == null)
            return;

        var container = composer.Playfield.HitObjectContainer;
        float x = GarbusEditorPlayfield.ShoulderXFraction(HitObject.Side) * DrawWidth;

        headPiece.Position = new Vector2(x, ToLocalSpace(container.ScreenSpacePositionAtTime(HitObject.StartTime)).Y);
        headSprite.Position = headPiece.Position;
        tailPiece.Position = new Vector2(x, ToLocalSpace(container.ScreenSpacePositionAtTime(HitObject.EndTime)).Y);

        float bottom = Math.Max(headPiece.Y, tailPiece.Y);
        float top = Math.Min(headPiece.Y, tailPiece.Y);

        bodyPiece.Position = new Vector2(x, bottom);
        bodyPiece.Height = bottom - top;
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        BeginPlacement(true);
        return true;
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        base.OnMouseUp(e);
        EndPlacement(true);
    }

    public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
    {
        var result = composer?.FindSnappedAngleTimeAndPosition(screenSpacePosition) ?? new SnapResult(screenSpacePosition, fallbackTime);

        base.UpdateTimeAndPosition(result.ScreenSpacePosition, result.Time ?? fallbackTime);

        if (PlacementActive == PlacementState.Active)
        {
            if (result.Time is double endTime)
            {
                HitObject.StartTime = endTime < originalStartTime ? endTime : originalStartTime;
                HitObject.Duration = Math.Abs(endTime - originalStartTime);
            }
        }
        else
        {
            if (result.Time is double startTime)
                originalStartTime = startTime;

            if (composer != null)
            {
                var playfield = composer.Playfield;
                float cursorAngle = EditorAngleMapping.ToAngle(playfield.ToLocalSpace(screenSpacePosition).X / playfield.DrawWidth);
                HitObject.Side = wrapDistance(cursorAngle, GarbusEditorPlayfield.LEFT_SHOULDER_ANGLE_DEG) <= wrapDistance(cursorAngle, GarbusEditorPlayfield.RIGHT_SHOULDER_ANGLE_DEG)
                    ? HorizontalDirection.Left
                    : HorizontalDirection.Right;
            }
        }

        return result;
    }

    private static float wrapDistance(float a, float b)
    {
        float d = Math.Abs(EditorAngleMapping.NormalizeDeg(a - b));
        return Math.Min(d, 360 - d);
    }

    public override bool ReplacesExistingObject(GarbusHitObject existing) =>
        base.ReplacesExistingObject(existing) && existing is ShoulderHoldNote shoulder && shoulder.Side == HitObject.Side;
}
