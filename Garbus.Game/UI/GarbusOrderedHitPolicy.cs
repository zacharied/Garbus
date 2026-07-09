// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/BacOrderedHitPolicy.cs).
// BacOrderedHitPolicy → GarbusOrderedHitPolicy. Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.

using System.Collections.Generic;
using osu.Framework.Extensions.IEnumerableExtensions;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Objects.Drawables;

namespace Garbus.Game.UI;

/// <summary>
/// Ensures that only the most recent <see cref="Objects.GarbusHitObject"/> in a single <see cref="Lane"/> is
/// hittable — the "note lock". Mirrors mania's <c>OrderedHitPolicy</c>, but because each <see cref="Lane"/>
/// owns its own <see cref="HitObjectContainer"/>, the policy only ever sees that lane's objects. Note lock
/// is therefore lane-independent: hitting (or missing) a north note never locks out an east note.
/// </summary>
public class GarbusOrderedHitPolicy
{
    private readonly HitObjectContainer hitObjectContainer;

    public GarbusOrderedHitPolicy(HitObjectContainer hitObjectContainer)
    {
        this.hitObjectContainer = hitObjectContainer;
    }

    /// <summary>
    /// Determines whether a <see cref="DrawableHitObject"/> can be hit at a point in time. Only the most
    /// recent object in the lane can be hit; an earlier object's window cannot extend past the next one.
    /// </summary>
    public bool IsHittable(DrawableHitObject hitObject, double time)
    {
        var nextObject = hitObjectContainer.AliveObjects.GetNext(hitObject);
        return nextObject == null || time < nextObject.HitObject.StartTime;
    }

    /// <summary>
    /// Handles an object being hit, force-missing every earlier un-judged object in the same lane so a
    /// skipped note cannot be hit after a later one.
    /// </summary>
    public void HandleHit(DrawableHitObject hitObject)
    {
        foreach (var obj in enumerateHitObjectsUpTo(hitObject.HitObject.StartTime))
        {
            if (obj.Judged)
                continue;

            if (obj is IHittableNote note)
                note.MissForcefully();
        }
    }

    private IEnumerable<DrawableHitObject> enumerateHitObjectsUpTo(double targetTime)
    {
        foreach (var obj in hitObjectContainer.AliveObjects)
        {
            if (obj.HitObject.GetEndTime() >= targetTime)
                yield break;

            yield return obj;
        }
    }
}
