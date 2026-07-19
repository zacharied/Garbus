// The note-family timing windows for shoulder notes and holds, per docs/rules-specs/Judgement.md.
// The Miss window is early-only: pressing inside it registers an immediate Miss; late mistimes are
// instead handled by eligibility elapsing (LateEligibilityEdge). The spec marks these extents
// provisional — tune them here.

using System;
using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class ShoulderNoteHitWindows : HitWindows
{
    private const double critical_perfect_window = 40;
    private const double perfect_window = 80;
    private const double near_window = 150;
    private const double early_miss_window = 200;

    public override bool IsHitResultAllowed(HitResult result)
        => result is HitResult.CriticalPerfect or HitResult.Perfect or HitResult.Near or HitResult.Miss;

    public override HitWindowRange WindowFor(HitResult result)
    {
        switch (result)
        {
            case HitResult.CriticalPerfect:
                return HitWindowRange.Symmetric(critical_perfect_window);

            case HitResult.Perfect:
                return HitWindowRange.Symmetric(perfect_window);

            case HitResult.Near:
                return HitWindowRange.Symmetric(near_window);

            case HitResult.Miss:
                return new HitWindowRange(early_miss_window, 0);

            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, null);
        }
    }
}
