// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/CardinalHoldNoteHead.cs).

using Garbus.Game.Input;

namespace Garbus.Game.Objects;

/// <summary>
/// The head of a <see cref="CardinalHoldNote"/>: a timed press judged exactly like a <see cref="CardinalNote"/>
/// (same <see cref="Note.CreateHitWindows"/> defaults). It nests inside the hold at the hold's
/// <see cref="Gameplay.Objects.HitObject.StartTime"/> and takes its angle / button from the parent, so it
/// shares the parent's lane and direction. Its judgement is folded into the hold's final result at the tail
/// (see <see cref="Drawables.DrawableCardinalHoldNote"/>).
/// </summary>
public class CardinalHoldNoteHead : Note, IHasAngle
{
    public readonly CardinalHoldNote Parent;

    public CardinalHoldNoteHead(CardinalHoldNote parent)
    {
        Parent = parent;
    }

    public int AngleDeg => Parent.AngleDeg;

    public override GarbusButtonInput ButtonInput => Parent.ButtonInput;
}
