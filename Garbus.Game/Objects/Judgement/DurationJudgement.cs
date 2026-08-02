// Duration-tail resolution shared by hold notes and slider segments, per
// docs/rules-specs/Judgement.md ("Grace period", "Final judgement").

using System;
using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public static class DurationJudgement
{
    /// <summary>
    /// The activation credited to a duration, in milliseconds: the opening grace period — capped at
    /// the duration, and credited only when the object was Activated inside it — plus the activation
    /// measured after the grace period.
    /// </summary>
    public static double CreditedActivation(double duration, double gracePeriod, bool activatedDuringGrace, double activatedAfterGrace)
        => (activatedDuringGrace ? Math.Min(duration, gracePeriod) : 0) + activatedAfterGrace;

    public static HitResult Resolve(
        double duration,
        double activatedDuration,
        bool activatedAtEnd,
        bool activatedDuringEndGrace,
        double bestThreshold,
        double perfectThreshold,
        double badThreshold)
    {
        double fraction = duration > 0 ? activatedDuration / duration : 0;

        HitResult result = fraction >= bestThreshold ? HitResult.CriticalPerfect
            : fraction >= perfectThreshold ? HitResult.Perfect
            : fraction >= badThreshold ? HitResult.Bad
            : HitResult.Miss;

        if (!activatedDuringEndGrace && result == HitResult.CriticalPerfect)
            result = HitResult.Perfect;

        if (activatedAtEnd && result == HitResult.Miss)
            result = HitResult.Bad;

        return result;
    }
}
