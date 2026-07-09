// The minimal chart model for the Phase 2 vertical slice: just the hit objects. Phase 3 (native chart
// format) grows this with timing (ControlPointInfo), metadata and the audio reference.

using System.Collections.Generic;
using Garbus.Game.Objects;

namespace Garbus.Game.Charts;

public class GarbusChart
{
    public List<GarbusHitObject> HitObjects { get; init; } = new List<GarbusHitObject>();
}
