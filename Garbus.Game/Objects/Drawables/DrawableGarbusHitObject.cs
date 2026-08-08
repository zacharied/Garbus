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

    // Mirrors GarbusScrollingHitObjectContainer's fallback: bare test scenes without a cached
    // GarbusScrollingInfo still get the production defaults rather than a second set of literals.
    private readonly GarbusScrollingInfo fallbackScrollingInfo = new GarbusScrollingInfo();

    // Never null before load() resolves the real scrollingInfo/fallback choice — see
    // GarbusScrollingHitObjectContainer.effectiveScrollingInfo's doc comment for why this is
    // initialised to the fallback rather than left null.
    private GarbusScrollingInfo effectiveScrollingInfo;

    /// <summary>
    /// How long this object's spawn animation runs. The same quantity as the motionless hold it spends
    /// on the spawn halo, so the animation finishes exactly as the object starts moving. Specified in
    /// docs/presentation-specs/Playfield.md.
    /// </summary>
    protected double SpawnAnimationDuration => effectiveScrollingInfo.SpawnDuration.Value;

    // Anchor UpdateInitialTransforms at the note's halo-spawn time (StartTime − LeadTime) so the spawn
    // animation plays across exactly the window the note spends motionless on the halo and, being
    // absolute-sequenced, replays on rewind/restart and under editor-preview scrubbing. Base 10000
    // would fire it ~10s early / invisibly.
    protected override double InitialLifetimeOffset => effectiveScrollingInfo.LeadTime;

    public DrawableGarbusHitObject(T hitObject)
        : base(hitObject)
    {
        // Never null before load() resolves the real scrollingInfo/fallback choice — see the field's
        // doc comment. A field initialiser can't reference another instance member (CS0236), so this
        // has to happen here rather than at the declaration.
        effectiveScrollingInfo = fallbackScrollingInfo;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        effectiveScrollingInfo = scrollingInfo ?? fallbackScrollingInfo;
    }

    /// <summary>Number of family members this object has played. Test seam.</summary>
    public int SamplesPlayCount => Samples?.PlayCount ?? 0;

    public override void PlaySamples() => GarbusHitSoundPlayback.Play(Samples, HitObject, Result);
}
