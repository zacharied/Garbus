# Song Select Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `PlayScreen`'s hardcoded bundled-chart load with a real song-select screen that discovers charts from bundled resources + an AppData library folder, groups them by folder, previews audio, and launches the chosen chart into gameplay.

**Architecture:** An `IChartSource` abstraction yields `ChartCard`s (decoded metadata + a locator) from two providers — `ResourceChartSource` (bundled `Charts/*.garbus` via the existing `ChartStore`) and `DirectoryChartSource` (recursive scan of `%APPDATA%\Garbus\charts\`). `ChartLibrary` unions and groups them by folder into `SongGroup`s. `SongSelectScreen` renders a grouped/flat list (a view toggle drives layout + sort), plays a looping preview on selection, and pushes the existing `PlayScreen(chart, track)` constructor on launch.

**Tech Stack:** C# / .NET 8, osu-framework (`ppy.osu.Framework` 2026.629.0), NUnit visual `TestScene` + plain NUnit unit tests, System.Text.Json (already used by `GarbusChartSerializer`).

## Global Constraints

- Nullability is enabled solution-wide. DI-resolved / BDL-initialised fields use `= null!`.
- Terminology: osu's "beatmap" is "chart"; `Garbus*` prefixes (never `Bac*`).
- No version bumps on any format/schema; no backwards-compat layers (experimental project).
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- Commit messages end with the trailer:
  `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- New code lives under `Garbus.Game/Screens/SongSelect/`; tests under `Garbus.Game.Tests/`.
- A chart on disk is a folder holding its `.garbus` + audio + background (matches `ChartFile`).
- Resource track audio resolves through the DI `ITrackStore` (the `Tracks/` namespace); disk track
  audio resolves through a directory-rooted `ITrackStore` built from `AudioManager` (as
  `ChartFile.GetTrackStore` does).

---

## File Structure

- Create `Garbus.Game/Screens/SongSelect/IChartSource.cs` — the source abstraction.
- Create `Garbus.Game/Screens/SongSelect/ChartCard.cs` — one decoded chart entry.
- Create `Garbus.Game/Screens/SongSelect/SongGroup.cs` — a folder's charts, grouped.
- Create `Garbus.Game/Screens/SongSelect/ChartLibrary.cs` — union + group across sources.
- Create `Garbus.Game/Screens/SongSelect/ResourceChartSource.cs` — bundled-resource provider.
- Create `Garbus.Game/Screens/SongSelect/DirectoryChartSource.cs` — AppData-folder provider.
- Create `Garbus.Game/Screens/SongSelect/SongSelectScreen.cs` — the screen (list, toggle, preview, launch).
- Create `Garbus.Game/Screens/SongSelect/ChartRow.cs` — a clickable list row drawable.
- Modify `Garbus.Game/Charts/ChartStore.cs` — add `GetAvailableCharts()`.
- Modify `Garbus.Game/Configuration/GarbusSetting.cs` — add `SongSelectGrouped`.
- Modify `Garbus.Game/Configuration/GarbusConfigManager.cs` — default for `SongSelectGrouped`.
- Modify `Garbus.Game/Screens/MainMenuScreen.cs` — "Play" pushes `SongSelectScreen`.
- Modify `Garbus.Game/Screens/PlayScreen.cs` — reframe the `(chart, track, startTime)` ctor doc.
- Create `Garbus.Game.Tests/Charts/TestChartLibrary.cs` — unit tests for grouping + sources.
- Create `Garbus.Game.Tests/Visual/TestSceneSongSelect.cs` — visual/integration test.

---

## Task 1: Data models + library grouping

**Files:**
- Create: `Garbus.Game/Screens/SongSelect/IChartSource.cs`
- Create: `Garbus.Game/Screens/SongSelect/ChartCard.cs`
- Create: `Garbus.Game/Screens/SongSelect/SongGroup.cs`
- Create: `Garbus.Game/Screens/SongSelect/ChartLibrary.cs`
- Test: `Garbus.Game.Tests/Charts/TestChartLibrary.cs`

**Interfaces:**
- Produces:
  - `interface IChartSource { IEnumerable<ChartCard> Enumerate(); GarbusChart LoadChart(ChartCard card); Track GetTrack(ChartCard card, AudioManager audio); }`
  - `class ChartCard` with init props `IChartSource Source`, `string Locator`, `string GroupKey`, `string Title`, `string Artist`, `string ChartName`, `int Level`, `double? PreviewTime`, `string AudioFile`; computed `string DisplayName`; methods `GarbusChart LoadChart()`, `Track GetTrack(AudioManager audio)`.
  - `class SongGroup(string Title, string Artist, IReadOnlyList<ChartCard> Charts)`.
  - `class ChartLibrary(params IChartSource[] sources)` with `IReadOnlyList<SongGroup> Scan()` and `IReadOnlyList<ChartCard> AllCharts()`.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Charts/TestChartLibrary.cs`:

```csharp
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestChartLibrary`
Expected: FAIL to **compile** — `IChartSource`, `ChartCard`, `SongGroup`, `ChartLibrary` do not exist.

- [ ] **Step 3: Create `IChartSource.cs`**

```csharp
// Abstracts where playable charts come from (bundled resources vs an on-disk library folder).
// Enumeration + full load are pure; only track resolution needs the AudioManager.

using System.Collections.Generic;
using Garbus.Game.Charts;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;

namespace Garbus.Game.Screens.SongSelect
{
    public interface IChartSource
    {
        /// <summary>Decoded metadata for every chart this source exposes (broken files skipped).</summary>
        IEnumerable<ChartCard> Enumerate();

        /// <summary>Fully decodes the card's chart and applies defaults, ready for play.</summary>
        GarbusChart LoadChart(ChartCard card);

        /// <summary>A fresh <see cref="Track"/> for the card's audio. Caller owns/disposes it.</summary>
        Track GetTrack(ChartCard card, AudioManager audio);
    }
}
```

- [ ] **Step 4: Create `ChartCard.cs`**

```csharp
// One decoded chart entry in the song-select list: display metadata plus the locator its owning
// source uses to load the full chart and its audio.

using Garbus.Game.Charts;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;

namespace Garbus.Game.Screens.SongSelect
{
    public class ChartCard
    {
        /// <summary>The source that produced this card and can load its chart/track.</summary>
        public required IChartSource Source { get; init; }

        /// <summary>Source-specific handle (disk path or resource name).</summary>
        public required string Locator { get; init; }

        /// <summary>Folder identity charts are grouped by (a song = one folder).</summary>
        public required string GroupKey { get; init; }

        public string Title { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public string ChartName { get; init; } = string.Empty;
        public int Level { get; init; }
        public double? PreviewTime { get; init; }
        public string AudioFile { get; init; } = string.Empty;

        /// <summary>Title, plus the chart (difficulty) name in brackets when present.</summary>
        public string DisplayName => string.IsNullOrEmpty(ChartName) ? Title : $"{Title} [{ChartName}]";

        public GarbusChart LoadChart() => Source.LoadChart(this);

        public Track GetTrack(AudioManager audio) => Source.GetTrack(this, audio);
    }
}
```

- [ ] **Step 5: Create `SongGroup.cs`**

```csharp
// A song: the charts (difficulties) sharing one folder, with the group's display title/artist.

using System.Collections.Generic;

namespace Garbus.Game.Screens.SongSelect
{
    public class SongGroup
    {
        public string Title { get; }
        public string Artist { get; }
        public IReadOnlyList<ChartCard> Charts { get; }

        public SongGroup(string title, string artist, IReadOnlyList<ChartCard> charts)
        {
            Title = title;
            Artist = artist;
            Charts = charts;
        }
    }
}
```

- [ ] **Step 6: Create `ChartLibrary.cs`**

```csharp
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
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestChartLibrary`
Expected: PASS (4 tests).

- [ ] **Step 8: Commit**

```bash
git add Garbus.Game/Screens/SongSelect Garbus.Game.Tests/Charts/TestChartLibrary.cs
git commit -m "feat: song-select chart library models + folder grouping

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: ResourceChartSource

**Files:**
- Create: `Garbus.Game/Screens/SongSelect/ResourceChartSource.cs`
- Modify: `Garbus.Game/Charts/ChartStore.cs`
- Test: `Garbus.Game.Tests/Charts/TestChartLibrary.cs` (append)

**Interfaces:**
- Consumes: `ChartStore` (existing; gains `IEnumerable<string> GetAvailableCharts()`), `ITrackStore`.
- Produces: `class ResourceChartSource(ChartStore charts, ITrackStore trackStore) : IChartSource`.
- Grouping rule: `GroupKey` = the resource subfolder (`Path.GetDirectoryName`), or `"res:" + name`
  for a flat resource chart (each becomes its own single-chart group). `Locator` = the resource name.

- [ ] **Step 1: Write the failing test (append to `TestChartLibrary.cs`)**

Add these usings at the top of the file if missing: `using System.IO;`, `using Garbus.Game.Charts.Format;`, `using osu.Framework.IO.Stores;`. Append inside the class:

```csharp
        // Writes a chart's JSON into <root>/Charts/<name> so a ChartStore over <root> can find it.
        private static void writeResourceChart(string root, string name, string title, int level)
        {
            var chart = new GarbusChart { Metadata = { Title = title, Artist = "A", Level = level, AudioFile = "song.ogg" } };
            string full = Path.Combine(root, "Charts", name);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, GarbusChartSerializer.Encode(chart));
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

                var store = new ChartStore(new StorageBackedResourceStore(new osu.Framework.Platform.NativeStorage(root)));
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestResourceSourceEnumeratesAndGroups`
Expected: FAIL to compile — `ResourceChartSource` and `ChartStore.GetAvailableCharts` do not exist.

- [ ] **Step 3: Add `GetAvailableCharts()` to `ChartStore.cs`**

Add `using System.Collections.Generic;`, `using System.Linq;`, and `using System;` to the top of `Garbus.Game/Charts/ChartStore.cs`, then add this method inside the class (after `Get`):

```csharp
    /// <summary>
    /// Names of every bundled <c>.garbus</c> resource (namespace-relative, e.g.
    /// <c>"test-chart.garbus"</c> or <c>"set/easy.garbus"</c>).
    /// </summary>
    public IEnumerable<string> GetAvailableCharts() =>
        store.GetAvailableResources()
             .Where(n => n.EndsWith(".garbus", StringComparison.OrdinalIgnoreCase));
```

- [ ] **Step 4: Create `ResourceChartSource.cs`**

```csharp
// Song-select source over the bundled .garbus resources (read-only). Track audio for these charts
// lives in the game's Tracks/ resource namespace, resolved through the DI ITrackStore.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Logging;

namespace Garbus.Game.Screens.SongSelect
{
    public class ResourceChartSource : IChartSource
    {
        private readonly ChartStore charts;
        private readonly ITrackStore trackStore;

        public ResourceChartSource(ChartStore charts, ITrackStore trackStore)
        {
            this.charts = charts;
            this.trackStore = trackStore;
        }

        public IEnumerable<ChartCard> Enumerate()
        {
            foreach (string name in charts.GetAvailableCharts())
            {
                ChartCard? card = null;
                try
                {
                    var chart = charts.Get(name);
                    string? subfolder = Path.GetDirectoryName(name);
                    string groupKey = string.IsNullOrEmpty(subfolder) ? "res:" + name : "res:" + subfolder;

                    card = new ChartCard
                    {
                        Source = this,
                        Locator = name,
                        GroupKey = groupKey,
                        Title = chart.Metadata.Title,
                        Artist = chart.Metadata.Artist,
                        ChartName = chart.Metadata.ChartName,
                        Level = chart.Metadata.Level,
                        PreviewTime = chart.PreviewTime,
                        AudioFile = chart.Metadata.AudioFile,
                    };
                }
                catch (Exception ex)
                {
                    Logger.Log($"Skipping unreadable bundled chart \"{name}\": {ex.Message}", level: LogLevel.Important);
                }

                if (card != null)
                    yield return card;
            }
        }

        public GarbusChart LoadChart(ChartCard card)
        {
            var chart = charts.Get(card.Locator);
            chart.ApplyDefaults();
            return chart;
        }

        public Track GetTrack(ChartCard card, AudioManager audio) => trackStore.Get(card.AudioFile);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestResourceSourceEnumeratesAndGroups`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Screens/SongSelect/ResourceChartSource.cs Garbus.Game/Charts/ChartStore.cs Garbus.Game.Tests/Charts/TestChartLibrary.cs
git commit -m "feat: song-select resource chart source

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: DirectoryChartSource

**Files:**
- Create: `Garbus.Game/Screens/SongSelect/DirectoryChartSource.cs`
- Test: `Garbus.Game.Tests/Charts/TestChartLibrary.cs` (append)

**Interfaces:**
- Consumes: a root directory path (absolute), `AudioManager` (for track resolution only).
- Produces: `class DirectoryChartSource(string rootDirectory) : IChartSource, IDisposable`.
- Grouping rule: `GroupKey` = the file's containing directory (absolute path). `Locator` = the full
  `.garbus` path. Track store is built per-directory from `AudioManager` and cached; disposed with
  the source.

- [ ] **Step 1: Write the failing test (append to `TestChartLibrary.cs`)**

```csharp
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestDirectorySource`
Expected: FAIL to compile — `DirectoryChartSource` does not exist.

- [ ] **Step 3: Create `DirectoryChartSource.cs`**

```csharp
// Song-select source over the on-disk library folder (%APPDATA%/Garbus/charts). Recursively finds
// .garbus files; each folder is one song. Track audio sits beside each chart, resolved through a
// per-directory track store (cached, disposed with the source) exactly as ChartFile does.

using System;
using System.Collections.Generic;
using System.IO;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace Garbus.Game.Screens.SongSelect
{
    public class DirectoryChartSource : IChartSource, IDisposable
    {
        private readonly string rootDirectory;
        private readonly Dictionary<string, ITrackStore> trackStores = new Dictionary<string, ITrackStore>(StringComparer.OrdinalIgnoreCase);

        public DirectoryChartSource(string rootDirectory) => this.rootDirectory = rootDirectory;

        public IEnumerable<ChartCard> Enumerate()
        {
            if (!Directory.Exists(rootDirectory))
                yield break;

            foreach (string path in Directory.EnumerateFiles(rootDirectory, "*.garbus", SearchOption.AllDirectories))
            {
                ChartCard? card = null;
                try
                {
                    var chart = GarbusChartSerializer.Decode(File.ReadAllText(path));
                    card = new ChartCard
                    {
                        Source = this,
                        Locator = path,
                        GroupKey = Path.GetDirectoryName(path)!,
                        Title = chart.Metadata.Title,
                        Artist = chart.Metadata.Artist,
                        ChartName = chart.Metadata.ChartName,
                        Level = chart.Metadata.Level,
                        PreviewTime = chart.PreviewTime,
                        AudioFile = chart.Metadata.AudioFile,
                    };
                }
                catch (Exception ex)
                {
                    Logger.Log($"Skipping unreadable chart \"{path}\": {ex.Message}", level: LogLevel.Important);
                }

                if (card != null)
                    yield return card;
            }
        }

        public GarbusChart LoadChart(ChartCard card)
        {
            var chart = GarbusChartSerializer.Decode(File.ReadAllText(card.Locator));
            chart.ApplyDefaults();
            return chart;
        }

        public Track GetTrack(ChartCard card, AudioManager audio)
        {
            string dir = Path.GetDirectoryName(card.Locator)!;

            if (!trackStores.TryGetValue(dir, out var store))
            {
                store = audio.GetTrackStore(new StorageBackedResourceStore(new NativeStorage(dir)));
                trackStores[dir] = store;
            }

            return store.Get(card.AudioFile);
        }

        public void Dispose()
        {
            foreach (var store in trackStores.Values)
                (store as IDisposable)?.Dispose();
            trackStores.Clear();
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestDirectorySource`
Expected: PASS (2 tests).

- [ ] **Step 5: Run the full library test class**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestChartLibrary`
Expected: PASS (all 8 tests: 4 grouping + resource + 2 directory + resource-group).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Screens/SongSelect/DirectoryChartSource.cs Garbus.Game.Tests/Charts/TestChartLibrary.cs
git commit -m "feat: song-select directory chart source

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: SongSelectScreen (list, view toggle, preview, launch) + wiring

**Files:**
- Create: `Garbus.Game/Screens/SongSelect/ChartRow.cs`
- Create: `Garbus.Game/Screens/SongSelect/SongSelectScreen.cs`
- Modify: `Garbus.Game/Configuration/GarbusSetting.cs`
- Modify: `Garbus.Game/Configuration/GarbusConfigManager.cs`
- Modify: `Garbus.Game/Screens/MainMenuScreen.cs`
- Modify: `Garbus.Game/Screens/PlayScreen.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneSongSelect.cs`

**Interfaces:**
- Consumes: `ChartLibrary`, `ResourceChartSource`, `DirectoryChartSource`, `ChartCard`, `SongGroup`,
  `ChartStore`, `ITrackStore`, `AudioManager`, `Storage`, `GarbusConfigManager`, `GarbusSetting`,
  `PlayScreen(GarbusChart, Track, double)`.
- Produces: `class SongSelectScreen : Screen` exposing (for tests) `IReadOnlyList<SongGroup> Groups`,
  `ChartCard? SelectedChart`, `bool Grouped` (bound to config), `void Select(ChartCard card)`,
  `void Launch()`. `class ChartRow : ClickableContainer` with a `bool Selected` setter.

- [ ] **Step 1: Add the config setting**

Add to `Garbus.Game/Configuration/GarbusSetting.cs` (inside the enum, after `EditorLastFileDirectory`):

```csharp
        // --- Song select ---

        /// <summary>Whether song select groups charts by song (true) or lists every chart flat (false).</summary>
        SongSelectGrouped,
```

Add to `Garbus.Game/Configuration/GarbusConfigManager.cs` in `InitialiseDefaults()` (after the editor defaults):

```csharp
            // Song select.
            SetDefault(GarbusSetting.SongSelectGrouped, true);
```

- [ ] **Step 2: Reframe the PlayScreen constructor doc**

In `Garbus.Game/Screens/PlayScreen.cs`, replace the XML `<summary>` on the `PlayScreen(GarbusChart chart, Track track, double startTime = 0)` constructor:

Find:
```csharp
        /// Creates a <see cref="PlayScreen"/> pre-loaded with the given chart and track, starting
        /// at the specified time. Used by the editor's Test mode (F5 / Test button).
```
Replace with:
```csharp
        /// Creates a <see cref="PlayScreen"/> pre-loaded with the given chart and track, starting
        /// at the specified time. Used by song select (startTime 0) and the editor's Test mode
        /// (F5 / Test button, non-zero startTime).
```

- [ ] **Step 3: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneSongSelect.cs`:

```csharp
// Visual + integration test for song select: the library scans bundled charts, the view toggle
// flips grouping (and persists), selecting a chart drives the audio preview, and launching pushes a
// PlayScreen for the chosen chart.

using System.Linq;
using Garbus.Game.Configuration;
using Garbus.Game.Screens;
using Garbus.Game.Screens.SongSelect;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSongSelect : GarbusTestScene
    {
        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private ScreenStack stack = null!;
        private SongSelectScreen songSelect = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("push song select", () =>
                Child = stack = new ScreenStack(songSelect = new SongSelectScreen()) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("loaded", () => songSelect.IsLoaded && songSelect.Groups != null);
        }

        [Test]
        public void TestBundledChartAppears()
        {
            // The bundled test-chart.garbus must show up as at least one card.
            AddAssert("has at least one chart", () => songSelect.Groups.SelectMany(g => g.Charts).Any());
        }

        [Test]
        public void TestViewTogglePersists()
        {
            AddStep("set flat", () => songSelect.Grouped = false);
            AddAssert("config updated", () => config.Get<bool>(GarbusSetting.SongSelectGrouped) == false);
            AddStep("set grouped", () => songSelect.Grouped = true);
            AddAssert("config updated", () => config.Get<bool>(GarbusSetting.SongSelectGrouped) == true);
        }

        [Test]
        public void TestSelectThenLaunchPushesPlayScreen()
        {
            AddStep("select first chart", () => songSelect.Select(songSelect.Groups.SelectMany(g => g.Charts).First()));
            AddAssert("selection set", () => songSelect.SelectedChart != null);
            AddStep("launch", () => songSelect.Launch());
            AddUntilStep("play screen pushed", () => stack.CurrentScreen is PlayScreen);
            AddAssert("play screen has the selected chart", () =>
                ((PlayScreen)stack.CurrentScreen).Chart != null);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneSongSelect`
Expected: FAIL to compile — `SongSelectScreen` does not exist.

- [ ] **Step 5: Create `ChartRow.cs`**

```csharp
// A single clickable row in the song-select list: the chart's display name + level, highlighted
// when selected. Group headers reuse this with a bolder style.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Screens.SongSelect
{
    public partial class ChartRow : ClickableContainer
    {
        private readonly Box background;
        private bool selected;

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                background.FadeColour(value ? new Color4(70, 90, 140, 255) : new Color4(32, 32, 44, 255), 120, Easing.OutQuint);
            }
        }

        public ChartRow(string text, int? level, bool header = false)
        {
            RelativeSizeAxes = Axes.X;
            Height = header ? 34 : 30;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = header ? new Color4(24, 24, 32, 255) : new Color4(32, 32, 44, 255),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Padding = new MarginPadding { Left = header ? 12 : 28 },
                    Text = text,
                    Font = FontUsage.Default.With(size: header ? 22 : 18, weight: header ? "Bold" : null),
                    Colour = header ? Color4.White : new Color4(210, 210, 220, 255),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Padding = new MarginPadding { Right = 12 },
                    Text = level is > 0 ? $"Lv.{level}" : string.Empty,
                    Font = FontUsage.Default.With(size: 16),
                    Colour = new Color4(150, 160, 190, 255),
                },
            };
        }
    }
}
```

- [ ] **Step 6: Create `SongSelectScreen.cs`**

```csharp
// Bespoke song select (Phase 5). Scans bundled + AppData-folder charts into a grouped/flat list,
// loops an audio preview for the selected chart, and launches the chosen chart into PlayScreen.
// Replaces PlayScreen's hardcoded bundled-chart load on the main-menu Play path.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Screens.SongSelect
{
    public partial class SongSelectScreen : Screen
    {
        private const int preview_fade = 200;

        private ChartLibrary library = null!;
        private DirectoryChartSource directorySource = null!;
        private AudioManager audio = null!;

        private FillFlowContainer list = null!;
        private readonly Dictionary<ChartCard, ChartRow> rows = new Dictionary<ChartCard, ChartRow>();

        private readonly Bindable<bool> grouped = new Bindable<bool>(true);
        private Track? previewTrack;

        /// <summary>The scanned song groups (grouped-view model). Populated in load. Exposed for tests.</summary>
        public IReadOnlyList<SongGroup> Groups { get; private set; } = null!;

        /// <summary>The currently selected chart, or null before any selection. Exposed for tests.</summary>
        public ChartCard? SelectedChart { get; private set; }

        /// <summary>Whether the list is grouped by song (true) or flat by level (false).</summary>
        public bool Grouped
        {
            get => grouped.Value;
            set => grouped.Value = value;
        }

        [BackgroundDependencyLoader]
        private void load(Storage storage, ChartStore charts, ITrackStore resourceTracks, AudioManager audio, GarbusConfigManager config)
        {
            this.audio = audio;

            config.BindWith(GarbusSetting.SongSelectGrouped, grouped);

            var resourceSource = new ResourceChartSource(charts, resourceTracks);
            string songsRoot = storage.GetStorageForDirectory("charts").GetFullPath(string.Empty);
            directorySource = new DirectoryChartSource(songsRoot);
            library = new ChartLibrary(directorySource, resourceSource);
            Groups = library.Scan();

            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(18, 18, 26, 255) },
                new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 56, Bottom = 12, Horizontal = 40 },
                    Child = list = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 3),
                    },
                },
                new SpriteText
                {
                    Padding = new MarginPadding { Top = 16, Left = 40 },
                    Text = "Select a chart",
                    Font = FontUsage.Default.With(size: 28),
                },
                new BasicButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 14, Right = 40 },
                    Size = new Vector2(160, 30),
                    Text = "View: …",
                    Action = () => grouped.Value = !grouped.Value,
                }.With(b => viewButton = b),
            };
        }

        private BasicButton viewButton = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            grouped.BindValueChanged(_ => rebuild(), true);
        }

        private void rebuild()
        {
            viewButton.Text = grouped.Value ? "View: Grouped" : "View: Flat";
            list.Clear();
            rows.Clear();

            if (grouped.Value)
            {
                foreach (var group in Groups)
                {
                    list.Add(new ChartRow($"{group.Title} — {group.Artist}", null, header: true));
                    foreach (var card in group.Charts)
                        addRow(card);
                }
            }
            else
            {
                foreach (var card in library.AllCharts())
                    addRow(card);
            }

            // Re-apply highlight after a rebuild.
            if (SelectedChart != null && rows.TryGetValue(SelectedChart, out var row))
                row.Selected = true;
        }

        private void addRow(ChartCard card)
        {
            var row = new ChartRow(card.DisplayName, card.Level) { Action = () => Select(card) };
            rows[card] = row;
            list.Add(row);
        }

        /// <summary>Selects a chart, highlighting its row and starting a looping audio preview.</summary>
        public void Select(ChartCard card)
        {
            if (SelectedChart != null && rows.TryGetValue(SelectedChart, out var previous))
                previous.Selected = false;

            SelectedChart = card;

            if (rows.TryGetValue(card, out var row))
                row.Selected = true;

            startPreview(card);
        }

        private void startPreview(ChartCard card)
        {
            stopPreview();

            try
            {
                var track = card.GetTrack(audio);
                if (track == null)
                    return;

                previewTrack = track;
                track.Looping = true;
                track.Seek(card.PreviewTime ?? 0);
                track.Volume.Value = 0;
                track.Start();
                track.VolumeTo(1, preview_fade);
            }
            catch (Exception)
            {
                // A missing/undecodable audio file just means no preview — selection still works.
                previewTrack = null;
            }
        }

        private void stopPreview()
        {
            var track = previewTrack;
            previewTrack = null;
            if (track == null)
                return;

            track.VolumeTo(0, preview_fade).OnComplete(_ =>
            {
                track.Stop();
                track.Dispose();
            });
        }

        /// <summary>Loads the selected chart + a fresh gameplay track and pushes the play screen.</summary>
        public void Launch()
        {
            if (SelectedChart == null)
                return;

            stopPreview();

            var chart = SelectedChart.LoadChart();
            var track = SelectedChart.GetTrack(audio);
            this.Push(new PlayScreen(chart, track));
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat)
                return base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.Enter:
                case Key.KeypadEnter:
                    Launch();
                    return true;

                case Key.Escape:
                    this.Exit();
                    return true;
            }

            return base.OnKeyDown(e);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            // Returning from play: rescan (new charts may exist) and resume the preview.
            Groups = library.Scan();
            rebuild();
            if (SelectedChart != null)
                startPreview(SelectedChart);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            stopPreview();
            return base.OnExiting(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            stopPreview();
            directorySource?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
```

- [ ] **Step 7: Wire the main menu**

In `Garbus.Game/Screens/MainMenuScreen.cs`, add `using Garbus.Game.Screens.SongSelect;` at the top, then change `onPlay`:

Find:
```csharp
        private void onPlay()
        {
            this.Push(new PlayScreen());
        }
```
Replace with:
```csharp
        private void onPlay()
        {
            this.Push(new SongSelectScreen());
        }
```

- [ ] **Step 8: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors. (Fix any `VolumeTo`/`Seek`/`GetFullPath` signature mismatches
against the framework here — e.g. if `VolumeTo` returns a `TransformSequence` whose `OnComplete`
signature differs, adjust to `.Then(...)` per the installed framework version.)

- [ ] **Step 9: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneSongSelect`
Expected: PASS (3 tests).

- [ ] **Step 10: Run the full suite (no regressions)**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (all existing tests + the new ones).

- [ ] **Step 11: Commit**

```bash
git add Garbus.Game/Screens/SongSelect Garbus.Game/Configuration Garbus.Game/Screens/MainMenuScreen.cs Garbus.Game/Screens/PlayScreen.cs Garbus.Game.Tests/Visual/TestSceneSongSelect.cs
git commit -m "feat: song select screen with grouped/flat toggle, preview, and launch

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Manual verification + plan tracker update

**Files:**
- Modify: `PLAN-port.md`

- [ ] **Step 1: Run the app and exercise song select**

Run: `dotnet run --project Garbus.Desktop`
Verify manually:
- Main menu "Play" opens song select; the bundled test chart is listed.
- The view button toggles Grouped ↔ Flat and the list re-sorts (title vs level).
- Selecting a chart starts an audible looping preview; changing selection swaps it.
- Enter (or clicking then Enter) launches gameplay with the selected chart; Escape returns to menu.
- Drop a folder with a `.garbus` + its `.ogg` into `%APPDATA%\Garbus\charts\<name>\`, return to menu,
  re-open song select → the chart appears.

Check `%APPDATA%\Garbus\logs\*.runtime.log` for errors after the session.

- [ ] **Step 2: Update the Phase 5 checklist in `PLAN-port.md`**

In the `### Phase 5 — game chrome` section, mark song select done and note the deferrals:

```markdown
### Phase 5 — game chrome

- [x] Song select (bespoke) — `Screens/SongSelect/`: unions bundled resource charts + an AppData
      `charts/` folder (scanned), grouped by folder into `SongGroup`s; a view toggle switches
      grouped-by-title ↔ flat-by-level (persisted in `GarbusSetting.SongSelectGrouped`); looping
      audio preview on selection; launches the existing `PlayScreen(chart, track)`. Deferred:
      background rendering, search box, configurable songs dir, editor default-save into the library.
- [ ] Settings screen: audio device, volumes, offset calibration (port osu's suggested-offset idea),
      key bindings
- [ ] Results screen
```

- [ ] **Step 3: Commit**

```bash
git add PLAN-port.md
git commit -m "docs: mark song select complete in port plan

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review Notes (verify during execution)

- **Spec coverage:** two sources unioned (Tasks 2–3), folder grouping (Task 1), view toggle drives
  layout+sort with persisted setting (Task 4), looping preview from `PreviewTime` (Task 4), launch via
  existing `PlayScreen` ctor (Task 4), main-menu wiring (Task 4). Deferrals recorded (Task 5).
- **Framework signatures — verified against the vendored clone** (`BAC/LocalDependencies/osu-framework`):
  `VolumeTo(newVolume, duration, Easing)` returns `TransformSequence<T>` (extension in
  `osu.Framework.Audio`, already imported) and `TransformSequence<T>.OnComplete(Action<T>)` exists —
  so `track.VolumeTo(0, preview_fade).OnComplete(_ => { track.Stop(); track.Dispose(); })` compiles.
  `Storage.GetFullPath(string, bool = false)` — `GetFullPath(string.Empty)` is valid.
  `AudioManager.GetTrackStore(IResourceStore<byte[]>)` is the path `ChartFile` already uses.
  Still confirm `Track.Seek/Looping/Volume` and `BasicScrollContainer`/`BasicButton` at Task 4 Step 8;
  if anything mismatches the installed package, adjust inline (fallback: `track.Stop()` right after the
  fade start instead of in `OnComplete`).
- **Type consistency:** `IChartSource.GetTrack(ChartCard, AudioManager)` used identically in both
  sources and `ChartCard.GetTrack(AudioManager)`; `ChartLibrary.Scan()`/`AllCharts()` names match
  their test call sites; `GarbusSetting.SongSelectGrouped` name matches the config default and the
  screen's `BindWith`.
```
