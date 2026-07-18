// Unit tests for the song-select chart library: grouping, sorting, and (later tasks) the two
// concrete sources. Plain NUnit — no game host.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Screens.SongSelect;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;

namespace Garbus.Game.Tests.Charts
{
    [TestFixture]
    public class TestChartLibrary
    {
        // A stub source that just replays hand-built cards (grouping tests need no I/O).
        private class StubSource : IChartSource
        {
            private readonly IEnumerable<ChartCard> cards;
            public StubSource(params ChartCard[] cards) => this.cards = cards;
            public IEnumerable<ChartCard> Enumerate() => cards;
            public GarbusChart LoadChart(ChartCard card) => new GarbusChart();
            public Track GetTrack(ChartCard card, AudioManager audio) => null!;
        }

        private ChartCard card(string group, string title, string artist, int level)
            => new ChartCard { Source = null!, Locator = $"{group}/{title}", GroupKey = group, Title = title, Artist = artist, Level = level };

        [Test]
        public void TestGroupsByGroupKey()
        {
            var lib = new ChartLibrary(new StubSource(
                card("a", "Song A", "Artist A", 5),
                card("a", "Song A", "Artist A", 2),
                card("b", "Song B", "Artist B", 4)));

            var groups = lib.Scan();

            Assert.That(groups.Count, Is.EqualTo(2));
            var a = groups.Single(g => g.Title == "Song A");
            Assert.That(a.Charts.Count, Is.EqualTo(2));
        }

        [Test]
        public void TestChartsWithinGroupSortedByLevel()
        {
            var lib = new ChartLibrary(new StubSource(
                card("a", "Song A", "Artist A", 7),
                card("a", "Song A", "Artist A", 2)));

            var group = lib.Scan().Single();
            Assert.That(group.Charts.Select(c => c.Level), Is.EqualTo(new[] { 2, 7 }));
        }

        [Test]
        public void TestGroupsSortedByTitle()
        {
            var lib = new ChartLibrary(new StubSource(
                card("z", "Zed", "x", 1),
                card("a", "Alpha", "x", 1)));

            Assert.That(lib.Scan().Select(g => g.Title), Is.EqualTo(new[] { "Alpha", "Zed" }));
        }

        [Test]
        public void TestAllChartsSortedByLevelAcrossGroups()
        {
            var lib = new ChartLibrary(new StubSource(
                card("a", "Song A", "x", 5),
                card("b", "Song B", "x", 1),
                card("a", "Song A", "x", 3)));

            Assert.That(lib.AllCharts().Select(c => c.Level), Is.EqualTo(new[] { 1, 3, 5 }));
        }
    }
}
