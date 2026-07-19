// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/SliderChild.cs).

using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Objects.Judgement;

namespace Garbus.Game.Objects;

public class SliderChild : GarbusHitObject, IHasAngle
{
    public SliderBody Parent;
    public GarbusPathControlPoint ControlPoint;

    public SliderChild(SliderBody parent, GarbusPathControlPoint controlPoint)
    {
        Parent = parent;
        ControlPoint = controlPoint;
    }

    public int AngleDeg => Parent.AngleDeg + ControlPoint.RotationOffset;

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SliderChild;

    public override Gameplay.Judgements.Judgement CreateJudgement() => new PerfectJudgement();
}
