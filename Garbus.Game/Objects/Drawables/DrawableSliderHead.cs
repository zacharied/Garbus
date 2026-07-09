// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableSliderHead.cs).

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableSliderHead : DrawableGarbusHitObject<SliderHead>, ISelfPosition
{
    public DrawableSliderHead(SliderHead hitObject)
        : base(hitObject)
    {
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset >= 0)
            // todo: implement judgement logic
            ApplyMaxResult();
    }
}
