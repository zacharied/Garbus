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

    /// <summary>The duration of the segment ending at this child. Zero at a jump.</summary>
    public double SegmentDuration => StartTime - Parent.GetSegmentStartTime(this);

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SliderChild;

    public override double MaximumJudgementOffset => SliderNodeHitWindows.NODE_WINDOW;

    // A jump has no segment to grade, so the child is judged as a node — Perfect, Bad or Miss.
    public override Gameplay.Judgements.Judgement CreateJudgement()
        => SegmentDuration <= 0 ? new PerfectJudgement() : new Gameplay.Judgements.Judgement();
}
