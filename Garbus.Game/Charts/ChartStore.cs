// Loads .garbus charts from the game's resource stores (replacing osu's WorkingBeatmap/realm chart
// management — deliberately minimal until song select arrives in Phase 5).

using System.IO;
using osu.Framework.IO.Stores;
using Garbus.Game.Charts.Format;

namespace Garbus.Game.Charts;

public class ChartStore
{
    private readonly IResourceStore<byte[]> store;

    public ChartStore(IResourceStore<byte[]> resources)
    {
        store = new NamespacedResourceStore<byte[]>(resources, @"Charts");
    }

    /// <summary>
    /// Loads and decodes the chart with the given filename (e.g. <c>"test-chart.garbus"</c>).
    /// Defaults are not applied — call <see cref="GarbusChart.ApplyDefaults"/> before play.
    /// </summary>
    /// <exception cref="FileNotFoundException">If no chart resource exists with the given name.</exception>
    public GarbusChart Get(string name)
    {
        using var stream = store.GetStream(name);

        if (stream == null)
            throw new FileNotFoundException($"Chart \"{name}\" not found in resources.");

        return GarbusChartSerializer.Decode(stream);
    }
}
