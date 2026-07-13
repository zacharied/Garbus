// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/SliderHead.cs).

using Garbus.Game.Gameplay.Audio;

namespace Garbus.Game.Objects;

public class SliderHead : GarbusHitObject, IHasAngle
{
    private readonly SliderBody parent;

    public SliderHead(SliderBody parent)
    {
        this.parent = parent;
    }

    public int AngleDeg => parent.AngleDeg;

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SliderHead;
}
