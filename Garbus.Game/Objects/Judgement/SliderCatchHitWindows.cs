// Catch timing for slider heads and child pseudo-heads: no early-miss region and an inclusive
// Perfect window through 200 ms late, per docs/rules-specs/Judgement.md.

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class SliderCatchHitWindows : HitWindows
{
    public const double PERFECT_WINDOW = 200;

    public override bool IsHitResultAllowed(HitResult result) => result is HitResult.Perfect or HitResult.Miss;

    public override HitWindowRange WindowFor(HitResult result) => result switch
    {
        HitResult.Perfect => new HitWindowRange(0, PERFECT_WINDOW),
        HitResult.Miss => default,
        _ => throw new System.ArgumentOutOfRangeException(nameof(result), result, null),
    };
}
