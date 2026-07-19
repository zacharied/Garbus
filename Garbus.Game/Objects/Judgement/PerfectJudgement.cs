// A judgement capped at Perfect — for catch-timed slider parts and early-permissive slams, whose
// families have no Critical Perfect (see docs/rules-specs/Judgement.md, "Judgement families").

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class PerfectJudgement : Gameplay.Judgements.Judgement
{
    public override HitResult MaxResult => HitResult.Perfect;
}
