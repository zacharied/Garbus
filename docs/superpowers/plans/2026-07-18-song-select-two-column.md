# Two-Column Song Select Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split `SongSelectScreen` into a two-column layout — the existing chart list on the left, and a new detail panel (image, title, artist, chart/level, play button) for the selected chart on the right.

**Architecture:** Wrap the current left-hand list contents in the left column of a two-column `GridContainer`; add a new `ChartDetailPanel` drawable in the right column. Expose the chart's background image (already present as `ChartMetadata.BackgroundFile` but never loaded) by adding `BackgroundFile` to `ChartCard` and a `GetBackground(card)` method to `IChartSource`, mirroring the existing `GetTrack`.

**Tech Stack:** C#, osu-framework (`GridContainer`, `Sprite`, `TextureStore`, `BasicButton`), NUnit visual test scenes.

## Global Constraints

- Nullability is enabled solution-wide. DI-resolved / BDL-initialised fields use `= null!`.
- The play button text is the literal string `Press X to play!`. **No new `X` key binding** — clicking the button (or the existing `Enter` key) launches.
- When a chart has no background file (or the image fails to load), the image square shows a neutral **placeholder** square, never a collapsed/empty area.
- Terminology: osu's "beatmap" is "chart" here.
- Do not add compatibility shims or version bumps; deviate from vendored osu minimally.

---

### Task 1: Expose the chart background through the source layer

Add `BackgroundFile` to `ChartCard`, populate it in both sources, and add `Texture? GetBackground(ChartCard)` to `IChartSource`. New texture dependencies are **optional** (nullable, default null) so existing test constructors keep compiling and `GetBackground` returns null (→ placeholder) when unavailable.

**Files:**
- Modify: `Garbus.Game/Screens/SongSelect/ChartCard.cs`
- Modify: `Garbus.Game/Screens/SongSelect/IChartSource.cs`
- Modify: `Garbus.Game/Screens/SongSelect/ResourceChartSource.cs`
- Modify: `Garbus.Game/Screens/SongSelect/DirectoryChartSource.cs`
- Modify (stub): `Garbus.Game.Tests/Charts/TestChartLibrary.cs` (add `GetBackground` to `StubSource`)
- Test: `Garbus.Game.Tests/Charts/TestChartLibrary.cs`

**Interfaces:**
- Produces:
  - `ChartCard.BackgroundFile` → `string` (init-only, default `""`).
  - `IChartSource.GetBackground(ChartCard card)` → `osu.Framework.Graphics.Textures.Texture?`.
  - `ResourceChartSource(ChartStore charts, ITrackStore trackStore, TextureStore? textures = null)`.
  - `DirectoryChartSource(string rootDirectory, GameHost? host = null)`.

- [ ] **Step 1: Write the failing tests**

Add these two tests to the `TestChartLibrary` class in `Garbus.Game.Tests/Charts/TestChartLibrary.cs` (place them after `TestDirectorySourceEmptyWhenRootMissing`). The first proves `BackgroundFile` is decoded onto the card; the second proves `GetBackground` returns null when no host/texture backing exists (the placeholder path).

```csharp
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
```

Also add the missing `using` at the top of the file if not present:

```csharp
using osu.Framework.Graphics.Textures;
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChartLibrary"`
Expected: FAIL to **compile** — `ChartCard` has no `BackgroundFile`, `IChartSource` has no `GetBackground`, `StubSource` doesn't implement it. (Compilation failure is the expected "red" here.)

- [ ] **Step 3: Add `BackgroundFile` to `ChartCard`**

In `Garbus.Game/Screens/SongSelect/ChartCard.cs`, add the property alongside `AudioFile`:

```csharp
        public string AudioFile { get; init; } = string.Empty;

        /// <summary>The background image beside the chart (full filename); empty when none.</summary>
        public string BackgroundFile { get; init; } = string.Empty;
```

- [ ] **Step 4: Add `GetBackground` to `IChartSource`**

In `Garbus.Game/Screens/SongSelect/IChartSource.cs`, add the using and the method:

```csharp
using osu.Framework.Graphics.Textures;
```

```csharp
        /// <summary>A fresh <see cref="Track"/> for the card's audio. Caller owns/disposes it.</summary>
        Track GetTrack(ChartCard card, AudioManager audio);

        /// <summary>The card's background image, or null when it has none / can't be loaded (→ placeholder).</summary>
        Texture? GetBackground(ChartCard card);
```

- [ ] **Step 5: Implement in `ResourceChartSource`**

In `Garbus.Game/Screens/SongSelect/ResourceChartSource.cs`:

Add usings:

```csharp
using osu.Framework.Graphics.Textures;
```

Change the constructor to accept an optional `TextureStore` and store it:

```csharp
        private readonly ChartStore charts;
        private readonly ITrackStore trackStore;
        private readonly TextureStore? textures;

        public ResourceChartSource(ChartStore charts, ITrackStore trackStore, TextureStore? textures = null)
        {
            this.charts = charts;
            this.trackStore = trackStore;
            this.textures = textures;
        }
```

Populate `BackgroundFile` in `Enumerate` (add the line alongside `AudioFile`):

```csharp
                        AudioFile = chart.Metadata.AudioFile,
                        BackgroundFile = chart.Metadata.BackgroundFile,
```

Add the method (after `GetTrack`):

```csharp
        public Texture? GetBackground(ChartCard card)
        {
            if (textures == null || string.IsNullOrEmpty(card.BackgroundFile))
                return null;

            return textures.Get(card.BackgroundFile);
        }
```

- [ ] **Step 6: Implement in `DirectoryChartSource`**

In `Garbus.Game/Screens/SongSelect/DirectoryChartSource.cs`:

Add usings:

```csharp
using osu.Framework.Graphics.Textures;
```

Add a `GameHost?` field + per-directory texture-store cache, and accept the host in the constructor (parallel to the existing `trackStores`):

```csharp
        private readonly string rootDirectory;
        private readonly GameHost? host;
        private readonly Dictionary<string, ITrackStore> trackStores = new Dictionary<string, ITrackStore>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TextureStore> textureStores = new Dictionary<string, TextureStore>(StringComparer.OrdinalIgnoreCase);

        public DirectoryChartSource(string rootDirectory, GameHost? host = null)
        {
            this.rootDirectory = rootDirectory;
            this.host = host;
        }
```

Populate `BackgroundFile` in `Enumerate` (alongside `AudioFile`):

```csharp
                        AudioFile = chart.Metadata.AudioFile,
                        BackgroundFile = chart.Metadata.BackgroundFile,
```

Add `GetBackground` (after `GetTrack`), building a per-directory `TextureStore` exactly like the track-store path:

```csharp
        public Texture? GetBackground(ChartCard card)
        {
            if (host == null || string.IsNullOrEmpty(card.BackgroundFile))
                return null;

            string dir = Path.GetDirectoryName(card.Locator)!;

            if (!textureStores.TryGetValue(dir, out var store))
            {
                store = new TextureStore(host.Renderer, host.CreateTextureLoaderStore(new StorageBackedResourceStore(new NativeStorage(dir))));
                textureStores[dir] = store;
            }

            return store.Get(card.BackgroundFile);
        }
```

Dispose the texture stores in `Dispose` (add alongside the track-store loop):

```csharp
        public void Dispose()
        {
            foreach (var store in trackStores.Values)
                (store as IDisposable)?.Dispose();
            trackStores.Clear();

            foreach (var store in textureStores.Values)
                store.Dispose();
            textureStores.Clear();
        }
```

- [ ] **Step 7: Add `GetBackground` to the test `StubSource`**

In `Garbus.Game.Tests/Charts/TestChartLibrary.cs`, update `StubSource` to satisfy the interface:

```csharp
        private class StubSource : IChartSource
        {
            private readonly IEnumerable<ChartCard> cards;
            public StubSource(params ChartCard[] cards) => this.cards = cards;
            public IEnumerable<ChartCard> Enumerate() => cards;
            public GarbusChart LoadChart(ChartCard card) => new GarbusChart();
            public Track GetTrack(ChartCard card, AudioManager audio) => null!;
            public Texture? GetBackground(ChartCard card) => null;
        }
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChartLibrary"`
Expected: PASS (all `TestChartLibrary` tests green, including the two new ones).

- [ ] **Step 9: Commit**

```bash
git add Garbus.Game/Screens/SongSelect/ChartCard.cs Garbus.Game/Screens/SongSelect/IChartSource.cs Garbus.Game/Screens/SongSelect/ResourceChartSource.cs Garbus.Game/Screens/SongSelect/DirectoryChartSource.cs Garbus.Game.Tests/Charts/TestChartLibrary.cs
git commit -m "feat: expose chart background image through song-select sources"
```

---

### Task 2: `ChartDetailPanel` component

A self-contained right-column panel that displays a card's image/title/artist/chart-level and a play button. It owns no selection or launch logic — it invokes a supplied callback.

**Files:**
- Create: `Garbus.Game/Screens/SongSelect/ChartDetailPanel.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneChartDetailPanel.cs`

**Interfaces:**
- Consumes: `ChartCard` (Task existing) incl. `Title`/`Artist`/`ChartName`/`Level`; `Texture?` (from `IChartSource.GetBackground`, Task 1).
- Produces:
  - `ChartDetailPanel.Show(ChartCard? card, Texture? background)` → `void`.
  - `ChartDetailPanel.LaunchRequested` → `Action?` (settable; invoked on button click).
  - `ChartDetailPanel.DisplayedCard` → `ChartCard?` (test hook).
  - `ChartDetailPanel.HasBackground` → `bool` (test hook; true only when a non-null texture was shown).

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneChartDetailPanel.cs`:

```csharp
// Visual + behaviour test for the song-select detail panel: showing a card populates its fields and
// reports no background for a textureless card; the empty state clears the displayed card.

using System.Linq;
using Garbus.Game.Screens.SongSelect;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneChartDetailPanel : GarbusTestScene
    {
        private ChartDetailPanel panel = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create panel", () => Child = panel = new ChartDetailPanel { RelativeSizeAxes = Axes.Both });
        }

        [Test]
        public void TestShowCardPopulatesFields()
        {
            var card = new ChartCard { Source = null!, Locator = "l", GroupKey = "g", Title = "My Song", Artist = "My Artist", ChartName = "Insane", Level = 7 };

            AddStep("show card", () => panel.Show(card, null));
            AddAssert("displayed card set", () => panel.DisplayedCard == card);
            AddAssert("no background (placeholder)", () => !panel.HasBackground);
            AddAssert("title rendered", () => this.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == "My Song"));
            AddAssert("artist rendered", () => this.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == "My Artist"));
        }

        [Test]
        public void TestEmptyStateClearsCard()
        {
            var card = new ChartCard { Source = null!, Locator = "l", GroupKey = "g", Title = "My Song", Artist = "My Artist", Level = 1 };

            AddStep("show card", () => panel.Show(card, null));
            AddStep("show empty", () => panel.Show(null, null));
            AddAssert("no displayed card", () => panel.DisplayedCard == null);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneChartDetailPanel"`
Expected: FAIL to compile — `ChartDetailPanel` does not exist.

- [ ] **Step 3: Create `ChartDetailPanel`**

Create `Garbus.Game/Screens/SongSelect/ChartDetailPanel.cs`:

```csharp
// The right-hand song-select detail panel: the selected chart's background image (or a placeholder
// square), title, artist, chart name + level, and a "Press X to play!" button. Display-only — it
// invokes LaunchRequested on click and holds no selection/launch logic of its own.

using System;
using Garbus.Game.Charts;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Screens.SongSelect
{
    public partial class ChartDetailPanel : CompositeDrawable
    {
        private const float image_size = 300;

        private readonly Box placeholder;
        private readonly Sprite image;
        private readonly SpriteText titleText;
        private readonly SpriteText artistText;
        private readonly SpriteText chartInfoText;
        private readonly BasicButton playButton;

        /// <summary>The currently displayed card, or null in the empty state. Exposed for tests.</summary>
        public ChartCard? DisplayedCard { get; private set; }

        /// <summary>Whether a non-null background texture is currently shown (vs the placeholder). Test hook.</summary>
        public bool HasBackground { get; private set; }

        /// <summary>Invoked when the play button is clicked. Set by the owning screen.</summary>
        public Action? LaunchRequested { get; set; }

        public ChartDetailPanel()
        {
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(24, 24, 34, 255) },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Padding = new MarginPadding { Top = 56, Horizontal = 24 },
                    Spacing = new Vector2(0, 14),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Size = new Vector2(image_size),
                            Masking = true,
                            CornerRadius = 6,
                            Children = new Drawable[]
                            {
                                placeholder = new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(45, 45, 60, 255) },
                                image = new Sprite
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    FillMode = FillMode.Fill,
                                    Alpha = 0,
                                },
                            },
                        },
                        titleText = new SpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = FontUsage.Default.With(size: 30, weight: "Bold"),
                            Colour = Color4.White,
                        },
                        artistText = new SpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = FontUsage.Default.With(size: 20),
                            Colour = new Color4(190, 190, 205, 255),
                        },
                        chartInfoText = new SpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = FontUsage.Default.With(size: 18),
                            Colour = new Color4(150, 160, 190, 255),
                        },
                        playButton = new BasicButton
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Size = new Vector2(image_size, 72),
                            Text = "Press X to play!",
                            BackgroundColour = new Color4(70, 90, 140, 255),
                            Action = () => LaunchRequested?.Invoke(),
                        },
                    },
                },
            };

            Show(null, null);
        }

        /// <summary>Populates every field for <paramref name="card"/> (empty state when null).</summary>
        public void Show(ChartCard? card, Texture? background)
        {
            DisplayedCard = card;

            if (card == null)
            {
                titleText.Text = "Select a chart";
                artistText.Text = string.Empty;
                chartInfoText.Text = string.Empty;
                playButton.Enabled.Value = false;
            }
            else
            {
                titleText.Text = card.Title;
                artistText.Text = card.Artist;
                chartInfoText.Text = formatChartInfo(card);
                playButton.Enabled.Value = true;
            }

            HasBackground = background != null;
            image.Texture = background;
            image.Alpha = background != null ? 1 : 0;
            placeholder.Alpha = background != null ? 0 : 1;
        }

        // "{ChartName} · Lv.{Level}", omitting whichever piece is absent.
        private static string formatChartInfo(ChartCard card)
        {
            bool hasName = !string.IsNullOrEmpty(card.ChartName);
            bool hasLevel = card.Level > 0;

            if (hasName && hasLevel)
                return $"{card.ChartName} · Lv.{card.Level}";
            if (hasName)
                return card.ChartName;
            if (hasLevel)
                return $"Lv.{card.Level}";

            return string.Empty;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneChartDetailPanel"`
Expected: PASS (both tests green).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Screens/SongSelect/ChartDetailPanel.cs Garbus.Game.Tests/Visual/TestSceneChartDetailPanel.cs
git commit -m "feat: add song-select chart detail panel"
```

---

### Task 3: Two-column layout + wiring in `SongSelectScreen`

Move the existing list/title/toggle into the left column of a two-column `GridContainer`, add the `ChartDetailPanel` in the right column, pass the texture dependencies to the sources, and refresh the panel on selection.

**Files:**
- Modify: `Garbus.Game/Screens/SongSelect/SongSelectScreen.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneSongSelect.cs`

**Interfaces:**
- Consumes: `ChartDetailPanel.Show` / `.LaunchRequested` / `.DisplayedCard` / `.HasBackground` (Task 2); `IChartSource.GetBackground`, updated source constructors (Task 1).

- [ ] **Step 1: Write the failing tests**

Add these tests to `TestSceneSongSelect` in `Garbus.Game.Tests/Visual/TestSceneSongSelect.cs` (after `TestArrowKeysMoveSelection`). Add `using osu.Framework.Graphics.UserInterface;` to the file's usings if not present.

```csharp
[Test]
public void TestSelectPopulatesDetailPanel()
{
    ChartCard? first = null;

    AddStep("select first chart", () =>
    {
        first = songSelect.Groups.SelectMany(g => g.Charts).First();
        songSelect.Select(first);
    });

    AddAssert("panel shows selected card", () =>
        this.ChildrenOfType<ChartDetailPanel>().Single().DisplayedCard == first);

    // The bundled chart has no background file → the placeholder path.
    AddAssert("panel shows placeholder (no background)", () =>
        !this.ChildrenOfType<ChartDetailPanel>().Single().HasBackground);
}

[Test]
public void TestPlayButtonLaunches()
{
    AddStep("select first chart", () => songSelect.Select(songSelect.Groups.SelectMany(g => g.Charts).First()));

    AddStep("click play button", () =>
    {
        var button = this.ChildrenOfType<BasicButton>().Single(b => b.Text.ToString() == "Press X to play!");
        input.MoveMouseTo(button);
        input.Click(osuTK.Input.MouseButton.Left);
    });

    AddUntilStep("play screen pushed", () => stack.CurrentScreen is PlayScreen);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneSongSelect"`
Expected: FAIL — no `ChartDetailPanel` exists in the screen's tree (assertions/`Single()` throw), and the play button isn't present.

- [ ] **Step 3: Add the detail-panel field and resolve texture deps in `load`**

In `Garbus.Game/Screens/SongSelect/SongSelectScreen.cs`, add usings:

```csharp
using osu.Framework.Graphics.Textures;
```

Add a field alongside `viewButton`:

```csharp
        private ChartDetailPanel detailPanel = null!;
```

Change the `load` signature to resolve `GameHost` and `TextureStore`, and pass them into the sources. Replace the existing signature line and the source-construction lines:

```csharp
        [BackgroundDependencyLoader]
        private void load(Storage storage, ChartStore charts, ITrackStore resourceTracks, AudioManager audio, GarbusConfigManager config, GameHost host, TextureStore textures)
        {
            this.audio = audio;

            config.BindWith(GarbusSetting.SongSelectGrouped, grouped);

            var resourceSource = new ResourceChartSource(charts, resourceTracks, textures);
            string songsRoot = storage.GetStorageForDirectory("charts").GetFullPath(string.Empty);
            directorySource = new DirectoryChartSource(songsRoot, host);
            library = new ChartLibrary(directorySource, resourceSource);
            Groups = library.Scan();
```

(`GameHost` is already imported via `osu.Framework.Platform`; `TextureStore` needs the new using above.)

- [ ] **Step 4: Replace the `InternalChildren` layout with two columns**

Still in `load`, replace the entire `InternalChildren = new Drawable[] { ... };` block with a background box plus a two-column grid. The left column holds a `Container` with the scroll list, title, and view button (moved verbatim); the right column holds the panel:

```csharp
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(18, 18, 26, 255) },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 380),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
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
                                        Padding = new MarginPadding { Top = 16, Left = 64 },
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
                                },
                            },
                            detailPanel = new ChartDetailPanel
                            {
                                RelativeSizeAxes = Axes.Both,
                                LaunchRequested = Launch,
                            },
                        },
                    },
                },
            };
        }
```

Note: `GridContainer` / `Dimension` / `GridSizeMode` come from `osu.Framework.Graphics.Containers`, already imported.

- [ ] **Step 5: Refresh the panel on selection**

In `Select`, after the row-highlight block and before `startPreview(card)`, add the panel refresh:

```csharp
        public void Select(ChartCard card)
        {
            if (SelectedChart != null && rows.TryGetValue(SelectedChart, out var previous))
                previous.Selected = false;

            SelectedChart = card;

            if (rows.TryGetValue(card, out var row))
                row.Selected = true;

            detailPanel.Show(card, card.Source.GetBackground(card));

            startPreview(card);
        }
```

- [ ] **Step 6: Refresh the panel on resume**

In `OnResuming`, after the `rebuild();` call, refresh the panel for the (possibly re-resolved or cleared) selection:

```csharp
            rebuild();
            detailPanel.Show(SelectedChart, SelectedChart == null ? null : SelectedChart.Source.GetBackground(SelectedChart));
            if (SelectedChart != null)
                startPreview(SelectedChart);
```

- [ ] **Step 7: Run the song-select tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneSongSelect"`
Expected: PASS (all existing tests still green — the moved list/title/toggle behave identically — plus the two new ones).

- [ ] **Step 8: Run the full test suite + build**

Run: `dotnet build Garbus.Desktop.slnf` then `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: build succeeds; all tests green (confirms Task 1's source-constructor changes didn't break `TestChartLibrary` and the layout change didn't regress `TestSceneEditor*`/others).

- [ ] **Step 9: Commit**

```bash
git add Garbus.Game/Screens/SongSelect/SongSelectScreen.cs Garbus.Game.Tests/Visual/TestSceneSongSelect.cs
git commit -m "feat: two-column song select with chart detail panel"
```

---

## Self-Review Notes

- **Spec coverage:** image square (Task 2 image/placeholder) ✓; title/artist/chart-name+level (Task 2 fields) ✓; "Press X to play!" square button, no X key (Task 2 button, literal text, click→`LaunchRequested`) ✓; two-column layout (Task 3 grid) ✓; background loading via source (Task 1 `GetBackground`) ✓; placeholder fallback (Task 1 returns null → Task 2 placeholder) ✓; reactive refresh on select/arrow/resume (Task 3 `Select`/`OnResuming`; arrows funnel through `Select`) ✓; tests (all three tasks) ✓.
- **Type consistency:** `GetBackground(ChartCard) : Texture?`, `Show(ChartCard?, Texture?)`, `LaunchRequested : Action?`, `DisplayedCard : ChartCard?`, `HasBackground : bool` used identically across tasks.
- **Backward-compat of constructors:** new params are optional (`TextureStore? textures = null`, `GameHost? host = null`); existing `new ResourceChartSource(store, null!)` and `new DirectoryChartSource(root)` call sites keep compiling. `StubSource` updated for the new interface member.
