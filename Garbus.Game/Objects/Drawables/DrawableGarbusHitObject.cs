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

    /// <summary>Number of family members this object has played. Test seam.</summary>
    public int SamplesPlayCount => Samples?.PlayCount ?? 0;

    public override void PlaySamples() => GarbusHitSoundPlayback.Play(Samples, HitObject, Result);
}
