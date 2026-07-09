// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/BacHitObject.cs). BacHitObject →
// GarbusHitObject.

using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Objects;

namespace Garbus.Game.Objects;

public abstract class GarbusHitObject : HitObject
{
    protected GarbusHitObject()
    {
        Samples =
        [
            new(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT)
        ];
    }

    public override Gameplay.Judgements.Judgement CreateJudgement() => new();
}
