// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/SliderHead.cs).

using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects.Judgement;

namespace Garbus.Game.Objects;

public class SliderHead : GarbusHitObject, IHasAngle
{
    public SliderBody Parent { get; }

    public SliderHead(SliderBody parent)
    {
        Parent = parent;
    }

    public int AngleDeg => Parent.AngleDeg;

    public override double MaximumJudgementOffset => SliderCatchHitWindows.PERFECT_WINDOW;

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SliderHead;

    public override Gameplay.Judgements.Judgement CreateJudgement() => new PerfectJudgement();

    protected override HitWindows CreateHitWindows() => new SliderCatchHitWindows();
}
