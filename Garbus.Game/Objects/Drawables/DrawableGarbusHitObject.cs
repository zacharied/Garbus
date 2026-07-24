using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI.Scrolling;
using osu.Framework.Allocation;

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableGarbusHitObject<T> : DrawableHitObject<GarbusHitObject>
    where T : GarbusHitObject
{
    public new T HitObject => (T)base.HitObject;

    [Resolved(CanBeNull = true)]
    private GarbusScrollingInfo? scrollingInfo { get; set; }

    // Anchor UpdateInitialTransforms at the note's centre-spawn / scroll-in time (StartTime − TimeRange)
    // so the spawn animation plays as the note appears and, being absolute-sequenced, replays on
    // rewind/restart and under editor-preview scrubbing. Base 10000 would fire it ~10s early / invisibly.
    protected override double InitialLifetimeOffset => scrollingInfo?.TimeRange.Value ?? 700;

    public DrawableGarbusHitObject(T hitObject)
        : base(hitObject)
    {
    }

    /// <summary>Number of family members this object has played. Test seam.</summary>
    public int SamplesPlayCount => Samples?.PlayCount ?? 0;

    public override void PlaySamples() => GarbusHitSoundPlayback.Play(Samples, HitObject, Result);
}
