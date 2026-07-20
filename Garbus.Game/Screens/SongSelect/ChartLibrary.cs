// Unions charts from every source and groups them by folder into songs. A rescan is cheap enough
// to run on every screen entry (metadata is decoded per file; see the sources).

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;

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
        public IReadOnlyList<SongGroup> Scan()
        {
            var groups = sources.SelectMany(s => s.Enumerate())
                                .GroupBy(c => (c.Source, c.SongLocator))
                                .Select(g => new SongGroup(
                                    g.First().Title,
                                    g.First().Artist,
                                    g.OrderBy(c => c.Level).ToList(),
                                    g.First().SongId,
                                    g.First().SongLocator))
                                .OrderBy(sg => sg.Title, StringComparer.OrdinalIgnoreCase)
                                .ToList();

            foreach (var duplicate in groups.GroupBy(g => g.SongId).Where(g => g.Key != Guid.Empty && g.Count() > 1))
                Logger.Log($"Duplicate song ID {duplicate.Key} found at: {string.Join(", ", duplicate.Select(g => g.SongLocator))}",
                    level: LogLevel.Important);

            return groups;
        }
    }
}
