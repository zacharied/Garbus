// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Judgement/SliderJudgementResult.cs).

using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects;

namespace Garbus.Game.Objects.Judgement;

public class SliderJudgementResult : JudgementResult
{
    public SliderJudgementResult(HitObject hitObject, Gameplay.Judgements.Judgement judgement)
        : base(hitObject, judgement)
    {
    }
}
