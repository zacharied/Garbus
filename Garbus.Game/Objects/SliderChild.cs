// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/SliderChild.cs).

using Garbus.Game.Gameplay.Audio;

namespace Garbus.Game.Objects;

public class SliderChild : GarbusHitObject, IHasAngle
{
    public SliderBody Parent;
    public GarbusPathControlPoint ControlPoint;
    public GarbusHitObject HeadReference { get; }

    public SliderChild(SliderBody parent, GarbusPathControlPoint controlPoint, GarbusHitObject headReference)
    {
        Parent = parent;
        ControlPoint = controlPoint;
        HeadReference = headReference;
    }

    public int AngleDeg => Parent.AngleDeg + ControlPoint.RotationOffset;

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SliderChild;

    public override double MaximumJudgementOffset => global::Garbus.Game.Objects.Judgement.SliderCatchHitWindows.PERFECT_WINDOW;
}
