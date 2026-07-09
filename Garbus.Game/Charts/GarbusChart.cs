// The native chart model (Phase 3): hit objects + timing + metadata + audio reference. Serialized
// through Charts/Format (versioned JSON), replacing osu's Beatmap/WorkingBeatmap/converter pipeline.

using System.Collections.Generic;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Objects;

namespace Garbus.Game.Charts;

public class GarbusChart
{
    public ChartMetadata Metadata { get; init; } = new ChartMetadata();

    public ControlPointInfo ControlPointInfo { get; init; } = new ControlPointInfo();

    public List<GarbusHitObject> HitObjects { get; init; } = new List<GarbusHitObject>();

    /// <summary>
    /// Applies defaults (hit windows, nested object creation) to every hit object. Must be called
    /// after loading and before play.
    /// </summary>
    public void ApplyDefaults()
    {
        foreach (var hitObject in HitObjects)
            hitObject.ApplyDefaults();
    }
}
