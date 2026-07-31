# Jacket Background Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the song's jacket artwork during gameplay — circle-clipped at 20% brightness under the playfield ring, and dissolved into a blurred color wash filling the screen behind it.

**Architecture:** A new `JacketBackground` drawable owns both layers (a cached-framebuffer downscale+blur wash and a circle-masked jacket disc) and receives a `Texture?` — null means it adds no layers. `PlayScreen` hosts it between its dark base box and the gameplay-clock subtree, and gains an optional `jacket` constructor parameter fed by song select (`IChartSource.GetBackground`) and the editor's F5 test mode (a new `SongFile.GetJacketTexture`).

**Tech Stack:** C# / osu-framework 2026.629.0 (`BufferedContainer`, `CircularContainer`, `Sprite`, `LargeTextureStore`), NUnit test scenes.

**Spec:** `docs/superpowers/specs/2026-07-30-jacket-background-design.md`

## Global Constraints

- Build and test output stays **warning-clean** (AGENTS.md rule; includes tests).
- Test expectations are **spec-anchored and independently derived**; no bare styling pins (colours/alphas/offsets) — assert relations instead.
- Tests never rely on real song content (charts, audio, jackets) — generate fixtures.
- Tuning scenes are `[Explicit]` — no exceptions.
- Do not run the app to verify — tests only.
- Commit messages end with:
  `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` and
  `Claude-Session: https://claude.ai/code/session_01Tyevmd8w6zKqXE4qJBUL7n`
- Run tests with: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "<TestName>"` (omit filter for full suite).

---

### Task 1: `JacketBackground` component + shared padding constant

**Files:**
- Modify: `Garbus.Game/UI/GarbusPlayfield.cs` (extract `SCREEN_PADDING`)
- Create: `Garbus.Game/UI/JacketBackground.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneJacketBackground.cs`

**Interfaces:**
- Produces: `GarbusPlayfield.SCREEN_PADDING` (`public const float`, value 30).
- Produces: `public partial class JacketBackground : CompositeDrawable` with constructor `JacketBackground(Texture? jacket)` and init properties `float DiscBrightness` (default 0.2f), `float WashBrightness` (default 0.55f), `Vector2 WashBlurSigma` (default `new Vector2(5)`), `float WashFrameBufferScale` (default 0.05f). Null texture → no internal children.

- [ ] **Step 1: Write the failing tests**

Create `Garbus.Game.Tests/Visual/TestSceneJacketBackground.cs`:

```csharp
// Headless pins for the gameplay jacket background (spec:
// docs/superpowers/specs/2026-07-30-jacket-background-design.md): the jacket disc is inscribed in
// the same padded area as the judgement ring (alignment relation, not a styling pin), and a null
// jacket produces no layers at all.

using System;
using System.Linq;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    public partial class TestSceneJacketBackground : GarbusTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private JacketBackground background = null!;
        private GarbusPlayfield playfield = null!;

        [Test]
        public void TestDiscAlignsWithRingCircle()
        {
            // Host the background and a playfield in the same non-square area, as PlayScreen does.
            AddStep("create background and playfield", () => Child = new Container
            {
                Size = new Vector2(800, 600),
                Children = new Drawable[]
                {
                    background = new JacketBackground(renderer.WhitePixel),
                    playfield = new GarbusPlayfield(interactive: false),
                },
            });
            AddUntilStep("loaded", () => background.IsLoaded && playfield.IsLoaded);

            // The ring's Arc inscribes its circle in min(width, height) of the playfield's padded
            // area; the disc must match it exactly. Calibration anchor (hand-derived): with the
            // playfield padding of 30 on every side, min(800 − 60, 600 − 60) = 540.
            AddAssert("disc diameter equals ring diameter", () =>
            {
                var disc = background.ChildrenOfType<CircularContainer>().Single();
                var ring = playfield.ChildrenOfType<Ring>().Single();
                float ringDiameter = Math.Min(ring.DrawSize.X, ring.DrawSize.Y);
                return Precision.AlmostEquals(disc.DrawSize.X, ringDiameter, 0.5f)
                       && Precision.AlmostEquals(disc.DrawSize.Y, ringDiameter, 0.5f);
            });
        }

        [Test]
        public void TestNullJacketAddsNoLayers()
        {
            AddStep("create with null jacket", () => Child = background = new JacketBackground(null)
            {
                RelativeSizeAxes = Axes.Both,
            });
            AddUntilStep("loaded", () => background.IsLoaded);
            AddAssert("no sprite layers", () => !background.ChildrenOfType<Sprite>().Any());
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneJacketBackground"`
Expected: FAIL — `JacketBackground` does not exist (compile error is the failure mode here; that counts).

- [ ] **Step 3: Extract the shared padding constant**

In `Garbus.Game/UI/GarbusPlayfield.cs`, add to the class and use in the constructor:

```csharp
/// <summary>
/// The screen-edge padding inside which the playfield circle is inscribed. Shared with
/// <see cref="JacketBackground"/> so the background's jacket disc aligns with the judgement ring.
/// </summary>
public const float SCREEN_PADDING = 30;
```

and change `Padding = new MarginPadding(30);` to `Padding = new MarginPadding(SCREEN_PADDING);`.

- [ ] **Step 4: Implement `JacketBackground`**

Create `Garbus.Game/UI/JacketBackground.cs`:

```csharp
// The gameplay jacket background (spec: docs/superpowers/specs/2026-07-30-jacket-background-design.md).
// Two static layers under the playfield: a screen-filling color wash — the jacket dissolved by a
// one-shot cached downscale+blur framebuffer — and the un-blurred jacket circle-clipped to the
// judgement ring's disc. Receives its texture; performs no store lookups. Null texture → no layers
// (the screen's flat base box shows through).

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace Garbus.Game.UI;

public partial class JacketBackground : CompositeDrawable
{
    /// <summary>Brightness of the circle-clipped jacket disc (spec: 80% dim).</summary>
    public float DiscBrightness { get; init; } = 0.2f;

    /// <summary>Brightness of the blurred wash outside the ring (spec starting point).</summary>
    public float WashBrightness { get; init; } = 0.55f;

    /// <summary>Gaussian sigma applied in the wash framebuffer's (downscaled) pixel space.</summary>
    public Vector2 WashBlurSigma { get; init; } = new Vector2(5);

    /// <summary>Wash framebuffer scale — the downscale factor that dissolves the jacket into colors.</summary>
    public float WashFrameBufferScale { get; init; } = 0.05f;

    private readonly Texture? jacket;

    public JacketBackground(Texture? jacket)
    {
        this.jacket = jacket;
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        if (jacket == null)
            return;

        InternalChildren = new Drawable[]
        {
            // Wash: rendered once into a small cached framebuffer and blurred there, then reused
            // every frame. RedrawOnScale off — a window resize re-stretching the cached wash is
            // invisible at this blur level and skips a re-render.
            new BufferedContainer(cachedFrameBuffer: true)
            {
                RelativeSizeAxes = Axes.Both,
                FrameBufferScale = new Vector2(WashFrameBufferScale),
                BlurSigma = WashBlurSigma,
                RedrawOnScale = false,
                Colour = new Colour4(WashBrightness, WashBrightness, WashBrightness, 1),
                Child = new Sprite
                {
                    Texture = jacket,
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
            },
            // Disc: the jacket clipped to the playfield circle. Mirrors the playfield's geometry —
            // same padding, circle inscribed in min(width, height) — so it aligns with the ring.
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(GarbusPlayfield.SCREEN_PADDING),
                Child = new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    Child = new Sprite
                    {
                        Texture = jacket,
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = new Colour4(DiscBrightness, DiscBrightness, DiscBrightness, 1),
                    },
                },
            },
        };
    }
}
```

Note: `Sprite.Texture`'s setter updates `FillAspectRatio`, so `FillMode.Fill`/`FillMode.Fit` respect the jacket's aspect ratio automatically.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneJacketBackground"`
Expected: PASS (2 tests). Also confirm the build produced no new warnings.

- [ ] **Step 6: Commit**

```
git add Garbus.Game/UI/JacketBackground.cs Garbus.Game/UI/GarbusPlayfield.cs Garbus.Game.Tests/Visual/TestSceneJacketBackground.cs
git commit -m "feat: jacket background component (wash + ring-clipped disc)"
```

---

### Task 2: PlayScreen hosts the background

**Files:**
- Modify: `Garbus.Game/Screens/PlayScreen.cs`
- Test: `Garbus.Game.Tests/Visual/TestScenePlayScreen.cs`

**Interfaces:**
- Consumes: `JacketBackground(Texture?)` from Task 1.
- Produces: `PlayScreen(GarbusChart chart, Track track, double startTime = 0, Texture? jacket = null)` and `PlayScreen(PlayableChart chart, Track track, double startTime = 0, Texture? jacket = null)`. The screen always hosts one `JacketBackground` directly above its base `Box`.

- [ ] **Step 1: Write the failing tests**

Add to `Garbus.Game.Tests/Visual/TestScenePlayScreen.cs` (add `using` directives as needed: `Garbus.Game.Charts`, `Garbus.Game.UI`, `osu.Framework.Allocation`, `osu.Framework.Audio.Track`, `osu.Framework.Graphics.Rendering`, `osu.Framework.Graphics.Sprites`, `osu.Framework.Testing`):

```csharp
[Resolved]
private IRenderer renderer { get; set; } = null!;

[Test]
public void TestJacketLayersPresentWhenJacketProvided()
{
    AddStep("recreate with jacket", () =>
    {
        var chart = new GarbusChart();
        Child = new ScreenStack(playScreen = new PlayScreen(chart, new TrackVirtual(60000), jacket: renderer.WhitePixel))
        {
            RelativeSizeAxes = Axes.Both,
        };
    });
    AddUntilStep("screen loaded", () => playScreen.IsLoaded);
    AddAssert("jacket background has layers", () =>
        playScreen.ChildrenOfType<JacketBackground>().Single().ChildrenOfType<Sprite>().Any());
}

[Test]
public void TestNoJacketLayersOnBundledPath()
{
    // The SetUpSteps screen is the bundled test chart, which has no jacket: the background
    // component is present but empty, leaving the flat base box visible.
    AddAssert("no jacket layers", () =>
        !playScreen.ChildrenOfType<JacketBackground>().Single().ChildrenOfType<Sprite>().Any());
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestScenePlayScreen"`
Expected: the two new tests FAIL (no `jacket` parameter / no `JacketBackground` in the tree); existing tests still pass.

- [ ] **Step 3: Implement**

In `Garbus.Game/Screens/PlayScreen.cs`:

1. Add `using osu.Framework.Graphics.Textures;`.
2. Add a field: `private readonly Texture? jacket;`
3. Extend both injected-chart constructors with a trailing `Texture? jacket = null` parameter, assigning `this.jacket = jacket;`:

```csharp
public PlayScreen(GarbusChart chart, Track track, double startTime = 0, Texture? jacket = null)
```
```csharp
public PlayScreen(PlayableChart chart, Track track, double startTime = 0, Texture? jacket = null)
```

4. In `load`, insert the background between the base box and the gameplay clock container:

```csharp
new Box
{
    Colour = new Color4(18, 18, 26, 255),
    RelativeSizeAxes = Axes.Both,
},
new JacketBackground(jacket),
gameplayClock = new MasterGameplayClockContainer(track, StartTime)
```

(The parameterless bundled-chart constructor leaves `jacket` null — the component adds no layers.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestScenePlayScreen"`
Expected: PASS (all tests, old and new).

- [ ] **Step 5: Commit**

```
git add Garbus.Game/Screens/PlayScreen.cs Garbus.Game.Tests/Visual/TestScenePlayScreen.cs
git commit -m "feat: host jacket background in PlayScreen"
```

---

### Task 3: Song select passes the jacket

**Files:**
- Modify: `Garbus.Game/Screens/SongSelect/SongSelectScreen.cs` (the `Push(new PlayScreen(...))` call, ~line 295)
- Modify: `docs/agents/screens.md` (play-loop section)
- Test: `Garbus.Game.Tests/Visual/TestSceneSongSelect.cs`

**Interfaces:**
- Consumes: `PlayScreen(..., Texture? jacket)` from Task 2; existing `IChartSource.GetBackground(ChartCard)`.

- [ ] **Step 1: Write the failing test**

In `Garbus.Game.Tests/Visual/TestSceneSongSelect.cs`, extend the `"seed extra charts"` step so `song-1` gets a jacket (add `using SixLabors.ImageSharp;`, `using SixLabors.ImageSharp.PixelFormats;`, `using Garbus.Game.UI;`, `using osu.Framework.Graphics.Sprites;`, `using osu.Framework.Testing;` as needed). Inside the `for` loop, after writing the audio file:

```csharp
string background = string.Empty;

if (i == 1)
{
    // A generated jacket (never real song content) so the launch test can assert jacket flow.
    string jacketPath = Path.Combine(dir, "jacket.png");
    if (!File.Exists(jacketPath))
    {
        using var img = new Image<Rgba32>(4, 4);
        img.SaveAsPng(jacketPath);
    }
    background = "jacket.png";
}
```

and set it on the seeded chart: `Metadata = { ..., BackgroundFile = background }` (the legacy-chart wrap maps `Metadata.BackgroundFile` → `Resources.Background`, which `DirectoryChartSource` serves via `GetBackground`).

Add the test:

```csharp
[Test]
public void TestLaunchPassesJacketToPlayScreen()
{
    AddStep("select jacketed chart", () => songSelect.Select(
        songSelect.Groups.SelectMany(g => g.Charts).First(c => !string.IsNullOrEmpty(c.BackgroundFile))));
    AddStep("launch", () => songSelect.Launch());
    AddUntilStep("play screen pushed", () => stack.CurrentScreen is PlayScreen);
    AddUntilStep("jacket layers present", () => (stack.CurrentScreen as PlayScreen)
        ?.ChildrenOfType<JacketBackground>().SingleOrDefault()
        ?.ChildrenOfType<Sprite>().Any() == true);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneSongSelect"`
Expected: `TestLaunchPassesJacketToPlayScreen` FAILS at "jacket layers present" (jacket not passed); existing tests still pass.

- [ ] **Step 3: Implement**

In `SongSelectScreen` (the launch method, ~line 295), pass the jacket:

```csharp
this.Push(new PlayScreen(chart, track, jacket: SelectedChart.Source.GetBackground(SelectedChart)));
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneSongSelect"`
Expected: PASS (all tests).

- [ ] **Step 5: Update the screens domain doc**

In `docs/agents/screens.md`, in the section covering the play loop / PlayScreen composition, add one sentence:

> `PlayScreen` hosts a `JacketBackground` under the gameplay subtree: the song's jacket circle-clipped to the ring's disc plus a cached blurred color wash behind it; song select passes the jacket via `IChartSource.GetBackground`, and a null jacket leaves the flat base background.

(Adjust placement to fit the doc's existing structure; present tense, no history framing.)

- [ ] **Step 6: Commit**

```
git add Garbus.Game/Screens/SongSelect/SongSelectScreen.cs Garbus.Game.Tests/Visual/TestSceneSongSelect.cs docs/agents/screens.md
git commit -m "feat: pass jacket from song select into gameplay"
```

---

### Task 4: Editor F5 test mode passes the jacket

**Files:**
- Modify: `Garbus.Game/Charts/SongFile.cs` (new `GetJacketTexture`, mirroring `GetTrackStore`)
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs` (`StartTestMode`, ~line 384)
- Modify: `docs/agents/editor.md` (test-mode section)
- Test: `Garbus.Game.Tests/Visual/TestSceneSongFileJacket.cs`

**Interfaces:**
- Consumes: `PlayScreen(..., Texture? jacket)` from Task 2.
- Produces: `public Texture? SongFile.GetJacketTexture(GameHost host)` — null when the song has no directory or no `Resources.Background`; otherwise loads via a cached per-directory `LargeTextureStore`.

- [ ] **Step 1: Write the failing tests**

Create `Garbus.Game.Tests/Visual/TestSceneSongFileJacket.cs`:

```csharp
// Pins for SongFile.GetJacketTexture, the editor test-mode jacket source: null without a saved
// directory or a Resources.Background entry, and a real texture when a jacket file exists beside
// the saved song. Uses a generated png fixture (never real song content).

using System.IO;
using Garbus.Game.Charts;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Garbus.Game.Tests.Visual
{
    public partial class TestSceneSongFileJacket : GarbusTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Test]
        public void TestNullWithoutDirectory()
        {
            AddAssert("unsaved song has no jacket", () =>
            {
                var songFile = new SongFile(GarbusSong.CreateDefault());
                songFile.Song.Resources.Background = "jacket.png";
                return songFile.GetJacketTexture(host) == null;
            });
        }

        [Test]
        public void TestNullWithoutBackgroundResource()
        {
            AddAssert("song with no background resource has no jacket", () =>
            {
                string dir = storage.GetStorageForDirectory("jacket-test-nobg").GetFullPath(string.Empty);
                var songFile = new SongFile(GarbusSong.CreateDefault());
                songFile.Save(Path.Combine(dir, "song.garbus"));
                return songFile.GetJacketTexture(host) == null;
            });
        }

        [Test]
        public void TestLoadsJacketBesideSavedSong()
        {
            AddAssert("saved song with jacket file loads it", () =>
            {
                string dir = storage.GetStorageForDirectory("jacket-test").GetFullPath(string.Empty);

                using (var img = new Image<Rgba32>(4, 4))
                    img.SaveAsPng(Path.Combine(dir, "jacket.png"));

                var songFile = new SongFile(GarbusSong.CreateDefault());
                songFile.Song.Resources.Background = "jacket.png";
                songFile.Save(Path.Combine(dir, "song.garbus"));

                return songFile.GetJacketTexture(host) != null;
            });
        }
    }
}
```

(If `SongFile`'s constructor or `Save` signature differs, adapt the arrange steps to the actual API — `GarbusEditor.cs` line 523 constructs `new SongFile(GarbusSong.CreateDefault())` and `SaveAs` calls `SongFile.Save(path)`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneSongFileJacket"`
Expected: FAIL — `GetJacketTexture` does not exist (compile failure counts).

- [ ] **Step 3: Implement `GetJacketTexture`**

In `Garbus.Game/Charts/SongFile.cs`, next to `GetTrackStore` (add `using osu.Framework.Graphics.Textures;` and `using osu.Framework.Platform;` / `osu.Framework.IO.Stores;` as needed):

```csharp
private LargeTextureStore? jacketStore;
private string? jacketStoreDirectory;

/// <summary>
/// Loads the song's jacket texture from its directory, or null when the song has never been
/// saved or has no <see cref="SongResources.Background"/>. Mirrors <see cref="GetTrackStore"/>'s
/// per-directory store caching. LargeTextureStore: jackets are large and must not be atlased.
/// </summary>
public Texture? GetJacketTexture(GameHost host)
{
    if (Directory == null || string.IsNullOrEmpty(Song.Resources.Background))
        return null;

    if (jacketStore == null || !string.Equals(jacketStoreDirectory, Directory, StringComparison.OrdinalIgnoreCase))
    {
        jacketStore = new LargeTextureStore(host.Renderer,
            host.CreateTextureLoaderStore(new StorageBackedResourceStore(new NativeStorage(Directory))),
            manualMipmaps: false);
        jacketStoreDirectory = Directory;
    }

    return jacketStore.Get(Song.Resources.Background);
}
```

- [ ] **Step 4: Pass it from `StartTestMode`**

In `Garbus.Game/Edit/Screens/GarbusEditor.cs`:

1. Add a resolved host (with the class's other `[Resolved]` fields; add `using osu.Framework.Platform;`):

```csharp
[Resolved]
private GameHost gameHost { get; set; } = null!;
```

2. Change the push at the end of `StartTestMode` (~line 384):

```csharp
this.Push(new PlayScreen(clonedChart, freshTrack, startTime, SongFile.GetJacketTexture(gameHost)));
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneSongFileJacket"`
Expected: PASS (3 tests). Then run the editor test-mode suite to confirm no regression:
`dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneEditorTestMode"` — PASS (unsaved test songs yield a null jacket, which Task 2 makes a no-op).

- [ ] **Step 6: Update the editor domain doc**

In `docs/agents/editor.md`, in the test-mode section, add one sentence:

> Test mode also passes the song's jacket (`SongFile.GetJacketTexture`) so the pushed `PlayScreen` shows the same jacket background as real play; unsaved songs or songs without a jacket get none.

- [ ] **Step 7: Commit**

```
git add Garbus.Game/Charts/SongFile.cs Garbus.Game/Edit/Screens/GarbusEditor.cs Garbus.Game.Tests/Visual/TestSceneSongFileJacket.cs docs/agents/editor.md
git commit -m "feat: editor test mode passes the song jacket to gameplay"
```

---

### Task 5: Tuning scene

**Files:**
- Create: `Garbus.Game.Tests/Tuning/TestSceneJacketBackgroundTuning.cs`
- Modify: `docs/agents/gameplay.md` (add the background to the layout section)

**Interfaces:**
- Consumes: `JacketBackground` and its four init properties from Task 1.

- [ ] **Step 1: Write the tuning scene**

Create `Garbus.Game.Tests/Tuning/TestSceneJacketBackgroundTuning.cs` (pattern: `TestSceneSliderGlowTuning` — every parameter a live control, rebuild on change, `[Explicit]`):

```csharp
// Interactive tuning scene for the gameplay jacket background: disc/wash dim, blur sigma, and
// framebuffer scale are sliders, over a generated multi-color blob jacket (never real song
// content) with an empty playfield on top for ring alignment. [Explicit] — eyeball scene, pick it
// in the visual test browser.

using System;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Garbus.Game.Tests.Tuning
{
    [TestFixture]
    [Explicit]
    public partial class TestSceneJacketBackgroundTuning : GarbusTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private Texture jacket = null!;

        // Defaults mirror JacketBackground's init-property defaults; tweak there once chosen here.
        private float discBrightness = 0.2f;
        private float washBrightness = 0.55f;
        private float blurSigma = 5;
        private float frameBufferScale = 0.05f;
        private bool showJacket = true;

        public TestSceneJacketBackgroundTuning()
        {
            AddSliderStep("disc brightness", 0f, 1f, discBrightness, v => { discBrightness = v; scheduleRebuild(); });
            AddSliderStep("wash brightness", 0f, 1f, washBrightness, v => { washBrightness = v; scheduleRebuild(); });
            AddSliderStep("wash blur sigma", 0f, 20f, blurSigma, v => { blurSigma = v; scheduleRebuild(); });
            AddSliderStep("wash framebuffer scale", 0.01f, 1f, frameBufferScale, v => { frameBufferScale = v; scheduleRebuild(); });
            AddToggleStep("jacket present", v => { showJacket = v; scheduleRebuild(); });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            jacket = createTestJacket(renderer);
            rebuild();
        }

        private void scheduleRebuild() => Scheduler.AddOnce(rebuild);

        private void rebuild()
        {
            if (jacket == null)
                return;

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // PlayScreen's base box, so dim levels read against the real backdrop.
                    new Box
                    {
                        Colour = new Colour4(18, 18, 26, 255),
                        RelativeSizeAxes = Axes.Both,
                    },
                    new JacketBackground(showJacket ? jacket : null)
                    {
                        DiscBrightness = discBrightness,
                        WashBrightness = washBrightness,
                        WashBlurSigma = new Vector2(blurSigma),
                        WashFrameBufferScale = frameBufferScale,
                    },
                    // Empty playfield on top: shows the ring so disc alignment can be eyeballed.
                    new GarbusPlayfield(interactive: false),
                },
            };
        }

        /// <summary>
        /// A colorful square stand-in jacket: four soft color blobs on a dark base, enough hue
        /// variety to judge how the wash dissolves art into component colors.
        /// </summary>
        private static Texture createTestJacket(IRenderer renderer)
        {
            const int size = 256;

            var blobs = new (float cx, float cy, Rgba32 colour)[]
            {
                (0.25f, 0.3f, new Rgba32(220, 60, 60)),
                (0.75f, 0.25f, new Rgba32(60, 120, 220)),
                (0.3f, 0.75f, new Rgba32(240, 200, 70)),
                (0.7f, 0.7f, new Rgba32(90, 200, 120)),
            };

            using var image = new Image<Rgba32>(size, size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = 25, g = 25, b = 35;

                    foreach (var blob in blobs)
                    {
                        float dx = x / (float)size - blob.cx;
                        float dy = y / (float)size - blob.cy;
                        float w = MathF.Exp(-(dx * dx + dy * dy) / 0.03f);
                        r += blob.colour.R * w;
                        g += blob.colour.G * w;
                        b += blob.colour.B * w;
                    }

                    image[x, y] = new Rgba32((byte)Math.Min(255, r), (byte)Math.Min(255, g), (byte)Math.Min(255, b));
                }
            }

            var texture = renderer.CreateTexture(size, size);
            texture.SetData(new TextureUpload(image));
            return texture;
        }
    }
}
```

(`Image<Rgba32>` construction and `renderer.CreateTexture(...).SetData(new TextureUpload(image))` follow the existing `PlayfieldKeybeam.createGradientTexture` pattern. Resolve any ImageSharp `using` ambiguity the same way `TestSceneSongSelectBackgroundLeak` does.)

- [ ] **Step 2: Verify it builds and the suite stays green**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: full suite PASS, no new warnings; the `[Explicit]` scene does not run headlessly. (Being an eyeball scene, its checked-in verification is compile + the shared component's Task 1 pins.)

- [ ] **Step 3: Update the gameplay domain doc**

In `docs/agents/gameplay.md`, add to the "Layout: playfield → ring → lanes" section:

> - `UI/JacketBackground.cs` — the static jacket background under the playfield (hosted by `PlayScreen`, outside the gameplay-clock subtree): the song jacket circle-clipped to the ring's disc (sharing `GarbusPlayfield.SCREEN_PADDING` for alignment) plus a one-shot cached downscale+blur color wash behind it. Tuned in `Tuning/TestSceneJacketBackgroundTuning`.

- [ ] **Step 4: Commit**

```
git add Garbus.Game.Tests/Tuning/TestSceneJacketBackgroundTuning.cs docs/agents/gameplay.md
git commit -m "test: jacket background tuning scene"
```

---

## Final verification (after all tasks)

- [ ] Run the full suite: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj` — all green, no new warnings.
- [ ] Confirm every spec requirement maps to landed code: component + both layers (Task 1), PlayScreen hosting + null fallback (Task 2), song-select plumbing (Task 3), editor plumbing (Task 4), tuning scene + docs (Task 5).
