using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Objects.Judgement;

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

    public override double MaximumJudgementOffset => SliderNodeHitWindows.NODE_WINDOW;
}
