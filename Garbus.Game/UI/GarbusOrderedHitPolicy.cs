// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/BacOrderedHitPolicy.cs).
// BacOrderedHitPolicy → GarbusOrderedHitPolicy. Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: rewritten from mania's ordered policy to the Garbus judgement spec's
// note-lock (docs/rules-specs/Judgement.md) — oldest-eligible resolution, no force-missing.

using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Objects.Drawables;

namespace Garbus.Game.UI;

/// <summary>
/// The spec's note-lock: an input interacts with the <b>oldest eligible object in the lane whose
/// window contains it</b>. <see cref="IsHittable"/> vetoes a candidate when an older, press-unjudged
/// object's window also contains the press; combined with notes declining presses their own window
/// doesn't contain (<c>ResultFor == None</c> applies nothing), exactly one object accepts any press
/// regardless of input-queue order. Objects leave eligibility only by being judged (including via an
/// early-miss press) or by their own late window elapsing — hitting a later object never affects an
/// earlier one.
/// </summary>
public class GarbusOrderedHitPolicy
{
    private readonly HitObjectContainer hitObjectContainer;

    public GarbusOrderedHitPolicy(HitObjectContainer hitObjectContainer)
    {
        this.hitObjectContainer = hitObjectContainer;
    }

    /// <summary>
    /// Determines whether a <see cref="DrawableHitObject"/> may accept a press at a point in time.
    /// </summary>
    public bool IsHittable(DrawableHitObject hitObject, double time)
    {
        // AliveObjects is ordered by start time (ascending).
        foreach (var obj in hitObjectContainer.AliveObjects)
        {
            if (obj.HitObject.StartTime >= hitObject.HitObject.StartTime)
                return true; // no older candidates remain

            if (obj is not IHittableNote older || older.PressJudged)
                continue;

            if (obj.HitObject.HitWindows.ResultFor(time - obj.HitObject.StartTime) != HitResult.None)
                return false; // the press belongs to this older object
        }

        return true;
    }
}
