// Groups cardinal-directed notes (CardinalNote + CardinalHoldNote) that share an exact StartTime into
// "chords" of size >= 2. Pure and immutable: built from a hit-object snapshot, no drawing/framework types.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Gameplay.Objects;

namespace Garbus.Game.Objects;

public sealed class ChordIndex
{
    public readonly record struct ChordMember(HitObject Object, int AngleDeg);

    public sealed class ChordGroup
    {
        public double StartTime { get; }
        public IReadOnlyList<ChordMember> Members { get; }

        public ChordGroup(double startTime, IReadOnlyList<ChordMember> members)
        {
            StartTime = startTime;
            Members = members;
        }
    }

    private readonly IReadOnlyList<ChordGroup> groups;
    private readonly HashSet<HitObject> members;

    public ChordIndex(IEnumerable<HitObject> hitObjects)
    {
        var buckets = new Dictionary<double, List<ChordMember>>();

        foreach (var h in hitObjects)
        {
            if (!isCardinalDirected(h, out int angle))
                continue;

            if (!buckets.TryGetValue(h.StartTime, out var list))
                buckets[h.StartTime] = list = new List<ChordMember>();

            list.Add(new ChordMember(h, angle));
        }

        var kept = buckets
                   .Where(kvp => kvp.Value.Count >= 2)
                   .OrderBy(kvp => kvp.Key)
                   .Select(kvp => new ChordGroup(
                       kvp.Key,
                       kvp.Value.OrderBy(m => m.AngleDeg).ToArray()))
                   .ToArray();

        groups = kept;
        members = new HashSet<HitObject>(kept.SelectMany(g => g.Members).Select(m => m.Object));
    }

    public IReadOnlyList<ChordGroup> Groups => groups;

    public bool IsInChord(HitObject hitObject) => members.Contains(hitObject);

    // Cardinal-directed = CardinalNote or CardinalHoldNote specifically. ShoulderNote also carries a
    // cardinal Direction but is deliberately excluded, so match the concrete types, not IHasCardinalDirection.
    private static bool isCardinalDirected(HitObject h, out int angleDeg)
    {
        switch (h)
        {
            case CardinalNote c:
                angleDeg = c.AngleDeg;
                return true;
            case CardinalHoldNote hold:
                angleDeg = hold.AngleDeg;
                return true;
            default:
                angleDeg = 0;
                return false;
        }
    }
}
