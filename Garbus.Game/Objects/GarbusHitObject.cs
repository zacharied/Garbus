// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/BacHitObject.cs). BacHitObject →
// GarbusHitObject.

using System.Linq;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Objects;

namespace Garbus.Game.Objects;

public abstract class GarbusHitObject : HitObject
{
    /// <summary>
    /// This type's hitsound family: the set of sounds, one per earnable judgement, from which the
    /// judged sound is chosen at hit time.
    /// </summary>
    public abstract HitsoundFamily Hitsounds { get; }

    protected override void ApplyDefaultsToSelf()
    {
        base.ApplyDefaultsToSelf();
        Samples = Hitsounds.AllSamples.ToList();
    }

    public override Gameplay.Judgements.Judgement CreateJudgement() => new();
}
