// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/SliderHead.cs).

namespace Garbus.Game.Objects;

public class SliderHead : GarbusHitObject, IHasAngle
{
    private readonly SliderBody parent;

    public SliderHead(SliderBody parent)
    {
        this.parent = parent;
    }

    public int AngleDeg => parent.AngleDeg;
}
