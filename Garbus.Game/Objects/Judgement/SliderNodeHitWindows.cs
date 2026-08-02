// Catch timing for slider nodes: a symmetric window either side of StartTime, per
// docs/rules-specs/Judgement.md. Perfect is a state check at StartTime itself, so its window has no
// extent; Bad spans the rest of the node window.

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class SliderNodeHitWindows : HitWindows
{
    /// <summary>How far either side of a node's StartTime its angle may be covered and still count.</summary>
    public const double NODE_WINDOW = 200;

    public override bool IsHitResultAllowed(HitResult result)
        => result is HitResult.Perfect or HitResult.Bad or HitResult.Miss;

    public override HitWindowRange WindowFor(HitResult result) => result switch
    {
        HitResult.Perfect => default,
        HitResult.Bad => HitWindowRange.Symmetric(NODE_WINDOW),
        HitResult.Miss => default,
        _ => throw new System.ArgumentOutOfRangeException(nameof(result), result, null),
    };
}
