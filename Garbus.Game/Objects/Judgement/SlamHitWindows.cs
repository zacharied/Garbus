// Early-permissive slam timing: Perfect is symmetric at 200 ms, Near extends only on the late side
// through 300 ms, and there is no input-triggered Miss window.

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class SlamHitWindows : HitWindows
{
    public const double PERFECT_WINDOW = 200;
    public const double LATE_NEAR_WINDOW = 300;

    public override bool IsHitResultAllowed(HitResult result)
        => result is HitResult.Perfect or HitResult.Near or HitResult.Miss;

    public override HitWindowRange WindowFor(HitResult result) => result switch
    {
        HitResult.Perfect => HitWindowRange.Symmetric(PERFECT_WINDOW),
        HitResult.Near => new HitWindowRange(0, LATE_NEAR_WINDOW),
        HitResult.Miss => default,
        _ => throw new System.ArgumentOutOfRangeException(nameof(result), result, null),
    };
}
