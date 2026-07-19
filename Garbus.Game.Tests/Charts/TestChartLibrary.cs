// Unit tests for the song-select chart library: grouping, sorting, and (later tasks) the two
// concrete sources. Plain NUnit — no game host.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using Garbus.Game.Screens.SongSelect;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;

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
            public Texture? GetBackground(ChartCard card) => null;
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

        // Writes a chart's JSON into <root>/Charts/<name> so a ChartStore over <root> can find it.
        private static void writeResourceChart(string root, string name, string title, int level)
        {
            var chart = new GarbusChart { Metadata = { Title = title, Artist = "A", Level = level, AudioFile = "song.ogg" } };
            string full = Path.Combine(root, "Charts", name);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, GarbusChartSerializer.Encode(chart));
        }

        // osu-framework's StorageBackedResourceStore.GetAvailableResources() is
        // storage.GetDirectories("").SelectMany(d => storage.GetFiles(d)) — it only sees files exactly
        // one level inside a directory exactly one level from its root, so it can never report a root-level
        // file (like "flat.garbus") alongside a two-levels-deep one (like "set/easy.garbus") at once. Real
        // bundled resources go through DllResourceStore instead, whose GetAvailableResources() enumerates
        // the assembly's manifest resource names directly and has no such depth limit. This stub models
        // that (full recursive, real file I/O) so the test exercises ChartStore/ResourceChartSource's own
        // grouping logic rather than the unrelated upstream enumeration bug.
        private class RecursiveDirectoryResourceStore : IResourceStore<byte[]>
        {
            private readonly string root;
            public RecursiveDirectoryResourceStore(string root) => this.root = root;

            public byte[] Get(string name) => File.ReadAllBytes(Path.Combine(root, name));

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) =>
                File.ReadAllBytesAsync(Path.Combine(root, name), cancellationToken);

            public Stream GetStream(string name) => File.OpenRead(Path.Combine(root, name));

            public IEnumerable<string> GetAvailableResources() =>
                Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                         .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'));

            public void Dispose() { }
        }

        [Test]
        public void TestResourceSourceEnumeratesAndGroups()
        {
            string root = Directory.CreateTempSubdirectory("garbus-res-").FullName;
            try
            {
                writeResourceChart(root, "flat.garbus", "Flat Song", 3);
                writeResourceChart(root, "set/easy.garbus", "Set Song", 2);
                writeResourceChart(root, "set/hard.garbus", "Set Song", 8);

                var store = new ChartStore(new RecursiveDirectoryResourceStore(root));
                var source = new ResourceChartSource(store, null!);

                var cards = source.Enumerate().ToList();
                Assert.That(cards.Count, Is.EqualTo(3));

                var groups = new ChartLibrary(source).Scan();
                Assert.That(groups.Count, Is.EqualTo(2)); // flat song + set song
                Assert.That(groups.Single(g => g.Title == "Set Song").Charts.Count, Is.EqualTo(2));

                // Full load applies defaults (nested objects/hit windows) without throwing.
                var loaded = cards.First().LoadChart();
                Assert.That(loaded, Is.Not.Null);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void writeDiskChart(string dir, string file, string title, int level)
        {
            Directory.CreateDirectory(dir);
            var chart = new GarbusChart { Metadata = { Title = title, Artist = "A", Level = level, AudioFile = "song.ogg" } };
            File.WriteAllText(Path.Combine(dir, file), GarbusChartSerializer.Encode(chart));
        }

        [Test]
        public void TestDirectorySourceGroupsByFolder()
        {
            string root = Directory.CreateTempSubdirectory("garbus-dir-").FullName;
            try
            {
                writeDiskChart(Path.Combine(root, "song-a"), "easy.garbus", "Song A", 2);
                writeDiskChart(Path.Combine(root, "song-a"), "hard.garbus", "Song A", 7);
                writeDiskChart(Path.Combine(root, "song-b"), "normal.garbus", "Song B", 4);

                using var source = new DirectoryChartSource(root);
                var groups = new ChartLibrary(source).Scan();

                Assert.That(groups.Count, Is.EqualTo(2));
                Assert.That(groups.Single(g => g.Title == "Song A").Charts.Count, Is.EqualTo(2));

                var loaded = source.Enumerate().First().LoadChart();
                Assert.That(loaded, Is.Not.Null);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void TestDirectorySourceEmptyWhenRootMissing()
        {
            using var source = new DirectoryChartSource(Path.Combine(Path.GetTempPath(), "garbus-does-not-exist-" + System.Guid.NewGuid()));
            Assert.That(source.Enumerate().ToList(), Is.Empty);
        }

        [Test]
        public void TestDirectorySourcePopulatesBackgroundFile()
        {
            string root = Directory.CreateTempSubdirectory("garbus-bg-").FullName;
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "song-bg"));
                var chart = new GarbusChart { Metadata = { Title = "BG Song", Artist = "A", Level = 3, AudioFile = "song.ogg", BackgroundFile = "bg.jpg" } };
                File.WriteAllText(Path.Combine(root, "song-bg", "chart.garbus"), GarbusChartSerializer.Encode(chart));

                using var source = new DirectoryChartSource(root);
                var card = source.Enumerate().Single();

                Assert.That(card.BackgroundFile, Is.EqualTo("bg.jpg"));
                // No GameHost supplied → no texture store → null (placeholder path).
                Assert.That(source.GetBackground(card), Is.Null);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void TestResourceSourceBackgroundNullWhenEmpty()
        {
            var card = new ChartCard { Source = null!, Locator = "x", GroupKey = "g", BackgroundFile = string.Empty };
            var source = new ResourceChartSource(null!, null!); // textures default null
            Assert.That(source.GetBackground(card), Is.Null);
        }
    }
}
