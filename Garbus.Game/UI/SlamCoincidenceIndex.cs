// Resolves the slider-node floor for coincident slams. A node waits while a same-time, same-side slam
// is unresolved; if any such slam hits, the node cannot receive Miss.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Objects;

namespace Garbus.Game.UI;

public class SlamCoincidenceIndex
{
    private readonly HashSet<GarbusHitObject> slams = new();
    private readonly Dictionary<GarbusHitObject, JudgementResult> results = new();

    public void Add(HitObject hitObject)
    {
        if (hitObject is GarbusSlamCentered or GarbusSlamEdge)
            slams.Add((GarbusHitObject)hitObject);
    }

    public void Remove(HitObject hitObject)
    {
        if (hitObject is not GarbusHitObject garbusHitObject)
            return;

        slams.Remove(garbusHitObject);
        results.Remove(garbusHitObject);
    }

    public void Record(JudgementResult result)
    {
        if (result.HitObject is GarbusSlamCentered or GarbusSlamEdge)
            results[(GarbusHitObject)result.HitObject] = result;
    }

    public void Revert(JudgementResult result)
    {
        if (result.HitObject is GarbusHitObject slam)
            results.Remove(slam);
    }

    /// <returns><c>null</c> while a matching slam is unresolved; otherwise whether any matching slam hit.</returns>
    public bool? SlamHitAt(double startTime, Core.HorizontalDirection side)
    {
        var matching = slams.Where(s => s.StartTime == startTime && ((IHasSide)s).Side == side).ToArray();
        if (matching.Length == 0)
            return false;

        if (matching.Any(s => results.TryGetValue(s, out var result) && result.IsHit))
            return true;

        return matching.All(results.ContainsKey) ? false : null;
    }
}
