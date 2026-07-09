// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableBacHitObject.cs).
// DrawableBacHitObject → DrawableGarbusHitObject.

using Garbus.Game.Gameplay.Objects.Drawables;

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableGarbusHitObject<T> : DrawableHitObject<GarbusHitObject>
    where T : GarbusHitObject
{
    public new T HitObject => (T)base.HitObject;

    public DrawableGarbusHitObject(T hitObject)
        : base(hitObject)
    {
    }
}
