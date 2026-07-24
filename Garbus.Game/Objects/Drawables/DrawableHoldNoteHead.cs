// Generic over the head hit-object type so cardinal and shoulder holds share it.

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// The head of a <see cref="DrawableHoldNote{THitObject,THead}"/>. Purely judgemental — the parent hold
/// draws the head and owns input — so this draws nothing and never handles input. Its result is applied when
/// the parent delegates a press via <see cref="UpdateResult"/>, or it auto-misses once its window elapses.
/// </summary>
public partial class DrawableHoldNoteHead<THead> : DrawableGarbusHitObject<THead>, ISelfPosition
    where THead : Note
{
    public override bool DisplayResult => false;

    public DrawableHoldNoteHead(THead hitObject)
        : base(hitObject)
    {
    }

    public bool UpdateResult() => base.UpdateResult(true);

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (!userTriggered)
        {
            if (!HitObject.HitWindows.CanBeHit(timeOffset))
                ApplyMinResult();
            return;
        }

        var result = HitObject.HitWindows.ResultFor(timeOffset);

        if (result == HitResult.None)
            return;

        ApplyResult(result);
    }
}
