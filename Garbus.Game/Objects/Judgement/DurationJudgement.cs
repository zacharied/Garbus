// Duration-tail resolution shared by hold notes and slider segments, per
// docs/rules-specs/Judgement.md ("Final judgement").

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public static class DurationJudgement
{
    public static HitResult Resolve(
        double duration,
        double activatedDuration,
        double headWindow,
        bool headWasHit,
        bool activatedAtEnd,
        bool activatedDuringEndGrace,
        double bestThreshold,
        double perfectThreshold,
        double badThreshold)
    {
        HitResult result;

        if (duration < headWindow)
        {
            result = headWasHit ? HitResult.CriticalPerfect : HitResult.Miss;
        }
        else
        {
            double fraction = duration > 0 ? activatedDuration / duration : 1;

            result = fraction >= bestThreshold ? HitResult.CriticalPerfect
                : fraction >= perfectThreshold ? HitResult.Perfect
                : fraction >= badThreshold ? HitResult.Bad
                : HitResult.Miss;

            if (!activatedDuringEndGrace && result == HitResult.CriticalPerfect)
                result = HitResult.Perfect;

        }

        if (activatedAtEnd && result == HitResult.Miss)
            result = HitResult.Bad;

        return result;
    }
}
