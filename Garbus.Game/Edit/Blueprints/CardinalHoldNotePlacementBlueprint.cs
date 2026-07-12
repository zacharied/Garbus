// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/CardinalHoldNotePlacementBlueprint.cs).
// BacHitObject → GarbusHitObject; OsuColour resolve dropped — the body colour (osu's colours.Yellow)
// is inlined as Colour4.Yellow; EditorDrawableCardinalNote from Edit.Drawables; SnapResult from
// Edit.Compose; Composer/HitObject via GarbusPlacementBlueprint. Logic identical.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints;

/// <summary>
/// Mania-style hold placement: the first click commits the start time/angle, dragging stretches the
/// duration (in either direction — dragging upward swaps start/end), release commits.
/// </summary>
internal partial class CardinalHoldNotePlacementBlueprint : GarbusPlacementBlueprint<CardinalHoldNote>
{
    private readonly Box bodyPiece;
    private readonly EditSquarePiece headPiece;
    private readonly EditSquarePiece tailPiece;

    private double originalStartTime;

    protected override bool IsValidForPlacement => base.IsValidForPlacement && (PlacementActive == PlacementState.Waiting || Precision.DefinitelyBigger(HitObject.Duration, 0));

    public CardinalHoldNotePlacementBlueprint()
        : base(new CardinalHoldNote { AngleDeg = 0 })
    {
        InternalChildren = new Drawable[]
        {
            bodyPiece = new Box
            {
                Origin = Anchor.BottomCentre,
                Width = 12,
                Colour = Colour4.Yellow,
                Alpha = 0.4f,
            },
            headPiece = new EditSquarePiece
            {
                Origin = Anchor.Centre,
                Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE),
            },
            tailPiece = new EditSquarePiece
            {
                Origin = Anchor.Centre,
                Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE, 10),
            },
        };
    }

    protected override void Update()
    {
        base.Update();

        if (Composer == null)
            return;

        var container = Composer.Playfield.HitObjectContainer;
        float x = EditorAngleMapping.ToX(HitObject.AngleDeg) * DrawWidth;

        headPiece.Position = new Vector2(x, ToLocalSpace(container.ScreenSpacePositionAtTime(HitObject.StartTime)).Y);
        tailPiece.Position = new Vector2(x, ToLocalSpace(container.ScreenSpacePositionAtTime(HitObject.EndTime)).Y);

        // downward scrolling: the (later) tail sits above the head.
        float bottom = Math.Max(headPiece.Y, tailPiece.Y);
        float top = Math.Min(headPiece.Y, tailPiece.Y);

        bodyPiece.Position = new Vector2(x, bottom);
        bodyPiece.Height = bottom - top;
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
        var result = base.UpdateTimeAndPosition(screenSpacePosition, fallbackTime);

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
        }

        return result;
    }
}
