// Unions charts from every source and groups them by folder into songs. A rescan is cheap enough
// to run on every screen entry (metadata is decoded per file; see the sources).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Garbus.Game.Screens.SongSelect
{
    public class ChartLibrary
    {
        private readonly IReadOnlyList<IChartSource> sources;

        public ChartLibrary(params IChartSource[] sources) => this.sources = sources;

        /// <summary>All cards from all sources, flattened and sorted by level (flat view order).</summary>
        public IReadOnlyList<ChartCard> AllCharts() =>
            sources.SelectMany(s => s.Enumerate())
                   .OrderBy(c => c.Level)
                   .ToList();

        /// <summary>Cards grouped by folder into songs, groups sorted by title (grouped view order).</summary>
        public IReadOnlyList<SongGroup> Scan() =>
            sources.SelectMany(s => s.Enumerate())
                   .GroupBy(c => c.GroupKey)
                   .Select(g => new SongGroup(
                       g.First().Title,
                       g.First().Artist,
                       g.OrderBy(c => c.Level).ToList()))
                   .OrderBy(sg => sg.Title, StringComparer.OrdinalIgnoreCase)
                   .ToList();
    }
}
