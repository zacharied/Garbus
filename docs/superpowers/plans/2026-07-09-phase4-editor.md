# Phase 4 — Editor Rebuild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the BAC editor onto Garbus with functionality parity, rebuilding the osu.Game editor scaffolding it stands on (spec: `docs/superpowers/specs/2026-07-09-phase4-editor-design.md`).

**Architecture:** Hybrid — vendor osu's isolated editor core (EditorClock, beat divisor, change handler, blueprint/selection stack) with trims; rebuild all screens/chrome bespoke on framework `Basic*` controls; port BAC's ~2,400 lines of editor logic (`Bac*` → `Garbus*`) on top. Four tabs (Setup/Compose/Timing/Verify), full menu bar, free-floating `.garbus` files.

**Tech Stack:** C# / .NET 8, osu-framework 2026.629.0, System.Text.Json, NUnit headless visual tests.

## Global Constraints

- Repo rule (CLAUDE.md line 8): **no compatibility layers, no version-number increments, no historical context in docs.** `GarbusChartSerializer.CURRENT_VERSION` stays `1`.
- Vendored osu.Game files keep the ppy MIT attribution header **plus** an `// Adapted for Garbus:` line summarising trims. Vendor faithfully; deviate minimally and note why.
- Source clones for vendoring: `C:\Users\zachd\Code\BAC\LocalDependencies\osu` and `...\osu-framework`. BAC editor source to port: `C:\Users\zachd\Code\BAC\osu.Game.Rulesets.BigAssCircle\Edit\`.
- Naming: osu's "beatmap" → "chart"; `Bac*` → `Garbus*`; namespaces `Garbus.Game.*`.
- Nullability enabled solution-wide; DI/BDL-initialised fields use `= null!`.
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj` (run filtered per task; full suite before each commit). All pre-existing tests (20) must stay green.
- No osu.Game package/DLL references — everything comes from osu-framework or vendored source.
- UI visuals are throwaway: framework `Basic*` controls, hardcoded colours. Functionality parity is the bar.

## File structure (where everything lands)

```
Garbus.Game/
  Charts/                      (exists) ChartMetadata + GarbusChart gain fields; Format/ DTOs follow
  Charts/ChartFile.cs          NEW  load/save .garbus at arbitrary disk paths + per-chart resources
  Edit/                        NEW  vendored editor core + ported BAC logic
    BindableBeatDivisor.cs     vendored
    EditorClock.cs             vendored
    EditorChart.cs             vendored EditorBeatmap, adapted to GarbusChart
    TransactionalCommitComponent.cs  vendored
    EditorChangeHandler.cs     vendored abstract base
    GarbusChartChangeHandler.cs NEW  serializer-snapshot undo/redo
    EditorAngleMapping.cs      ported as-is from BAC
    GarbusHitObjectComposer.cs ported composer
    GarbusEditorPlayfield.cs   ported
    GarbusBlueprintContainer.cs / GarbusSelectionHandler.cs / GarbusSnapResult.cs / GarbusBeatSnapGrid.cs
    Tools/                     ported composition tools
    Compose/                   vendored blueprint stack (BlueprintContainer, ComposeBlueprintContainer,
                               SelectionHandler, EditorSelectionHandler, SelectionBox+pieces, DragBox,
                               ScrollingDragBox, MoveSelectionEvent, SnapResult, blueprint bases,
                               HitObjectComposer/ScrollingHitObjectComposer, toolbox components)
    Drawables/                 ported editor drawables (EditorDrawableGarbusHitObject + per-type)
    Blueprints/                ported placement/selection blueprints (+ Components/)
    Screens/                   NEW bespoke shell + tabs
      GarbusEditor.cs          the editor Screen (tabs, menus, bottom bar, dirty tracking)
      EditorTab.cs             enum Setup/Compose/Timing/Verify
      ComposeTab.cs            timeline strip + composer host
      SetupTab.cs / TimingTab.cs / VerifyTab.cs
      Timeline/                TimelineStrip, ZoomableScrollContainer (vendored), tick/marker layers
      BottomBar/               TimeInfoDisplay, SummaryTimeline, PlaybackControl
      Dialogs/                 ConfirmDialog overlay
      Setup/                   form sections + LabelledFileChooserRow
      Timing/                  timing point list + settings + TapTimingControl + metronome
      Verify/                  IssueTable, ICheck + 4 checks
  Gameplay/UI/Scrolling/       gains vendored ScrollingHitObjectContainer + ScrollingPlayfield
  Screens/
    MainMenuScreen.cs          NEW  Play / New Chart / Open Chart
    PlayScreen.cs              gains in-memory chart + track constructor (test mode)
Garbus.Game.Tests/
  Editor/                      NEW  GarbusEditorTestScene + per-area test scenes
  Charts/                      TestChartFormat extended; TestChartFile NEW
Garbus.Resources/
  Samples/Editor/metronome-tick.wav (+ downbeat)  synthesized
  Charts/test-chart.garbus     regenerated
```

Dependency order: A (format/IO) → B (core) → C (shell) → D (compose editing) → E (compose chrome) → F (tabs/finish). Within a milestone tasks are sequential unless noted.

---

## Milestone A — chart format & disk I/O

### Task 1: Format additions (metadata fields, backgroundFile, previewTime)

**Files:**
- Modify: `Garbus.Game/Charts/ChartMetadata.cs`
- Modify: `Garbus.Game/Charts/GarbusChart.cs`
- Modify: `Garbus.Game/Charts/Format/ChartFileDto.cs`
- Modify: `Garbus.Game/Charts/Format/GarbusChartSerializer.cs`
- Modify: `Garbus.Game.Tests/TestChartFormat.cs` (locate by glob; extend roundtrip coverage)
- Regenerate: `Garbus.Resources/Charts/test-chart.garbus`

**Interfaces:**
- Consumes: existing `GarbusChartSerializer.Encode/Decode`, `ChartFileDto` DTO layer.
- Produces: `ChartMetadata` gains `string RomanisedTitle`, `string RomanisedArtist`, `string Source`, `string Tags`, `string BackgroundFile` (all `= string.Empty`; empty string = unset, consistent with existing fields — no nullables in metadata). `GarbusChart` gains `double? PreviewTime` (ms). DTOs mirror 1:1. Later tasks (Setup tab, Verify checks, Timing menu) read/write exactly these names.

- [ ] **Step 1: Write the failing test** — in `TestChartFormat`, extend the existing roundtrip test's chart fixture (or add `TestNewFieldsRoundtrip`) so a chart carrying all new fields survives Encode→Decode:

```csharp
[Test]
public void TestNewFieldsRoundtrip()
{
    var chart = new GarbusChart
    {
        Metadata = new ChartMetadata
        {
            Title = "T", RomanisedTitle = "T-rom",
            Artist = "A", RomanisedArtist = "A-rom",
            Charter = "C", ChartName = "N",
            Source = "some game", Tags = "tag1 tag2",
            AudioFile = "track.ogg", BackgroundFile = "bg.png",
        },
        PreviewTime = 12345.0,
    };

    var decoded = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(chart));

    Assert.That(decoded.Metadata.RomanisedTitle, Is.EqualTo("T-rom"));
    Assert.That(decoded.Metadata.RomanisedArtist, Is.EqualTo("A-rom"));
    Assert.That(decoded.Metadata.Source, Is.EqualTo("some game"));
    Assert.That(decoded.Metadata.Tags, Is.EqualTo("tag1 tag2"));
    Assert.That(decoded.Metadata.BackgroundFile, Is.EqualTo("bg.png"));
    Assert.That(decoded.PreviewTime, Is.EqualTo(12345.0));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestNewFieldsRoundtrip`
Expected: FAIL — compile error (`RomanisedTitle` does not exist), which counts as the failing state.

- [ ] **Step 3: Implement** — add to `ChartMetadata` (after `ChartName`, before `AudioFile`):

```csharp
    /// <summary>UTF-8 latin-readable variants for players who can't read the native script.</summary>
    public string RomanisedTitle { get; set; } = string.Empty;

    public string RomanisedArtist { get; set; } = string.Empty;

    /// <summary>The media the song comes from.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Space-separated search tags.</summary>
    public string Tags { get; set; } = string.Empty;
```

and after `AudioFile`:

```csharp
    /// <summary>
    /// The background image beside the chart file (full filename including extension);
    /// empty when the chart has none. Stored only this phase — nothing renders it yet.
    /// </summary>
    public string BackgroundFile { get; set; } = string.Empty;
```

Add to `GarbusChart` (after `Metadata`):

```csharp
    /// <summary>The song-select audio preview point in ms, or null when unset.</summary>
    public double? PreviewTime { get; set; }
```

(`PreviewTime` is `{ get; set; }`, not `init` — the editor's Timing menu mutates it.) Mirror all six fields onto `ChartMetadataDto` / `ChartFileDto` (`PreviewTime` as `double?`) and map them in `GarbusChartSerializer.toDto`/`fromDto` — both directions, all fields. `CURRENT_VERSION` stays 1 (global constraint).

- [ ] **Step 4: Run the format test group**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestChartFormat`
Expected: PASS, including the pre-existing bundled-file-agreement test — if that one fails, re-run the regeneration in Step 5 first (order the steps so regeneration precedes the assertion if needed).

- [ ] **Step 5: Regenerate the bundled chart** — run the `[Explicit]` test `TestChartFormat.RegenerateBundledTestChart` (see its header comment for the exact invocation used in Phase 3), confirm `Garbus.Resources/Charts/test-chart.garbus` now carries the new (empty/null) fields, then run the full suite.

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all green (20 pre-existing + new).

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "Chart format: romanised metadata, source/tags, backgroundFile, previewTime"
```

### Task 2: ChartFile — disk load/save + per-chart resources

**Files:**
- Create: `Garbus.Game/Charts/ChartFile.cs`
- Test: `Garbus.Game.Tests/Charts/TestChartFile.cs`

**Interfaces:**
- Consumes: `GarbusChartSerializer` (Task 1), framework `NativeStorage`, `StorageBackedResourceStore`, `ITrackStore`/`AudioManager.GetTrackStore`, `TextureLoaderStore`.
- Produces (used by the editor shell, Setup tab, and test mode):

```csharp
namespace Garbus.Game.Charts;

/// <summary>A .garbus chart on disk plus the directory its audio/background live in.</summary>
public class ChartFile
{
    public GarbusChart Chart { get; }
    /// <summary>Absolute path of the .garbus file, or null for a new unsaved chart.</summary>
    public string? FilePath { get; private set; }
    /// <summary>Directory containing the chart and its resources; null until first save.</summary>
    public string? Directory => FilePath == null ? null : Path.GetDirectoryName(FilePath);

    public ChartFile(GarbusChart chart, string? filePath = null);
    public static ChartFile Load(string path);            // Decode + ApplyDefaults
    public void Save(string path);                        // Encode + write; updates FilePath
    public void Save();                                   // throws InvalidOperationException if FilePath null
    /// <summary>Copies an external file into the chart directory (overwrite allowed); returns the bare filename. Throws if Directory is null.</summary>
    public string ImportResource(string sourcePath);
    /// <summary>Track store rooted at the chart directory, or null before first save. Created lazily, invalidated on Save-to-new-path.</summary>
    public ITrackStore? GetTrackStore(AudioManager audio);
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
// Garbus.Game.Tests/Charts/TestChartFile.cs
[TestFixture]
public class TestChartFile
{
    private string tempDir = null!;

    [SetUp]
    public void SetUp() => tempDir = Directory.CreateTempSubdirectory("garbus-test-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(tempDir, true);

    [Test]
    public void TestSaveLoadRoundtrip()
    {
        var chart = GarbusTestChartGenerator.GenerateChart();
        var file = new ChartFile(chart);
        Assert.That(file.FilePath, Is.Null);
        Assert.That(file.Directory, Is.Null);

        string path = Path.Combine(tempDir, "my chart.garbus");
        file.Save(path);
        Assert.That(File.Exists(path));
        Assert.That(file.Directory, Is.EqualTo(tempDir));

        var loaded = ChartFile.Load(path);
        Assert.That(loaded.Chart.HitObjects.Count, Is.EqualTo(chart.HitObjects.Count));
        Assert.That(loaded.FilePath, Is.EqualTo(path));
    }

    [Test]
    public void TestSaveWithoutPathThrows()
        => Assert.Throws<InvalidOperationException>(() => new ChartFile(new GarbusChart()).Save());

    [Test]
    public void TestImportResourceCopiesIntoDirectory()
    {
        string source = Path.Combine(tempDir, "elsewhere", "song.ogg");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });

        var file = new ChartFile(new GarbusChart());
        Assert.Throws<InvalidOperationException>(() => file.ImportResource(source)); // unsaved

        file.Save(Path.Combine(tempDir, "c.garbus"));
        string name = file.ImportResource(source);
        Assert.That(name, Is.EqualTo("song.ogg"));
        Assert.That(File.Exists(Path.Combine(tempDir, "song.ogg")));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestChartFile`
Expected: FAIL — `ChartFile` does not exist.

- [ ] **Step 3: Implement `ChartFile`** exactly per the Produces block. Implementation notes: `Load` = `File.ReadAllText` → `GarbusChartSerializer.Decode` → `chart.ApplyDefaults()`. `Save` = `GarbusChartSerializer.Encode` → `File.WriteAllText`, then `FilePath = path` and drop any cached track store. `GetTrackStore` = `audio.GetTrackStore(new StorageBackedResourceStore(new NativeStorage(Directory)))` cached per directory (note: framework `ITrackStore` lookups need the full filename incl. extension — same gotcha as CLAUDE.md's `Tracks/test-track.ogg` note). `ImportResource` = `File.Copy(sourcePath, Path.Combine(Directory, Path.GetFileName(sourcePath)), overwrite: true)`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestChartFile`
Expected: PASS. Then full suite: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj` — all green.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "ChartFile: disk load/save and per-chart resource resolution"
```

---

## Milestone B — editor core (vendored)

### Task 3: Vendor BindableBeatDivisor

**Files:**
- Create: `Garbus.Game/Edit/BindableBeatDivisor.cs` (from `LocalDependencies\osu\osu.Game\Screens\Edit\BindableBeatDivisor.cs`)
- Modify: `Garbus.Game/Charts/Timing/` — whichever file inlined `PREDEFINED_DIVISORS` in Phase 3 (grep for `PREDEFINED_DIVISORS`); it now references `BindableBeatDivisor.PREDEFINED_DIVISORS`
- Test: `Garbus.Game.Tests/Editor/TestBindableBeatDivisor.cs`

**Interfaces:**
- Produces: `Garbus.Game.Edit.BindableBeatDivisor : BindableInt` — `static readonly int[] PREDEFINED_DIVISORS = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 16 }` (osu's real list), `SetArbitraryDivisor(int)`, `ValidDivisors` bindable, `SelectNext()`/`SelectPrevious()` snap cycling (osu's real names), static `GetDivisorForBeatIndex(int index, int beatDivisor)`. Keep osu's API surface — the beat snap grid, timeline ticks, and divisor control (later tasks) call these.
- Trims: any `OsuColour`/display helpers (e.g. `GetColourFor`) — replace call sites later with hardcoded colours; keep the numeric logic verbatim.

- [ ] **Step 1: Write the failing test** — divisor presets, arbitrary divisor, next/previous cycling:

```csharp
[TestFixture]
public class TestBindableBeatDivisor
{
    [Test]
    public void TestPresetCycle()
    {
        var divisor = new BindableBeatDivisor(4);
        divisor.Next();      // moves within the current preset collection
        Assert.That(divisor.Value, Is.Not.EqualTo(4));
        divisor.Previous();
        Assert.That(divisor.Value, Is.EqualTo(4));
    }

    [Test]
    public void TestArbitraryDivisor()
    {
        var divisor = new BindableBeatDivisor();
        divisor.SetArbitraryDivisor(5);
        Assert.That(divisor.Value, Is.EqualTo(5));
    }
}
```

- [ ] **Step 2: Run to verify failure** — `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestBindableBeatDivisor` → FAIL (type missing).
- [ ] **Step 3: Vendor the file.** Copy, keep ppy header + add `// Adapted for Garbus:` line, namespace `Garbus.Game.Edit`, strip colour/display helpers and any `osu.Game` using that remains. Point `Charts/Timing`'s Phase 3 inlined divisor list at `BindableBeatDivisor.PREDEFINED_DIVISORS` and delete the inline copy.
- [ ] **Step 4: Run** — filtered test PASS, then full suite green (Charts tests prove the divisor-list move broke nothing).
- [ ] **Step 5: Commit** — `git add -A && git commit -m "Vendor BindableBeatDivisor"`

### Task 4: Vendor EditorClock

**Files:**
- Create: `Garbus.Game/Edit/EditorClock.cs` (from `LocalDependencies\osu\osu.Game\Screens\Edit\EditorClock.cs`, ~344 L)
- Test: `Garbus.Game.Tests/Editor/TestSceneEditorClock.cs`

**Interfaces:**
- Consumes: `FramedChartClock` (Garbus.Game/Timing — already takes a `Track` directly), `ControlPointInfo` (Charts/Timing), `BindableBeatDivisor` (Task 3), framework `Track`/`TrackVirtual`.
- Produces: `Garbus.Game.Edit.EditorClock : CompositeComponent, IFrameBasedClock, IAdjustableClock, ISourceChangeableClock` with osu's API: `CurrentTime`, `TrackLength`, `ControlPointInfo` (settable reference), `Seek(double)`, `SeekSmoothlyTo(double)`, `SeekSnapped(double)`, `SeekBackward/SeekForward(bool snapped, double amount)`, `Start()/Stop()`, `SeekingOrStopped`, `IsRunning`, `ChangeSource(Track)`, `AudioAdjustments` (for playback speed). Constructor: `EditorClock(ControlPointInfo controlPointInfo, double trackLength, BindableBeatDivisor beatDivisor)`.
- Trims: `IBeatSyncProvider`/kiai plumbing if it drags osu.Game types; `EditorBeatmap` references become the injected `ControlPointInfo` (the clock only uses timing points for snapping). **Trackless charts:** callers pass `TrackVirtual` (framework) with length 60000 ms until Setup assigns real audio — no special casing inside the clock.

- [ ] **Step 1: Write the failing test** (headless `GarbusTestScene`; the existing `TestSceneClockStack` shows the pattern for stepping clocks in tests — mirror it):

```csharp
[TestFixture]
public partial class TestSceneEditorClock : GarbusTestScene
{
    [Test]
    public void TestSeekSnappedToDivisor()
    {
        var cpi = new ControlPointInfo();
        cpi.Add(0, new TimingControlPoint { BeatLength = 500 }); // 120 BPM
        var divisor = new BindableBeatDivisor(4);
        EditorClock clock = null!;

        AddStep("create clock", () => Child = clock = new EditorClock(cpi, 60000, divisor));
        AddStep("change source", () => clock.ChangeSource(new TrackVirtual(60000)));
        AddStep("seek snapped to 130", () => clock.SeekSnapped(130));
        AddAssert("snapped to 125 (1/4 of 500ms beat)", () => Precision.AlmostEquals(clock.CurrentTime, 125, 1));
        AddStep("seek forward snapped", () => clock.SeekForward(true, 1));
        AddAssert("advanced by one divisor step", () => clock.CurrentTime > 125);
    }
}
```

- [ ] **Step 2: Run to verify failure** — `--filter TestSceneEditorClock` → FAIL.
- [ ] **Step 3: Vendor.** Copy EditorClock, keep header + adaptation line, swap `FramedBeatmapClock` → `FramedChartClock`, `beatmap.ControlPointInfo` → constructor-injected settable `ControlPointInfo` property (the editor swaps it when a chart loads). `SeekSmoothlyTo` is framework transforms — keep it.
- [ ] **Step 4: Run** — filtered PASS, full suite green.
- [ ] **Step 5: Commit** — `git add -A && git commit -m "Vendor EditorClock"`

### Task 5: EditorChart (EditorBeatmap counterpart)

**Files:**
- Create: `Garbus.Game/Edit/TransactionalCommitComponent.cs` (from `LocalDependencies\osu\osu.Game\Screens\Edit\TransactionalCommitComponent.cs`, ~60 L — vendor verbatim, namespace change only)
- Create: `Garbus.Game/Edit/EditorChart.cs` (modeled on `LocalDependencies\osu\osu.Game\Screens\Edit\EditorBeatmap.cs`, heavily trimmed)
- Test: `Garbus.Game.Tests/Editor/TestEditorChart.cs`

**Interfaces:**
- Consumes: `GarbusChart` (hit objects list, `ControlPointInfo`, `Metadata`, `PreviewTime`), `GarbusHitObject.ApplyDefaults()`, `BindableList<T>`.
- Produces — this is the contract every later task codes against:

```csharp
namespace Garbus.Game.Edit;

public partial class EditorChart : TransactionalCommitComponent
{
    public readonly GarbusChart Chart;
    public event Action<GarbusHitObject>? HitObjectAdded;
    public event Action<GarbusHitObject>? HitObjectRemoved;
    public event Action<GarbusHitObject>? HitObjectUpdated;
    public readonly BindableList<GarbusHitObject> SelectedHitObjects = new BindableList<GarbusHitObject>();
    public IReadOnlyList<GarbusHitObject> HitObjects { get; }           // time-ordered view
    public ControlPointInfo ControlPointInfo => Chart.ControlPointInfo;
    public ChartMetadata Metadata => Chart.Metadata;

    public EditorChart(GarbusChart chart);
    public void Add(GarbusHitObject h);            // insert time-ordered, ApplyDefaults, event, SaveState
    public void AddRange(IEnumerable<GarbusHitObject> hs);
    public void Remove(GarbusHitObject h);         // + deselect
    public void RemoveRange(IEnumerable<GarbusHitObject> hs);
    public void Update(GarbusHitObject h);         // re-run ApplyDefaults (regenerates nesteds), re-sort, HitObjectUpdated
    public void PerformOnSelection(Action<GarbusHitObject> action);  // wraps in BeginChange/EndChange, Updates each
    public void Clear();
    // from TransactionalCommitComponent: BeginChange(), EndChange(), SaveState()
}
```

- [ ] **Step 1: Write the failing tests** — add fires event + applies defaults; `Update` regenerates nested objects; removal deselects; list stays time-ordered:

```csharp
[TestFixture]
public class TestEditorChart
{
    private EditorChart chart = null!;

    [SetUp]
    public void SetUp() => chart = new EditorChart(new GarbusChart());

    [Test]
    public void TestAddFiresEventAndAppliesDefaults()
    {
        GarbusHitObject? added = null;
        chart.HitObjectAdded += h => added = h;
        var hold = new HoldNote { StartTime = 1000, AngleDeg = 90, Duration = 500 };
        chart.Add(hold);
        Assert.That(added, Is.SameAs(hold));
        Assert.That(hold.NestedHitObjects, Is.Not.Empty); // head generated by ApplyDefaults
        Assert.That(chart.HitObjects, Has.Count.EqualTo(1));
    }

    [Test]
    public void TestUpdateRegeneratesNested()
    {
        var hold = new HoldNote { StartTime = 1000, AngleDeg = 90, Duration = 500 };
        chart.Add(hold);
        var headBefore = hold.NestedHitObjects.Single();
        hold.Duration = 900;
        chart.Update(hold);
        Assert.That(hold.NestedHitObjects.Single(), Is.Not.SameAs(headBefore));
    }

    [Test]
    public void TestRemoveDeselects()
    {
        var note = new CardinalNote { StartTime = 0, AngleDeg = 0 };
        chart.Add(note);
        chart.SelectedHitObjects.Add(note);
        chart.Remove(note);
        Assert.That(chart.SelectedHitObjects, Is.Empty);
    }

    [Test]
    public void TestHitObjectsStayTimeOrdered()
    {
        chart.Add(new CardinalNote { StartTime = 2000 });
        chart.Add(new CardinalNote { StartTime = 1000 });
        Assert.That(chart.HitObjects.Select(h => h.StartTime), Is.Ordered);
    }
}
```

Note: if `GarbusHitObject.ApplyDefaults()` doesn't regenerate nesteds on re-call (check the actual implementation — BAC's `CreateNestedHitObjects` pattern), `Update` must clear nesteds first, the way osu's `EditorBeatmap.Update` → `ApplyDefaultsToHitObject` does. Verify against `Gameplay/Objects/HitObject.cs` and adapt.

- [ ] **Step 2: Run to verify failure** — `--filter TestEditorChart` → FAIL.
- [ ] **Step 3: Implement** `TransactionalCommitComponent` (vendor) + `EditorChart` per the contract. Keep the osu behavioural detail: mutations between `BeginChange`/`EndChange` produce ONE state save at the outermost `EndChange`; mutations outside a transaction save immediately.
- [ ] **Step 4: Run** — filtered PASS, full suite green.
- [ ] **Step 5: Commit** — `git add -A && git commit -m "EditorChart: transactional chart editing model"`

### Task 6: Undo/redo — EditorChangeHandler + GarbusChartChangeHandler

**Files:**
- Create: `Garbus.Game/Edit/IEditorChangeHandler.cs`
- Create: `Garbus.Game/Edit/EditorChangeHandler.cs` (from `LocalDependencies\osu\osu.Game\Screens\Edit\EditorChangeHandler.cs`, ~130 L — the abstract snapshot-stack base)
- Create: `Garbus.Game/Edit/GarbusChartChangeHandler.cs`
- Modify: `Garbus.Game/Charts/Format/GarbusChartSerializer.cs` (add `internal static string EncodeHitObject(GarbusHitObject)` beside `Encode`, for the per-object diff)
- Test: `Garbus.Game.Tests/Editor/TestChangeHandler.cs`

**Interfaces:**
- Consumes: `EditorChart` (Task 5 — the handler hooks its `SaveState`/transaction flow per the vendored base's pattern), `GarbusChartSerializer` (Task 1).
- Produces:

```csharp
public interface IEditorChangeHandler
{
    void BeginChange(); void EndChange(); void SaveState();
}

public abstract partial class EditorChangeHandler : TransactionalCommitComponent, IEditorChangeHandler
{
    public readonly Bindable<bool> CanUndo, CanRedo;
    public string CurrentStateHash { get; }   // SHA256 of current serialized state — dirty tracking
    public void SaveState();
    public void Undo();  public void Redo();
    protected abstract void WriteCurrentStateToStream(MemoryStream stream);
    protected abstract void ApplyStateChange(byte[] previousState, byte[] newState);
}

public partial class GarbusChartChangeHandler : EditorChangeHandler
{
    public GarbusChartChangeHandler(EditorChart editorChart);
    // WriteCurrentStateToStream: UTF8 bytes of GarbusChartSerializer.Encode(editorChart.Chart)
    // ApplyStateChange: decode newState; diff hit objects by per-object encoded-JSON identity against
    //   current; remove/add only the delta through editorChart.Remove/Add (events fire; untouched
    //   objects keep their references); overwrite Metadata fields, PreviewTime, and ControlPointInfo
    //   contents directly.
}
```

- [ ] **Step 1: Write the failing tests:**

```csharp
[TestFixture]
public class TestChangeHandler
{
    private EditorChart chart = null!;
    private GarbusChartChangeHandler handler = null!;

    [SetUp]
    public void SetUp()
    {
        chart = new EditorChart(new GarbusChart());
        handler = new GarbusChartChangeHandler(chart);
    }

    [Test]
    public void TestUndoRedoPlacement()
    {
        chart.Add(new CardinalNote { StartTime = 1000, AngleDeg = 90 });
        Assert.That(handler.CanUndo.Value, Is.True);

        handler.Undo();
        Assert.That(chart.HitObjects, Is.Empty);
        Assert.That(handler.CanRedo.Value, Is.True);

        handler.Redo();
        Assert.That(chart.HitObjects, Has.Count.EqualTo(1));
        Assert.That(((CardinalNote)chart.HitObjects[0]).AngleDeg, Is.EqualTo(90));
    }

    [Test]
    public void TestTransactionIsOneUndoStep()
    {
        chart.BeginChange();
        chart.Add(new CardinalNote { StartTime = 0 });
        chart.Add(new CardinalNote { StartTime = 500 });
        chart.EndChange();

        handler.Undo();
        Assert.That(chart.HitObjects, Is.Empty);
    }

    [Test]
    public void TestUndoPreservesUntouchedObjects()
    {
        var keep = new CardinalNote { StartTime = 0 };
        chart.Add(keep);
        chart.Add(new CardinalNote { StartTime = 500 });
        handler.Undo(); // removes only the second add
        Assert.That(chart.HitObjects.Single(), Is.SameAs(keep)); // same reference — diff didn't recreate it
    }

    [Test]
    public void TestMetadataUndo()
    {
        chart.BeginChange();
        chart.Metadata.Title = "changed";
        chart.SaveState();
        chart.EndChange();
        handler.Undo();
        Assert.That(chart.Metadata.Title, Is.Empty);
    }

    [Test]
    public void TestStateHashChangesWithEdits()
    {
        string before = handler.CurrentStateHash;
        chart.Add(new CardinalNote { StartTime = 0 });
        Assert.That(handler.CurrentStateHash, Is.Not.EqualTo(before));
    }
}
```

- [ ] **Step 2: Run to verify failure** — `--filter TestChangeHandler` → FAIL.
- [ ] **Step 3: Implement.** Vendor the base faithfully (50-state cap, hash, bindables). For `ApplyStateChange`'s diff: encode each current hit object via `EncodeHitObject` and match against the target list's encodings; unmatched-current → `Remove`, unmatched-target → `Add`. This mirrors osu's `LegacyEditorBeatmapPatcher` object-diff idea without the legacy encoder.
- [ ] **Step 4: Run** — filtered PASS, full suite green.
- [ ] **Step 5: Commit** — `git add -A && git commit -m "Undo/redo: serializer-snapshot change handler"`

---

## Milestone C — editor shell & main menu

### Task 7: GarbusEditor shell — tabs, menu bar, dialog overlay

**Files:**
- Create: `Garbus.Game/Edit/Screens/EditorTab.cs`, `Garbus.Game/Edit/Screens/EditorTabScreen.cs`, `Garbus.Game/Edit/Screens/GarbusEditor.cs`
- Create: `Garbus.Game/Edit/Screens/Dialogs/ConfirmDialog.cs`
- Create (stubs, filled by later tasks): `Garbus.Game/Edit/Screens/ComposeTab.cs`, `SetupTab.cs`, `TimingTab.cs`, `VerifyTab.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneEditorShell.cs`

**Interfaces:**
- Consumes: `ChartFile` (Task 2), `EditorClock` (4), `EditorChart` (5), `GarbusChartChangeHandler` (6), `BindableBeatDivisor` (3), framework `BasicMenu`, `BasicTabControl`, `TrackVirtual`.
- Produces:

```csharp
public enum EditorTab { Setup, Compose, Timing, Verify }

/// <summary>Base for the four tab screens: shown/hidden by the shell, never unloaded.</summary>
public abstract partial class EditorTabScreen : VisibilityContainer
{
    protected override void PopIn() => this.FadeIn(200);
    protected override void PopOut() => this.FadeOut(200);
}

public partial class GarbusEditor : Screen
{
    public GarbusEditor(ChartFile chartFile);
    public readonly Bindable<EditorTab> Tab = new Bindable<EditorTab>(EditorTab.Compose);
    public ChartFile ChartFile { get; }
    public bool HasUnsavedChanges { get; }     // changeHandler.CurrentStateHash != hashAtLastSave
    public void Save();                        // Save As flow when FilePath == null
    public void SaveAs();                      // directory selector + filename textbox in a dialog
    /// <summary>Swap the clock's track (called by Setup when audio is assigned). Falls back to TrackVirtual(60000).</summary>
    public void ReloadTrack();
    // caches via DI for everything below it:
    //   EditorClock, EditorChart, GarbusChartChangeHandler (+ as IEditorChangeHandler),
    //   BindableBeatDivisor, GarbusEditor itself, ChartFile
}

public partial class ConfirmDialog : VisibilityContainer   // modal overlay
{
    public ConfirmDialog(string message, params (string label, Action action)[] buttons);
    public static ConfirmDialog SaveDiscardCancel(Action save, Action discard);
}
```

Layout (all `Basic*`/plain containers): top bar 40px — `BasicMenu` (File/Edit/View/Timing, horizontal) left, `BasicTabControl<EditorTab>` right; content area = the four `EditorTabScreen`s in a container, visibility driven by `Tab`; bottom bar 60px placeholder container (filled in Task 17). Menu items this task: File = New/Open…/Save/Save As…/Exit (New/Open push confirmation via `ConfirmDialog` when `HasUnsavedChanges`; actual open flow is Task 8's); Edit = Undo/Redo wired to the change handler with enabled-state bound to `CanUndo`/`CanRedo`; View/Timing menus = created empty here (items land in Tasks 17 and 23). Hotkeys: Ctrl+S = Save, Ctrl+Z = Undo, Ctrl+Shift+Z / Ctrl+Y = Redo. Track: `ChartFile.GetTrackStore(audioManager)?.Get(metadata.AudioFile)` else `new TrackVirtual(60000)` (`audioManager` = the BDL-resolved `AudioManager`).

- [ ] **Step 1: Write the failing tests:**

```csharp
[TestFixture]
public partial class TestSceneEditorShell : GarbusTestScene
{
    private GarbusEditor editor = null!;

    [SetUp]
    public new void SetUp() => Schedule(() =>
    {
        var chart = new GarbusChart();
        chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        Child = new ScreenStack(editor = new GarbusEditor(new ChartFile(chart))) { RelativeSizeAxes = Axes.Both };
    });

    [Test]
    public void TestTabSwitching()
    {
        AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
        AddStep("switch to setup", () => editor.Tab.Value = EditorTab.Setup);
        AddUntilStep("setup visible", () => editor.ChildrenOfType<SetupTab>().Single().State.Value == Visibility.Visible);
        AddUntilStep("compose hidden", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Hidden);
    }

    [Test]
    public void TestDirtyTracking()
    {
        AddAssert("clean at start", () => !editor.HasUnsavedChanges);
        AddStep("add object", () => editor.EditorChart.Add(new CardinalNote { StartTime = 1000 }));
        AddAssert("dirty", () => editor.HasUnsavedChanges);
        AddStep("save", () => editor.Save());
        AddAssert("clean again", () => !editor.HasUnsavedChanges);
    }
}
```

For `TestDirtyTracking`: expose `public EditorChart EditorChart { get; }` on `GarbusEditor` (it owns it anyway), and construct the `ChartFile` pre-saved to a temp path in this test's `SetUp` so `Save()` has a target.

- [ ] **Step 2: Run to verify failure** — `--filter TestSceneEditorShell` → FAIL.
- [ ] **Step 3: Implement** per the Produces block. Keep `GarbusEditor` under ~300 lines by delegating: menu construction in a `createMenuBar()` method, DI caching via `CreateChildDependencies`.
- [ ] **Step 4: Run** — filtered PASS, full suite green.
- [ ] **Step 5: Commit** — `git add -A && git commit -m "Editor shell: tabs, menu bar, dirty tracking, dialogs"`

### Task 8: MainMenuScreen + open/new/save-as flows

**Files:**
- Create: `Garbus.Game/Screens/MainMenuScreen.cs`
- Create: `Garbus.Game/Edit/Screens/Dialogs/SaveAsDialog.cs` (BasicDirectorySelector + filename BasicTextBox + Save/Cancel)
- Create: `Garbus.Game/Edit/Screens/Dialogs/OpenChartDialog.cs` (BasicFileSelector with `validFileExtensions: new[] { ".garbus" }`)
- Modify: `Garbus.Game/GarbusGame.cs` — boot into `MainMenuScreen` instead of `PlayScreen`
- Test: `Garbus.Game.Tests/Editor/TestSceneMainMenu.cs`

**Interfaces:**
- Consumes: `ChartFile.Load/Save` (2), `GarbusEditor` (7), existing `PlayScreen`, `ChartStore` (bundled test chart).
- Produces: `MainMenuScreen : Screen` with three `BasicButton`s — **Play** (push `new PlayScreen()`), **New Chart** (push `GarbusEditor` with `new ChartFile(emptyChart())` — empty chart gets one default `TimingControlPoint { BeatLength = 500 }` at 0 so beat snap works pre-timing), **Open Chart** (show `OpenChartDialog`; on pick, `ChartFile.Load(path)` → push editor; decode failure shows a `ConfirmDialog` with the error, no crash). `GarbusEditor.SaveAs()` uses `SaveAsDialog`; `Save()` delegates to it when `FilePath == null`. Editor exit with unsaved changes intercepts `OnExiting` → `ConfirmDialog.SaveDiscardCancel`.

- [ ] **Step 1: Write the failing tests** — menu buttons exist and push the right screens; editor exit-dirty shows the dialog; save-as writes the file:

```csharp
[TestFixture]
public partial class TestSceneMainMenu : GarbusTestScene
{
    private ScreenStack stack = null!;

    [SetUp]
    public new void SetUp() => Schedule(() => Child = stack = new ScreenStack(new MainMenuScreen()) { RelativeSizeAxes = Axes.Both });

    [Test]
    public void TestNewChartOpensEditor()
    {
        AddStep("click new chart", () => this.ChildrenOfType<BasicButton>().Single(b => b.Text == "New Chart").TriggerClick());
        AddUntilStep("editor pushed", () => stack.CurrentScreen is GarbusEditor);
        AddAssert("chart has default timing point", () =>
            ((GarbusEditor)stack.CurrentScreen).ChartFile.Chart.ControlPointInfo.TimingPoints.Any());
    }

    [Test]
    public void TestExitDirtyPrompts()
    {
        AddStep("click new chart", () => this.ChildrenOfType<BasicButton>().Single(b => b.Text == "New Chart").TriggerClick());
        AddUntilStep("editor pushed", () => stack.CurrentScreen is GarbusEditor);
        AddStep("dirty the chart", () => /* resolve EditorChart, Add a CardinalNote */ dirtyEditor());
        AddStep("try exit", () => stack.Exit());
        AddUntilStep("dialog shown", () => this.ChildrenOfType<ConfirmDialog>().Any(d => d.State.Value == Visibility.Visible));
    }
}
```

(`dirtyEditor()` = helper resolving the editor's `EditorChart` and adding a `CardinalNote`; write it concretely.)

- [ ] **Step 2: Run to verify failure** — `--filter TestSceneMainMenu` → FAIL.
- [ ] **Step 3: Implement** per Produces. `GarbusGame` boot change is one line (`ScreenStack.Push(new MainMenuScreen())` where `PlayScreen` was pushed).
- [ ] **Step 4: Run** — filtered PASS; also `--filter TestScenePlayScreen` still green (boot change must not break it — it constructs `PlayScreen` directly). Full suite green.
- [ ] **Step 5: Manual smoke:** `dotnet run --project Garbus.Desktop` — menu appears; Play works as before; New Chart opens the editor shell with tabs; exit prompts when dirtied (place nothing yet — edit metadata once Task 20 lands; for now dirty via undo-able tab default, or just verify clean exit). Note observations in the commit message if anything is off.
- [ ] **Step 6: Commit** — `git add -A && git commit -m "Main menu and chart open/new/save-as flows"`

---

## Milestone D — compose editing (vendored blueprint stack + BAC port)

Vendor tasks 9–13 have no meaningful behaviour to test in isolation (abstract bases, no concrete subclass yet) — their gate is: builds clean, full existing suite stays green, and the **Interfaces block matches what Tasks 14–16 consume**. The behavioural tests land in Tasks 14–16 and exercise the whole stack. Do not skip the build gate between tasks.

### Task 9: Vendor ScrollingHitObjectContainer + ScrollingPlayfield

**Files:**
- Create: `Garbus.Game/Gameplay/UI/Scrolling/ScrollingHitObjectContainer.cs` (from `LocalDependencies\osu\osu.Game\Rulesets\UI\Scrolling\ScrollingHitObjectContainer.cs`)
- Create: `Garbus.Game/Gameplay/UI/Scrolling/ScrollingPlayfield.cs` (from `...\ScrollingPlayfield.cs`)
- Create: `Garbus.Game/Gameplay/UI/Scrolling/IScrollingInfo.cs` + `ScrollingDirection.cs` if Phase 2's `GarbusScrollingInfo` didn't already extract these (check first — grep `IScrollingInfo` in `Garbus.Game`)

**Interfaces:**
- Consumes: existing `HitObjectContainer`, `IScrollAlgorithm`/`ConstantScrollAlgorithm`, `DrawableHitObject`.
- Produces (the editor playfield and every blueprint use these): `ScrollingHitObjectContainer : HitObjectContainer` with `ScreenSpacePositionAtTime(double time)`, `TimeAtScreenSpacePosition(Vector2)`, `PositionAtTime(double time, double currentTime)`, `LengthAtTime(double start, double end)`; `ScrollingPlayfield : Playfield` resolving `IScrollingInfo` with `Direction` bindable and `TimeRange` bindable.
- Trims: mods/`IApplicableToScrollingInfo`, per-hit-object initial-state cache complexity may be kept as-is (it's framework-only code). Direction support can be trimmed to `Down` if branches on Up/Left/Right pull anything osu-specific (they don't — keep all four, it's pure math).
- If BAC's `GarbusScrollingHitObjectContainer` (gameplay) duplicates helpers these provide, do NOT refactor gameplay — the editor container is separate.

- [ ] **Step 1: Vendor both files** with headers + adaptation lines, namespace `Garbus.Game.Gameplay.UI.Scrolling`.
- [ ] **Step 2: Build** — `dotnet build Garbus.Desktop.slnf` → 0 errors/warnings introduced.
- [ ] **Step 3: Full suite** — green.
- [ ] **Step 4: Commit** — `git add -A && git commit -m "Vendor ScrollingHitObjectContainer/ScrollingPlayfield for the editor"`

### Task 10: Vendor SnapResult + placement/selection blueprint bases

**Files:**
- Create: `Garbus.Game/Edit/Compose/SnapResult.cs` (from `LocalDependencies\osu\osu.Game\Rulesets\Edit\SnapResult.cs` — tiny)
- Create: `Garbus.Game/Edit/Compose/PlacementBlueprint.cs` + `HitObjectPlacementBlueprint.cs` (from `...\osu.Game\Rulesets\Edit\`)
- Create: `Garbus.Game/Edit/Compose/SelectionBlueprint.cs` (from `...\osu.Game\Rulesets\Edit\SelectionBlueprint.cs`) + `HitObjectSelectionBlueprint.cs`

**Interfaces (exactly what BAC's ported blueprints consume — see `BacPlacementBlueprint.cs`/`BacSelectionBlueprint.cs` in the source repo):**

```csharp
public class SnapResult
{
    public Vector2 ScreenSpacePosition;
    public double? Time;
    public readonly Playfield? Playfield;
    public SnapResult(Vector2 screenSpacePosition, double? time, Playfield? playfield = null);
}

public abstract partial class HitObjectPlacementBlueprint : CompositeDrawable
{
    public GarbusHitObject HitObject { get; }
    public PlacementState PlacementActive { get; }   // enum: Waiting, Active, Finished
    protected HitObjectPlacementBlueprint(GarbusHitObject hitObject);
    protected void BeginPlacement(bool commitStart = false);
    public void EndPlacement(bool commit);
    public virtual SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime);
    public virtual bool ReplacesExistingObject(GarbusHitObject existing);  // default: same StartTime
    // on commit: EditorChart.Add(HitObject) inside a change-handler transaction; removes replaced
    // objects (ReplacesExistingObject == true) first; auto-seek to the placed object when the
    // AutoSeekOnPlacement bindable (Task 17's View toggle; a plain cached Bindable<bool>) is on.
}

public abstract partial class SelectionBlueprint<T> : CompositeDrawable, IStateful<SelectionState>
{
    public readonly T Item;
    public event Action<SelectionBlueprint<T>>? Selected, Deselected;
    public SelectionState State { get; set; }
    public bool IsSelected { get; }
    public virtual Vector2 ScreenSpaceSelectionPoint { get; }
    public virtual Quad SelectionQuad { get; }
    public virtual bool HandleQuickDeletion { get; }
    protected virtual void OnSelected(); protected virtual void OnDeselected();
}

public abstract partial class HitObjectSelectionBlueprint : SelectionBlueprint<GarbusHitObject>
{
    public DrawableHitObject? DrawableObject { get; }  // resolved from the composer's playfield
}
public abstract partial class HitObjectSelectionBlueprint<T> : HitObjectSelectionBlueprint where T : GarbusHitObject
{
    public new T Item { get; }  // typed shadow, osu's exact pattern
}
```

- Trims: `IPlacementHandler` indirection may be simplified to direct `EditorChart` calls IF osu's version routes through the composer — check the source first and keep osu's shape where it doesn't cost extra types. Sample banks / combo handling in placement: strip. `SelectionBlueprint`'s `IStateful` is framework — keep.

- [ ] **Step 1: Vendor the files** per above; namespace `Garbus.Game.Edit.Compose`.
- [ ] **Step 2: Build** → clean. **Step 3:** full suite green. **Step 4: Commit** — `git add -A && git commit -m "Vendor blueprint bases and SnapResult"`

### Task 11: Vendor SelectionHandler + SelectionBox + EditorSelectionHandler

**Files:**
- Create: `Garbus.Game/Edit/Compose/MoveSelectionEvent.cs` (from `...\osu.Game\Screens\Edit\Compose\Components\MoveSelectionEvent.cs`)
- Create: `Garbus.Game/Edit/Compose/SelectionHandler.cs` (from `...\SelectionHandler.cs`)
- Create: `Garbus.Game/Edit/Compose/EditorSelectionHandler.cs` (from `...\EditorSelectionHandler.cs`)
- Create: `Garbus.Game/Edit/Compose/SelectionBox.cs` + `SelectionBoxControl.cs`/handle pieces as needed (from `...\SelectionBox*.cs` — copy only what compiles without the scale/rotate handles BAC doesn't use; BAC hides the box for sliders and uses plain move elsewhere, so **skip SelectionBoxRotationHandle/ScaleHandle and their operations entirely**)
- Create: `Garbus.Game/Edit/Compose/TernaryStateToggleMenuItem.cs` (+ minimal `TernaryState` enum + `OsuMenuItem`-equivalent plain `MenuItem` subclass, from `...\osu.Game\Graphics\UserInterface\` — trim to framework `MenuItem`)

**Interfaces (what `BacSelectionHandler` overrides/calls):**

```csharp
public partial class SelectionHandler<T> : CompositeDrawable, IHasContextMenu
{
    public readonly BindableList<T> SelectedItems;
    public IReadOnlyList<SelectionBlueprint<T>> SelectedBlueprints { get; }
    public SelectionBox SelectionBox { get; }
    public virtual bool HandleMovement(MoveSelectionEvent<T> moveEvent);
    protected virtual void DeleteItems(IEnumerable<T> items);
    protected virtual IEnumerable<MenuItem> GetContextMenuItemsForSelection(IReadOnlyList<SelectionBlueprint<T>> selection);
    protected virtual void OnSelectionChanged();
    public virtual MenuItem[] ContextMenuItems { get; }
    // + the internal selection bookkeeping BlueprintContainer drives:
    internal void HandleSelected(SelectionBlueprint<T> blueprint); /* etc. — keep osu's internal surface intact */
}

public partial class EditorSelectionHandler : SelectionHandler<GarbusHitObject>
{
    [Resolved] protected EditorChart EditorChart { get; private set; }
    [Resolved] protected IEditorChangeHandler? ChangeHandler { get; private set; }
    // DeleteItems override: EditorChart.RemoveRange inside BeginChange/EndChange
    // SelectionBox updates from SelectedBlueprints' SelectionQuads (union of AABBFloat)
}
```

- Trims: sample/bank/new-combo ternary states in `EditorSelectionHandler` (osu's is mostly that — what remains is thin), rotation/scale support, `OsuContextMenu` → `BasicContextMenuContainer`-compatible plain `MenuItem`s.

- [ ] **Step 1: Vendor** per above. **Step 2:** build clean. **Step 3:** full suite green. **Step 4: Commit** — `git add -A && git commit -m "Vendor SelectionHandler/SelectionBox stack"`

### Task 12: Vendor BlueprintContainer + ComposeBlueprintContainer + DragBox

**Files:**
- Create: `Garbus.Game/Edit/Compose/BlueprintContainer.cs` (from `...\osu.Game\Screens\Edit\Compose\Components\BlueprintContainer.cs`, ~700 L — the big one)
- Create: `Garbus.Game/Edit/Compose/ComposeBlueprintContainer.cs` (from `...\ComposeBlueprintContainer.cs`)
- Create: `Garbus.Game/Edit/Compose/DragBox.cs` + `ScrollingDragBox.cs` (from `...\DragBox.cs`, `...\ScrollingDragBox.cs`)

**Interfaces (what `BacBlueprintContainer` subclasses — see its source):**

```csharp
public partial class BlueprintContainer<T> : CompositeDrawable  // osu's generic base
{
    protected SelectionHandler<T> SelectionHandler { get; }
    protected virtual SelectionHandler<T> CreateSelectionHandler();
    protected virtual SelectionBlueprint<T>? CreateBlueprintFor(T item);
    protected virtual DragBox CreateDragBox();
    protected virtual bool TryMoveBlueprints(DragEvent e, IList<(SelectionBlueprint<T> blueprint, Vector2[] originalSnapPositions)> blueprints);
    // selection input flow: click-select via blueprint.IsHovered, drag-box select via
    //   ScreenSpaceSelectionPoint, ctrl+click multi-select, ctrl+A select-all, Delete key → DeleteItems
}

public partial class ComposeBlueprintContainer : BlueprintContainer<GarbusHitObject>
{
    public HitObjectComposer Composer { get; }
    public ComposeBlueprintContainer(HitObjectComposer composer);
    public virtual HitObjectSelectionBlueprint? CreateHitObjectBlueprintFor(GarbusHitObject hitObject);
    public HitObjectPlacementBlueprint? CurrentPlacement { get; }
    protected void ApplySnapResultTime(SnapResult result, double referenceTime);  // shifts selection times by snap delta
    // placement lifecycle: watches the composer's active CompositionTool, creates its placement
    //   blueprint, drives UpdateTimeAndPosition from mouse moves, commits on the blueprint's say-so
    // subscribes to EditorChart.HitObjectAdded/Removed to create/remove selection blueprints
}
```

- Trims: paste/duplicate placement (comes with clipboard in Task 23 — leave osu's hooks compiled but inert if cheap, else strip and re-add), `OsuConfigManager` "limit distance snap" etc., sample point pieces.
- **Note:** `ApplySnapResultTime` and the `TryMoveBlueprints` tuple signature must match `BacBlueprintContainer`'s expectations verbatim (see Interfaces above and the BAC source) — that file ports with near-zero edits in Task 16.

- [ ] **Step 1: Vendor** per above. **Step 2:** build clean. **Step 3:** full suite green. **Step 4: Commit** — `git add -A && git commit -m "Vendor BlueprintContainer stack and drag box"`

### Task 13: Vendor composer bases + toolbox components

**Files:**
- Create: `Garbus.Game/Edit/Compose/HitObjectComposer.cs` + `ScrollingHitObjectComposer.cs` (from `...\osu.Game\Rulesets\Edit\HitObjectComposer.cs` ~623 L, heavily reworked — see below)
- Create: `Garbus.Game/Edit/Compose/CompositionTool.cs` (from `...\osu.Game\Rulesets\Edit\Tools\CompositionTool.cs` + `SelectTool`)
- Create: `Garbus.Game/Edit/Compose/EditorToolboxGroup.cs`, `ExpandingToolboxContainer.cs`, `EditorRadioButtonCollection.cs`, `RadioButton.cs` (from `...\osu.Game\Screens\Edit\Components\` — swap `OsuButton`→`BasicButton`, OsuColour→hardcoded, keep the radio semantics: one selected, `Select()` API, item list)
- Create: `Garbus.Game/Edit/Compose/BeatSnapGrid.cs` (from `...\osu.Game\Rulesets\Edit\BeatSnapGrid.cs` — draws divisor lines into a target container; depends on EditorClock + BindableBeatDivisor + ControlPointInfo, all present)

**The one structural rework (biggest deviation from osu — get this right):** Garbus has no `DrawableRuleset`. The vendored `HitObjectComposer` therefore hosts the playfield directly:

```csharp
[Cached]
public abstract partial class HitObjectComposer : CompositeDrawable
{
    [Resolved] protected EditorChart EditorChart { get; private set; }
    [Resolved] protected EditorClock EditorClock { get; private set; }
    public abstract Playfield Playfield { get; }
    protected abstract IReadOnlyList<CompositionTool> CompositionTools { get; }
    public readonly Bindable<CompositionTool?> ActiveTool;
    protected FillFlowContainer LeftToolbox { get; }    // ExpandingToolboxContainer
    protected FillFlowContainer RightToolbox { get; }
    protected abstract ComposeBlueprintContainer CreateBlueprintContainer();
    protected virtual BeatSnapGrid? CreateBeatSnapGrid() => null;
    public virtual SnapResult FindSnappedPositionAndTime(Vector2 screenSpacePosition);
    // layout: LeftToolbox | playfield-with-blueprint-overlay | RightToolbox
    // key handling: number keys select tools (index order, 1 = Select)
    // FindSnappedPositionAndTime: y→time via the playfield's ScrollingHitObjectContainer
    //   .TimeAtScreenSpacePosition, snapped to ControlPointInfo.GetClosestSnappedTime(time, divisor),
    //   back to screen y via ScreenSpacePositionAtTime — this reproduces the scrolling snap BAC's
    //   FindSnappedAngleTimeAndPosition builds on (including the recentre-x quirk it works around).
}

public abstract partial class ScrollingHitObjectComposer<T> : HitObjectComposer where T : GarbusHitObject
{
    // creates the concrete playfield via CreatePlayfield(), wires non-pooled drawable creation
    //   (PlayScreen's pattern) from EditorChart.HitObjectAdded/Removed, initial population from
    //   EditorChart.HitObjects, drawable refresh on HitObjectUpdated (remove + re-create)
    protected abstract Playfield CreatePlayfield();
    protected abstract DrawableHitObject? CreateDrawableRepresentation(T hitObject);
    public readonly Bindable<double> TimelineTimeRange;   // Task 17's zoom sync writes this;
                                                          // flows into the playfield's IScrollingInfo.TimeRange
}
```

- [ ] **Step 1: Vendor/rework** per above. Where osu's file is too entangled to copy (the DrawableRulesetWrapper parts), write the replacement fresh but keep osu's member names so ported BAC code compiles against it.
- [ ] **Step 2:** build clean. **Step 3:** full suite green. **Step 4: Commit** — `git add -A && git commit -m "Vendor composer bases, toolboxes, beat snap grid"`

### Task 14: Port EditorAngleMapping + editor playfield + editor drawables

**Files (all ported from `C:\Users\zachd\Code\BAC\osu.Game.Rulesets.BigAssCircle\Edit\`, `Bac*`→`Garbus*`, namespace `Garbus.Game.Edit`):**
- Create: `Garbus.Game/Edit/EditorAngleMapping.cs` (from `EditorAngleMapping.cs` — **verbatim port**, zero osu deps)
- Create: `Garbus.Game/Edit/GarbusEditorPlayfield.cs` (from `BacEditorPlayfield.cs` — `ScrollingPlayfield` from Task 9; its own cached `IScrollingInfo` with `ScrollingDirection.Down` + `TimeRange` bound from the composer's `TimelineTimeRange`)
- Create: `Garbus.Game/Edit/Drawables/*` (from `Edit/Drawables/*`: `EditorDrawableGarbusHitObject.cs`, per-type drawables, `EditorDrawableNestedStub.cs`, `EditorSpritePiece.cs`, `SliderPolylineVisual.cs`)
- Test: `Garbus.Game.Tests/Editor/TestEditorAngleMapping.cs`, `Garbus.Game.Tests/Editor/TestSceneEditorPlayfield.cs`

**Interfaces:**
- Consumes: Tasks 9/13 bases; existing gameplay `GarbusHitObject` types; textures from `Garbus.Resources/Textures`.
- Produces: `EditorAngleMapping` (identical public surface: `ANGLE_ORIGIN`, `GHOST_DEGREES`, `TOTAL_DEGREES`, `ToX`, `SnapX`, `GhostTwinX`, `MinimalDiff`, `NormalizeDeg`, `VisibleWrapCopies`), `GarbusEditorPlayfield : ScrollingPlayfield` (`HitObjectContainer` typed `ScrollingHitObjectContainer`, `UnderlayElements` container for the beat snap grid), all editor drawables.
- Port notes: BAC gotchas apply — nested drawables need `AddNestedHitObject` → nested container (or NRE in `OnKilled`); `SliderPolylineVisual` pools its `SmoothPath`s (never new-per-frame — framebuffer leak); keep the `GlobalStatistic` rebuild counter.

- [ ] **Step 1: Write failing tests** — `TestEditorAngleMapping`: port the angle-mapping assertions from BAC (`SnapX` wrap normalisation, `GhostTwinX` band membership, `MinimalDiff` shortest rotation — if BAC has no standalone test for these, write: `ToX(135) == 0` domain checks, `SnapX` on a ghost-band x stays in band, `MinimalDiff(350, 10) == 20`). `TestSceneEditorPlayfield`: construct playfield inside a minimal composer harness, add one `CardinalNote` at angle 90 / t=1000 via `EditorChart`, assert an `EditorDrawableCardinalNote` appears and its x-fraction ≈ `EditorAngleMapping.ToX(90)`; add a seam-crossing slider and assert `SliderPolylineVisual` renders 2 wrap copies (`ChildrenOfType<SmoothPath>` count, BAC's test pattern).
- [ ] **Step 2:** run filtered → FAIL. **Step 3:** port the files. **Step 4:** run filtered → PASS; full suite green. **Step 5: Commit** — `git add -A && git commit -m "Port editor playfield, angle mapping, editor drawables"`

### Task 15: Port composer, tools, placement blueprints

**Files (ported, `Bac*`→`Garbus*`):**
- Create: `Garbus.Game/Edit/GarbusHitObjectComposer.cs`, `Garbus.Game/Edit/GarbusSnapResult.cs`, `Garbus.Game/Edit/GarbusBeatSnapGrid.cs`
- Create: `Garbus.Game/Edit/Tools/*` (from `CardinalNoteCompositionTool.cs` + `BacCompositionTools.cs`; tool icons: plain text labels instead of OsuIcon/FontAwesome — visuals don't matter)
- Create: `Garbus.Game/Edit/Blueprints/*` placement half (from `BacPlacementBlueprint.cs`, `InstantPlacementBlueprint.cs`, `CardinalNotePlacementBlueprint.cs`, `ShoulderNotePlacementBlueprint.cs`, `HoldNotePlacementBlueprint.cs`, `SlamCenteredPlacementBlueprint.cs`, `SlamEdgePlacementBlueprint.cs`, `SliderPlacementBlueprint.cs`) + `Blueprints/Components/EditSquarePiece.cs`
- Modify: `Garbus.Game/Edit/Screens/ComposeTab.cs` — host the composer (timeline strip slot stays empty until Task 17)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs`

**Interfaces:**
- Consumes: everything above. `GarbusHitObjectComposer` keeps BAC's exact public surface: `BindableInt AngleSnap` (default 45, options 5/15/45/90), `FindSnappedAngleTimeAndPosition(Vector2) → SnapResult` (returning `GarbusSnapResult` carrying `AngleDeg`), `Playfield` (typed `GarbusEditorPlayfield`).
- Produces: clicking with a tool places objects into `EditorChart` — the full BAC placement semantics.
- Port note: BAC's composer resolved `EditorScreenWithTimeline` for zoom sync — replace with the `TimelineTimeRange` bindable (Task 13); the timeline writes it in Task 17. Until then time range holds a constant default (700 × timeline-zoom-equivalent; use 5000 ms).

- [ ] **Step 1: Write failing tests** — headless, `ManualInputManager`, BAC's `TestSceneBacEditor` placement patterns (auto-seek gotcha: placement seeks the clock to the placed object; wait for the seek before asserting screen positions):

```csharp
[Test]
public void TestPlaceCardinalNote()
{
    AddStep("select cardinal tool", () => composer.ChildrenOfType<RadioButton>() /* or keyboard: press Key.Number2 */);
    AddStep("move to angle 90, t≈1000 and click", () => { InputManager.MoveMouseTo(positionAtAngleTime(90, 1000)); InputManager.Click(MouseButton.Left); });
    AddUntilStep("note placed", () => editorChart.HitObjects.OfType<CardinalNote>().Any(n => n.AngleDeg == 90));
}
```

Cover: cardinal (angle snapped to 45°), hold (click-drag-release sets duration > 0), shoulder (click in left strip → `Side == Left`), both slams, slider (click body → click node at later time → right-click commits, ≥1 control point; node click at earlier time rejected). Port BAC's `positionAtAngle`/`screenPositionOf` helpers.

- [ ] **Step 2:** run filtered → FAIL. **Step 3:** port the files (minimal edits: namespaces, `EditorBeatmap`→`EditorChart`, `OsuColour.YellowDark`→`new Colour4(255, 196, 40, 255)`, tool icons→text). **Step 4:** filtered PASS; full suite green. **Step 5: Commit** — `git add -A && git commit -m "Port composer, tools, placement blueprints"`

### Task 16: Port selection blueprints + selection handler

**Files (ported):**
- Create: `Garbus.Game/Edit/GarbusBlueprintContainer.cs`, `Garbus.Game/Edit/GarbusSelectionHandler.cs`
- Create: `Garbus.Game/Edit/Blueprints/*` selection half (from `BacSelectionBlueprint.cs`, `OutlineSelectionBlueprint.cs`, `ShoulderNoteSelectionBlueprint.cs`, `HoldNoteSelectionBlueprint.cs`, `SliderSelectionBlueprint.cs`) + `Blueprints/Components/HoldNoteEndDragPiece.cs`, `NodeDragPiece.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: Tasks 11/12/15. `GarbusSelectionHandler` keeps: movement = x-delta → whole-degree rotation of every selected `IHasMutableAngle` (mod 360), slam anticlockwise `TernaryStateToggleMenuItem`, `SliderCountChip` overlay, hidden `SelectionBox` for slider-only selections.
- Produces: full BAC selection semantics — click/drag-box select, drag-rotate with snap, ghost-twin hit-testing, path-precise slider selection (outline + node handles only), T-key node insertion on selected slider, hold head/tail drag handles calling `EditorChart.Update`.

- [ ] **Step 1: Write failing tests** — port BAC's coverage: select note by click; drag rotates by snapped increments; select via ghost twin; slider selectable only on polyline/nodes (click inside AABB-but-off-path falls through); T inserts a time-ordered node; delete key removes selection; undo restores it (change-handler integration proof).
- [ ] **Step 2:** run filtered → FAIL. **Step 3:** port. **Step 4:** filtered PASS; full suite green. **Step 5: Commit** — `git add -A && git commit -m "Port selection blueprints and selection handler"`

---

## Milestone E — compose chrome

### Task 17: Top timeline strip + zoom sync + View timeline toggles

**Files:**
- Create: `Garbus.Game/Edit/Screens/Timeline/ZoomableScrollContainer.cs` (from `LocalDependencies\osu\osu.Game\Screens\Edit\Compose\Components\Timeline\ZoomableScrollContainer.cs` — framework-level zoom/pan logic, vendor with header)
- Create: `Garbus.Game/Edit/Screens/Timeline/TimelineStrip.cs`, `TimelineTickDisplay.cs`, `TimelineTimingChangeDisplay.cs`, `TimelineObjectMarkers.cs`, `CentreMarker.cs` (bespoke, modeled on osu's equivalents but rebuilt — they're ControlPointInfo-driven drawing code)
- Modify: `Garbus.Game/Edit/Screens/ComposeTab.cs` — timeline strip above the composer
- Modify: `Garbus.Game/Configuration/GarbusSetting.cs` + `GarbusConfigManager.cs` — add `EditorShowTimingChanges`, `EditorShowTicks`, `EditorWaveformOpacity`, `EditorAutoSeekOnPlacement`, `EditorContractSidebars` (defaults: true, true, 0.25, true, false)
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs` — View menu items wired to those config bindables
- Test: `Garbus.Game.Tests/Editor/TestSceneTimeline.cs`

**Interfaces:**
- Consumes: `EditorClock` (track, `TrackLength`, seek), `WaveformGraph` (framework; fed `Waveform` from the track's stream — `ChartFile` directory store; `TrackVirtual` = no waveform), `BindableBeatDivisor`, `ControlPointInfo`, `EditorChart.HitObjects` + events.
- Produces:

```csharp
public partial class TimelineStrip : ZoomableScrollContainer
{
    public readonly BindableFloat CurrentZoom;      // composer's TimelineTimeRange derives from this:
                                                    // TimelineTimeRange = EditorClock.TrackLength / CurrentZoom / 2   (BAC's exact formula)
    // content width ∝ TrackLength × zoom; playhead centred (CentreMarker); scroll position follows
    // EditorClock when playing; user scroll/drag seeks (beat-snapped on release, raw while dragging);
    // Ctrl+scroll and +/- buttons zoom
}
```

Layers inside, back to front: `WaveformGraph` (alpha = `EditorWaveformOpacity`), `TimelineTickDisplay` (beat lines coloured by divisor via `BindableBeatDivisor.GetDivisorForBeatIndex`, hidden when `EditorShowTicks` off), `TimelineTimingChangeDisplay` (red line per `TimingControlPoint`, hidden when toggle off), `TimelineObjectMarkers` (non-interactive: 4px dot at each object's time, widened bar for `IHasDuration`; rebuilds on chart events), `CentreMarker`.

- [ ] **Step 1: Write failing tests** — zoom writes composer time range (`TimelineTimeRange == TrackLength / zoom / 2`); click at fraction f seeks near `f × TrackLength` snapped; toggling `EditorShowTicks` hides the tick display; object markers appear when a note is added.
- [ ] **Step 2:** filtered → FAIL. **Step 3:** implement. **Step 4:** filtered PASS; full suite green. **Step 5: Commit** — `git add -A && git commit -m "Compose top timeline: waveform, ticks, markers, zoom-synced scroll speed"`

### Task 18: Bottom bar + transport keys

**Files:**
- Create: `Garbus.Game/Edit/Screens/BottomBar/TimeInfoDisplay.cs`, `SummaryTimeline.cs`, `PlaybackControl.cs`, `BottomBar.cs`
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs` — mount BottomBar; global key handling
- Test: `Garbus.Game.Tests/Editor/TestSceneBottomBar.cs`

**Interfaces:**
- Consumes: `EditorClock`, `EditorChart` (`ControlPointInfo`, `Chart.PreviewTime`), `BindableBeatDivisor`.
- Produces: `BottomBar` (fixed 60px, four columns 150 | flex | 220 | 90 — osu's proportions): `TimeInfoDisplay` (mm:ss.fff + BPM at playhead, updates per frame), `SummaryTimeline` (full-track strip: timing point ticks + preview point marker + progress; click/drag seeks, unsnapped), `PlaybackControl` (play/pause `BasicButton` + speed `BasicTabControl` 0.25/0.5/0.75/1.0 driving `EditorClock.AudioAdjustments` tempo), Test button (wired in Task 19).
- Key handling on `GarbusEditor` (only when no textbox focused): **Space** play/pause, **Z** seek 0, **X** play from start, **C** pause/resume, **V** seek end, **←/→** seek one divisor step (`SeekBackward/SeekForward(snapped: true)`), **↑/↓** change divisor (`BindableBeatDivisor.Next/Previous`), mouse wheel over the compose area seeks one divisor step (wheel-down = forward, osu convention).

- [ ] **Step 1: Write failing tests** — space toggles `clock.IsRunning`; speed 0.5 halves `AudioAdjustments` aggregate tempo; summary-timeline click seeks; arrow keys move by `beatLength / divisor`.
- [ ] **Step 2:** filtered → FAIL. **Step 3:** implement. **Step 4:** filtered PASS; full suite green. **Step 5: Commit** — `git add -A && git commit -m "Editor bottom bar and transport controls"`

### Task 19: Test mode

**Files:**
- Modify: `Garbus.Game/Screens/PlayScreen.cs` — add constructor `PlayScreen(GarbusChart chart, Track track, double startTime = 0)`; the parameterless path keeps loading the bundled chart
- Modify: `Garbus.Game/Edit/Screens/BottomBar/BottomBar.cs` + `GarbusEditor.cs` — Test button + F5
- Test: `Garbus.Game.Tests/Editor/TestSceneTestMode.cs`

**Interfaces:**
- Consumes: `GarbusChartSerializer` Encode→Decode as deep-clone, `ChartFile.GetTrackStore`, existing `PlayScreen` internals (`MasterGameplayClockContainer` takes a `Track` — CLAUDE.md Phase 2 notes).
- Produces: Test button/F5 → `this.Push(new PlayScreen(clonedChart, freshTrack, startTime))` where `clonedChart = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(editorChart.Chart))` (+ `ApplyDefaults`), `freshTrack` = new track instance from the chart directory store (never share the editor's track instance), `startTime` = `EditorClock.CurrentTime - 1500` clamped ≥ 0. On resume (`OnResuming`), the editor seeks to where gameplay ended (`PlayScreen` exposes `public double? ExitTime { get; }` set on exit). Trackless chart (still `TrackVirtual`): Test button disabled with tooltip-less greyed state.

- [ ] **Step 1: Write failing tests** — F5 pushes a `PlayScreen` whose chart has the same object count but zero shared references (`ReferenceEquals` false for first object); start time ≈ editor time − 1500; exiting play seeks editor clock to `ExitTime`; Test disabled when track is virtual.
- [ ] **Step 2:** filtered → FAIL. **Step 3:** implement. **Step 4:** filtered PASS; full suite green. **Step 5: Commit** — `git add -A && git commit -m "Editor test mode: play the in-memory chart"`

---

## Milestone F — Setup / Timing / Verify tabs, clipboard, finish

### Task 20: Setup tab

**Files:**
- Create: `Garbus.Game/Edit/Screens/Setup/FormRow.cs` (label + `BasicTextBox`, ~40 L), `MetadataSection.cs`, `DifficultySection.cs`, `ResourcesSection.cs`, `FileChooserRow.cs` (label + current filename + Choose `BasicButton` → `BasicFileSelector` in an overlay)
- Modify: `Garbus.Game/Edit/Screens/SetupTab.cs` — scrollable `FillFlowContainer` of the three sections
- Test: `Garbus.Game.Tests/Editor/TestSceneSetupTab.cs`

**Interfaces:**
- Consumes: `EditorChart.Metadata` (all 10 string fields), `IEditorChangeHandler` (transactions), `ChartFile.ImportResource/Directory` (Task 2), `GarbusEditor.ReloadTrack()` (Task 7).
- Produces:
  - **MetadataSection:** eight `FormRow`s — Title, Romanised Title, Artist, Romanised Artist, Charter, Chart Name, Source, Tags. On textbox commit (focus loss / enter): `changeHandler.BeginChange(); metadata.X = value; editorChart.SaveState(); changeHandler.EndChange();` — one undo step per field commit.
  - **DifficultySection:** heading + `SpriteText("No per-chart difficulty settings yet.")`.
  - **ResourcesSection:** two `FileChooserRow`s — Audio Track (extensions `.mp3/.ogg/.wav`), Background Image (`.jpg/.jpeg/.png`). When `ChartFile.Directory == null`: rows disabled + `SpriteText("Save the chart first to add resources.")`. On pick: `ImportResource(path)` → set `metadata.AudioFile`/`metadata.BackgroundFile` (transaction, as above) → for audio also `editor.ReloadTrack()`.

- [ ] **Step 1: Write failing tests** — typing a title + commit updates `Metadata.Title` and is one undo step; resources rows disabled for unsaved chart; after `Save` to temp dir + picking a file (drive the selector's `CurrentFile` programmatically), the file exists in the chart dir and `AudioFile` is set; `ReloadTrack` called (track no longer virtual — use a real short wav copied from test resources, or assert `metadata.AudioFile` set and track length changed).
- [ ] **Step 2:** filtered → FAIL. **Step 3:** implement. **Step 4:** filtered PASS; full suite green. **Step 5: Commit** — `git add -A && git commit -m "Setup tab: metadata, difficulty stub, resources"`

### Task 21: Timing tab

**Files:**
- Create: `Garbus.Game/Edit/Screens/Timing/TimingPointList.cs` (rows: offset / BPM / signature; select + add-at-playhead + delete buttons)
- Create: `Garbus.Game/Edit/Screens/Timing/TimingPointSettings.cs` (offset & BPM textboxes + nudge buttons, signature `BasicDropdown<int>` of 1–7 over 4)
- Create: `Garbus.Game/Edit/Screens/Timing/RepeatingButtonBehaviour.cs` (vendor from `LocalDependencies\osu\osu.Game\Screens\Edit\Timing\RepeatingButtonBehaviour.cs` — hold-to-repeat, framework-only)
- Create: `Garbus.Game/Edit/Screens/Timing/TapTimingControl.cs`, `TapButton.cs`, `MetronomeDisplay.cs`, `WaveformComparisonDisplay.cs` (modeled on osu's `Screens\Edit\Timing\` equivalents; rebuild UI on `Basic*`, keep osu's tap-BPM averaging + adjustment semantics — read the osu sources for the exact algorithms: tap intervals → rolling average BPM; "adjust offset/BPM" buttons that shift subsequent objects... **osu's object-shifting on timing change does NOT apply** — Garbus timing edits never move placed objects, keep it dumb)
- Create: `Garbus.Resources/Samples/Editor/metronome-tick.wav` + `metronome-downbeat.wav` — synthesize like Phase 2's hitsound (short sine burst, higher pitch for downbeat; a tiny C# generator script or Audacity — match the Phase 2 method)
- Modify: `Garbus.Game/Edit/Screens/TimingTab.cs` — GridContainer: list left (40%), settings + tap timing right
- Test: `Garbus.Game.Tests/Editor/TestSceneTimingTab.cs`

**Interfaces:**
- Consumes: `ControlPointInfo` (`TimingPoints`, `Add(time, point)`, `RemoveGroup`/removal API — check the vendored Phase 3 surface and use what exists), `EditorClock` (playhead, seek, `IsRunning` for metronome sync), `IEditorChangeHandler`.
- Produces: selecting a row seeks to it; Add inserts a `TimingControlPoint` at the playhead (BeatLength copied from the previous point, else 500); all edits transactional (undoable — `EditorChart.SaveState` after mutation); tap button averages the last 8 tap intervals into BPM and writes it to the selected point; metronome plays tick/downbeat on beats of the selected point's signature while the clock runs.

- [ ] **Step 1: Write failing tests** — add-at-playhead creates a point at (snapped) current time; editing BPM textbox to 180 sets `BeatLength ≈ 333.33`; deletion removes; undo restores the deleted point; 4 simulated taps 500ms apart → BPM 120 (drive `TapButton` handler directly with fake times — inject a clock or take timestamps as parameters for testability).
- [ ] **Step 2:** filtered → FAIL. **Step 3:** implement. **Step 4:** filtered PASS; full suite green. **Step 5: Commit** — `git add -A && git commit -m "Timing tab: point list, settings, tap timing, metronome"`

### Task 22: Verify tab

**Files:**
- Create: `Garbus.Game/Edit/Screens/Verify/ICheck.cs`, `Issue.cs`, `Checks/CheckAudioPresent.cs`, `Checks/CheckBackgroundPresent.cs`, `Checks/CheckObjectsBeyondTrackEnd.cs`, `Checks/CheckObjectsBeforeTimeZero.cs`
- Create: `Garbus.Game/Edit/Screens/Verify/IssueTable.cs`
- Modify: `Garbus.Game/Edit/Screens/VerifyTab.cs`
- Test: `Garbus.Game.Tests/Editor/TestChecks.cs`

**Interfaces:**

```csharp
public record Issue(double? Time, string Message, string CheckName);

public interface ICheck
{
    string Name { get; }
    IEnumerable<Issue> Run(CheckContext context);
}

/// <summary>Everything a check may inspect.</summary>
public record CheckContext(GarbusChart Chart, ChartFile ChartFile, double TrackLength);
```

- Checks: **CheckAudioPresent** — `Metadata.AudioFile` empty, or file missing from `ChartFile.Directory` → issue (no time). **CheckBackgroundPresent** — same for `BackgroundFile`. **CheckObjectsBeyondTrackEnd** — any object whose end time (`GetEndTime()` pattern — `IHasDuration` aware) > `TrackLength` → one issue per object at its time. **CheckObjectsBeforeTimeZero** — `StartTime < 0` (possible via timeline edge placement/drag) → issue per object.
- VerifyTab: Refresh `BasicButton` runs all checks over a fresh `CheckContext`; `IssueTable` = simple `FillFlowContainer` of clickable rows (time `mm:ss.fff` or "—", message, check name); row click → `EditorClock.SeekSmoothlyTo(issue.Time)` when it has one.

- [ ] **Step 1: Write failing tests** (pure, no scene needed) — each check: one violating fixture → exactly the expected issues; one clean fixture → empty. E.g. `CheckObjectsBeyondTrackEnd` with a note at 70000 and TrackLength 60000 → 1 issue at 70000.
- [ ] **Step 2:** filtered → FAIL. **Step 3:** implement. **Step 4:** filtered PASS; full suite green. **Step 5: Commit** — `git add -A && git commit -m "Verify tab: check framework and first four checks"`

### Task 23: Clipboard + Timing menu items

**Files:**
- Create: `Garbus.Game/Edit/EditorClipboard.cs`
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs` — Edit menu items enabled/wired; Timing menu items wired
- Test: `Garbus.Game.Tests/Editor/TestClipboard.cs`

**Interfaces:**
- Consumes: `GarbusChartSerializer.EncodeHitObject` (Task 6) or a small `EncodeHitObjects(IEnumerable<GarbusHitObject>) → string` / `DecodeHitObjects(string)` pair on the serializer (add them — JSON array of `HitObjectDto`, reusing the DTO mapping), `EditorChart`, `EditorClock`, `ConfirmDialog`, `ControlPointInfo.GetClosestSnappedTime`.
- Produces: `EditorClipboard` (`Bindable<string> Content`; `CanCut/CanCopy/CanPaste`): **Copy** = encode selection; **Cut** = copy + `RemoveRange` (transaction); **Paste** = decode, shift every object by `(snappedPlayheadTime − min(StartTime))`, `AddRange` + select the pasted set (one transaction); **Clone** (Ctrl+D) = copy+paste in one step at the playhead. Timing menu: **Set preview point to current time** → `Chart.PreviewTime = clock.CurrentTime` (transaction); **Snap all notes to current snap divisor** → `ConfirmDialog` → for every object `StartTime = ControlPointInfo.GetClosestSnappedTime(StartTime, divisor)` + `Update(h)` (one transaction; destructive-labelled).
- Hotkeys: Ctrl+X/C/V/D, guarded on selection non-empty (paste on clipboard non-empty).

- [ ] **Step 1: Write failing tests** — copy 2 notes → paste at playhead 4000 → 2 new objects, earliest at snapped 4000, relative offset preserved, originals intact, pasted set selected; cut removes originals; paste is one undo step; snap-all moves an off-grid note to the grid and is undoable; preview point set + undoable.
- [ ] **Step 2:** filtered → FAIL. **Step 3:** implement. **Step 4:** filtered PASS; full suite green. **Step 5: Commit** — `git add -A && git commit -m "Clipboard and Timing menu actions"`

### Task 24: Final integration — full editor test pass, plan tracker, smoke run

**Files:**
- Create: `Garbus.Game.Tests/Editor/TestSceneEditorIntegration.cs`
- Modify: `PLAN-port.md` — tick Phase 4 checkboxes, record deviations
- Modify: `CLAUDE.md` — add the Phase 4 "current state" section (layout, key classes, gotchas that cost debugging cycles during implementation); per repo rule keep it present-state-only

**Interfaces:** consumes everything.

- [ ] **Step 1: Write the integration test** — the full loop in one scene: new chart → place a cardinal + hold + seam-crossing slider → edit metadata title → add timing point at 10000/BPM 180 → Save to temp dir → assert file on disk decodes to 3 objects + 2 timing points + title → Undo ×3 / Redo ×3 → object counts track → clipboard clone a note → verify checks report the missing-audio issue → switch through all four tabs without error.
- [ ] **Step 2:** run it → fix whatever integration seams it exposes (this is the step that finds cross-task wiring bugs; budget real time).
- [ ] **Step 3:** Full suite — everything green.
- [ ] **Step 4: Manual smoke run** — `dotnet run --project Garbus.Desktop`: New Chart → Save As → Setup: pick a real mp3/ogg + background → Compose: place notes/holds/sliders across the seam, space playback scrolls the grid synced to audio, timeline zoom changes scroll speed → Timing: tap-set BPM, metronome audible → Verify: refresh, click an issue seeks → Test (F5): gameplay of the WIP chart, Esc back → Ctrl+S → reopen from main menu, everything intact. Fix anything that fails; add a regression test if it was logic, note it in CLAUDE.md if it was a gotcha.
- [ ] **Step 5: Update `PLAN-port.md` + `CLAUDE.md`** per Files above.
- [ ] **Step 6: Commit** — `git add -A && git commit -m "Phase 4: editor rebuild complete"`

---

## Post-plan checklist (for the executor)

- After every task: full suite green before commit; never commit red.
- Vendored files: header + adaptation line, every time — it's a repo convention, reviewers will bounce violations.
- When a vendored osu API doesn't match what a ported BAC file expects, **change the vendored side** to osu's real shape (re-read the osu source) rather than editing the BAC logic — the BAC code is the battle-tested part.
- Anything discovered that this plan got wrong: fix the plan file in the same commit so the tracker stays truthful.
