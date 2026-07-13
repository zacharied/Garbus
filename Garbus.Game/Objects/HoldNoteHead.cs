// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/HoldNoteHead.cs).

using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Input;

namespace Garbus.Game.Objects;

/// <summary>
/// The head of a hold note: a timed press judged exactly like a <see cref="CardinalNote"/>, nested inside
/// the hold at its start time. It takes its angle / button from the parent hold, so it shares the parent's
/// lane and direction. Its judgement is folded into the hold's final result at the tail
/// (see <see cref="Drawables.DrawableHoldNote{THitObject,THead}"/>).
/// </summary>
public class HoldNoteHead<TParent> : Note, IHasAngle
    where TParent : Note, IHasAngle
{
    public readonly TParent Parent;

    public HoldNoteHead(TParent parent)
    {
        Parent = parent;
    }

    public int AngleDeg => Parent.AngleDeg;

    public override GarbusButtonInput ButtonInput => Parent.ButtonInput;

    public override HitsoundFamily Hitsounds => Parent.Hitsounds;
}
