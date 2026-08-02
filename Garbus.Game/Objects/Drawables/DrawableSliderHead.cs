using Garbus.Game.Input;
using Garbus.Game.Objects.Judgement;
using Garbus.Game.UI;
using osu.Framework.Allocation;

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableSliderHead : DrawableGarbusHitObject<SliderHead>, ISelfPosition
{
    [Resolved]
    private AnalogInputManager analogInput { get; set; } = null!;

    [Resolved]
    private SlamCoincidenceIndex slamCoincidenceIndex { get; set; } = null!;

    public DrawableSliderHead(SliderHead hitObject)
        : base(hitObject)
    {
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset < 0)
            return;

        if (analogInput.SliderCatchers[HitObject.Parent.Side].IsCatchingAt(HitObject.AngleDeg))
        {
            ApplyMaxResult();
            return;
        }

        if (timeOffset > SliderNodeHitWindows.NODE_WINDOW)
        {
            bool? coincidentSlamHit = slamCoincidenceIndex.SlamHitAt(HitObject.StartTime, HitObject.Parent.Side);
            if (coincidentSlamHit is null)
                return;

            if (coincidentSlamHit.Value)
                ApplyMaxResult();
            else
                ApplyMinResult();
        }
    }
}
