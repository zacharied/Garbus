# Song-Owned Chart Storage Implementation Plan

**Goal:** Replace one-file-per-chart storage with one `.garbus` song file that owns shared metadata,
resources, preview time, optional shared timing, a stable song UUID, and one or more UUID-addressed
charts.

**Source specification:** `docs/superpowers/specs/2026-07-19-song-storage-architecture-design.md`

**Architecture:** Introduce `GarbusSong` as the serialized aggregate and `SongFile` as its disk/resource
handle. Decode v1 files through a one-way conversion into a one-chart song. Expose one active chart and
one effective timing source through editor-owned facades so existing editor components can rebind when
selection or timing ownership changes. Make song select enumerate song files directly and address an
embedded chart by `(source, song-file locator, ChartId)`.

**Tech stack:** C# / .NET 8, System.Text.Json, osu-framework bindables and drawables, NUnit unit and
visual test scenes.

## Global constraints

- Keep version 2 as the only write format. Never rewrite a v1 file merely by opening, scanning, or
  playing it.
- Preserve exact v1 JSON bytes for deterministic UUID derivation. Do not hash reserialized DTOs.
- Use `Guid` values for `SongId` and `ChartId`; reject empty IDs and duplicate chart IDs.
- Preserve the distinction between an omitted timing property and an explicitly empty timing array.
- Never permit mixed timing ownership: either the song owns timing or every chart owns timing.
- Keep ordinary chart selection out of serialization, undo history, and dirty tracking.
- Keep every task buildable before moving to the next task. Temporary legacy members may exist only as
  compile bridges and must be removed in Task 11.
- Use the editor's atomic commit-proposal tool at commit checkpoints; do not run `git commit` directly.

---

### Task 1: Add the song domain model and identity invariants

**Files:**
- Create: `Garbus.Game/Charts/GarbusSong.cs`
- Create: `Garbus.Game/Charts/SongMetadata.cs`
- Create: `Garbus.Game/Charts/SongResources.cs`
- Modify: `Garbus.Game/Charts/GarbusChart.cs`
- Modify: `Garbus.Game/Charts/ChartMetadata.cs`
- Create: `Garbus.Game.Tests/Charts/TestGarbusSong.cs`

**Interfaces:**
- `GarbusSong.SongId : Guid`
- `GarbusSong.Metadata : SongMetadata`
- `GarbusSong.Resources : SongResources`
- `GarbusSong.PreviewTime : double?`
- `GarbusSong.ControlPointInfo : ControlPointInfo?`
- `GarbusSong.Charts : List<GarbusChart>`
- `GarbusSong.GetChart(Guid) : GarbusChart`
- `GarbusSong.GetEffectiveControlPointInfo(GarbusChart) : ControlPointInfo`
- `GarbusSong.CreateDefault() : GarbusSong`
- `GarbusChart.ChartId : Guid`
- `GarbusChart.ControlPointInfo : ControlPointInfo?`

- [ ] Write failing tests covering unique non-empty IDs, default-song construction, chart lookup,
  shared timing resolution, per-chart timing resolution, missing timing, mixed timing, zero charts,
  empty IDs, and duplicate chart IDs.
- [ ] Add `SongMetadata` with Title, Artist, TitleRomanized, ArtistRomanized, and Source.
- [ ] Add `SongResources` with Track and Background relative paths.
- [ ] Add `GarbusSong`, generate `SongId` once through an init-only property, and implement structural
  validation plus effective-timing resolution.
- [ ] Add init-only `ChartId` to `GarbusChart` and make chart timing nullable. Generate chart IDs once,
  but let serializers set an existing ID.
- [ ] Make `CreateDefault()` return one blank Novice chart, shared timing containing 120 BPM at 0 ms,
  and distinct non-empty song/chart IDs.
- [ ] Retain existing song-owned members on `ChartMetadata` only as a temporary compile bridge. Add a
  cleanup comment pointing to Task 11; do not use those members from new code.
- [ ] Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestGarbusSong"
```

Expected: all new domain tests pass.

- [ ] Propose commit: `feat: add song aggregate and chart identities`

---

### Task 2: Implement the v2 serializer and deterministic v1 conversion

**Files:**
- Create: `Garbus.Game/Charts/Format/SongFileDto.cs`
- Create: `Garbus.Game/Charts/Format/LegacyChartFileDto.cs`
- Create: `Garbus.Game/Charts/Format/SongDecodeResult.cs`
- Create: `Garbus.Game/Charts/Format/GarbusSongSerializer.cs`
- Modify: `Garbus.Game/Charts/Format/ChartFileDto.cs` (split reusable hit/design/timing DTOs out or
  reduce it to the internal v1 shape)
- Modify: `Garbus.Game/Edit/EditorClipboard.cs`
- Replace tests in: `Garbus.Game.Tests/TestChartFormat.cs`

**Interfaces:**
- `GarbusSongSerializer.CURRENT_VERSION == 2`
- `GarbusSongSerializer.Encode(GarbusSong) : string`
- `GarbusSongSerializer.Decode(string|Stream) : SongDecodeResult`
- `SongDecodeResult.Song : GarbusSong`
- `SongDecodeResult.WasConvertedFromVersion1 : bool`
- `GarbusSongSerializer.EncodeHitObject(s)` / `DecodeHitObjects(...)` remain available for editor
  identity and clipboard operations.

- [ ] Replace format tests first. Cover shared-timing roundtrip, per-chart-timing roundtrip, multiple
  charts, empty timing arrays, omitted timing, UUID preservation, unknown version rejection, and every
  structural rejection rule.
- [ ] Add a literal v1 JSON fixture containing every legacy metadata field, resources, preview time,
  timing, each supported hit-object family, and a tutorial design point. Do not generate this fixture
  through the v2 serializer.
- [ ] Test that v1 conversion moves fields to the correct owner, drops Tags, promotes timing to the
  song, preserves objects/design points, and returns `WasConvertedFromVersion1 = true`.
- [ ] Implement deterministic UUID derivation from `UTF8(domain + "\0") + exactJsonBytes`: hash with
  SHA-256, take the first 16 bytes, set RFC 4122 version/variant bits, and construct the Guid using
  canonical big-endian byte order. Use separate `song` and `chart` domains.
- [ ] Prove repeated conversion returns the same IDs, song/chart IDs differ, and changing the v1 bytes
  changes both IDs.
- [ ] Configure nullable timing DTO properties with null omission so missing and empty arrays remain
  distinguishable.
- [ ] Validate before encode and after decode. Include actionable `InvalidDataException` messages that
  name the violated invariant.
- [ ] Move the existing polymorphic hit/design mapping and clipboard codecs into
  `GarbusSongSerializer`; update `EditorClipboard` without changing clipboard JSON semantics.
- [ ] Keep `GarbusChartSerializer` only until remaining production references migrate. Mark it as a
  temporary implementation bridge and remove it in Task 11.
- [ ] Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChartFormat|FullyQualifiedName~TestClipboard"
```

Expected: v2, v1 conversion, and clipboard tests pass.

- [ ] Propose commit: `feat: add v2 song format and v1 conversion`

---

### Task 3: Replace chart-file persistence with song-file persistence

**Files:**
- Create: `Garbus.Game/Charts/SongFile.cs`
- Create: `Garbus.Game/Charts/SongStore.cs`
- Modify or replace: `Garbus.Game/Charts/ChartFile.cs`
- Modify or replace: `Garbus.Game/Charts/ChartStore.cs`
- Modify: `Garbus.Game/GarbusGameBase.cs`
- Rename/replace: `Garbus.Game.Tests/Charts/TestChartFile.cs` to `TestSongFile.cs`
- Modify: `Garbus.Game.Tests/TestChartFormat.cs`

**Interfaces:**
- `SongFile.Song : GarbusSong`
- `SongFile.FilePath`, `Directory`, `NeedsVersionUpgrade`, and `IsDisposed`
- `SongFile.Load(path)`, `Save()`, `Save(path)`, `ImportResource(path)`, `GetTrackStore(...)`, and
  `GetAudioStream()`
- `SongStore.Get(name) : SongDecodeResult`
- `SongStore.GetAvailableSongs() : IEnumerable<string>`

- [ ] Rename the persistence tests around `SongFile` and cover unsaved guards, v2 roundtrip, v1 load,
  `NeedsVersionUpgrade`, resource import, same-file import, audio stream lookup, and disposal.
- [ ] Add atomic-save tests. Simulate a serialization/resource-copy failure and assert that an existing
  destination file remains byte-for-byte unchanged and the `SongFile` path/root does not switch.
- [ ] Add Save As tests for same-directory and different-directory paths. Copy Track and Background
  before committing the new song file; fail without switching roots when a referenced resource is
  missing.
- [ ] Write the JSON to a unique temporary sibling, flush/close it, then replace or move it onto the
  destination. Clean up the temporary file on every failure path.
- [ ] Clear `NeedsVersionUpgrade` only after a successful save. Do not save automatically from `Load`.
- [ ] Replace bundled `ChartStore` behavior with `SongStore`, while retaining the `Charts` resource
  namespace and `.garbus` extension. Cache `SongStore` from `GarbusGameBase` dependency injection.
- [ ] Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSongFile|FullyQualifiedName~TestChartFormat"
```

Expected: persistence, atomicity, Save As resource preservation, and conversion-dirty tests pass.

- [ ] Propose commit: `refactor: persist complete songs instead of charts`

---

### Task 4: Introduce the playable chart context and migrate gameplay

**Files:**
- Create: `Garbus.Game/Charts/PlayableChart.cs`
- Modify: `Garbus.Game/Screens/PlayScreen.cs`
- Modify: `Garbus.Game/Gameplay/Objects/BarLineGenerator.cs`
- Modify: `Garbus.Game.Tests/Visual/TestScenePlayScreen.cs`
- Modify: `Garbus.Game.Tests/Visual/TestSceneGameplay.cs`
- Modify: `Garbus.Game.Tests/Charts/TestBarLineGenerator.cs`

**Interfaces:**
- `PlayableChart.Song`, `Chart`, and `ControlPointInfo`
- `GarbusSong.CreatePlayableChart(Guid chartId) : PlayableChart`
- `PlayScreen(PlayableChart chart, Track track, double startTime = 0)`

- [ ] Write tests proving a playable context selects the requested UUID, resolves shared/per-chart
  timing, rejects a missing UUID, and applies defaults only to the selected chart.
- [ ] Make gameplay consume `PlayableChart.Chart` for hit/design objects and
  `PlayableChart.ControlPointInfo` for timing consumers.
- [ ] Change the parameterless developer path to load the bundled song, select its first chart, and
  resolve audio from `Song.Resources.Track`.
- [ ] Keep the public `PlayScreen.Chart` test seam if useful, and add `PlayScreen.PlayableChart` so
  tests can assert song and timing identity.
- [ ] Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestScenePlayScreen|FullyQualifiedName~TestSceneGameplay|FullyQualifiedName~TestBarLineGenerator"
```

Expected: gameplay and barline tests pass using explicit effective timing.

- [ ] Propose commit: `refactor: launch gameplay with song chart context`

---

### Task 5: Make song select enumerate song files and embedded charts

**Files:**
- Rename/modify: `Garbus.Game/Screens/SongSelect/IChartSource.cs` to `ISongSource.cs`
- Modify: `Garbus.Game/Screens/SongSelect/ChartCard.cs`
- Modify: `Garbus.Game/Screens/SongSelect/SongGroup.cs`
- Modify: `Garbus.Game/Screens/SongSelect/ChartLibrary.cs`
- Modify: `Garbus.Game/Screens/SongSelect/DirectoryChartSource.cs`
- Modify: `Garbus.Game/Screens/SongSelect/ResourceChartSource.cs`
- Modify: `Garbus.Game/Screens/SongSelect/SongSelectScreen.cs`
- Modify: `Garbus.Game/Screens/SongSelect/ChartDetailPanel.cs`
- Modify: `Garbus.Game.Tests/Charts/TestChartLibrary.cs`
- Modify: `Garbus.Game.Tests/Visual/TestSceneSongSelect.cs`
- Modify: `Garbus.Game.Tests/Visual/TestSceneChartDetailPanel.cs`

**Interfaces:**
- `SongGroup.SongId`, `SongLocator`, Title, Artist, and Charts
- `ChartCard.SongId`, `ChartId`, `SongLocator`, song fields, and chart fields
- `ISongSource.EnumerateSongs()` and `LoadPlayableChart(ChartCard)`

- [ ] Rewrite library tests around one group per `.garbus` file. Prove one v2 file with two charts
  yields one group/two cards, two files in one directory remain two groups, flat view yields one row per
  embedded chart, and charts sort by level.
- [ ] Add v1 scan/launch coverage and assert the source file's bytes and timestamp remain unchanged.
- [ ] Carry SongId as logical identity while retaining source + file locator as physical identity.
  Detect duplicate SongIds, log both locators, and keep both groups instead of merging them.
- [ ] Replace directory grouping and `GroupKey` with direct group construction from each decoded song.
- [ ] Make a card locator include the physical song locator plus ChartId. On load, decode once, resolve
  the exact chart ID, and return a `PlayableChart`.
- [ ] Populate preview, track, background, title, and artist from the song; populate chart name,
  charter, level, and difficulty from the embedded chart.
- [ ] Re-resolve selection after a rescan by `(source identity, SongLocator, ChartId)`, not only a path
  or UUID.
- [ ] Keep preview and background resource stores rooted at the song file's directory.
- [ ] Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChartLibrary|FullyQualifiedName~TestSceneSongSelect|FullyQualifiedName~TestSceneChartDetailPanel"
```

Expected: disk/resource/v1 discovery, grouping, selection, preview, detail, and launch tests pass.

- [ ] Propose commit: `refactor: discover embedded charts through songs`

---

### Task 6: Add the editor song session and active-chart facade

**Files:**
- Create: `Garbus.Game/Edit/EditorSong.cs`
- Modify: `Garbus.Game/Edit/EditorChart.cs`
- Create: `Garbus.Game/Edit/EditorTiming.cs`
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs`
- Create: `Garbus.Game.Tests/Editor/TestEditorSong.cs`
- Modify: `Garbus.Game.Tests/Editor/TestEditorChart.cs`

**Interfaces:**
- `EditorSong.Song`, `ActiveChartId`, `ActiveChart`, `AddChart()`, `RemoveActiveChart()`, and
  `SelectChart(Guid)`
- `EditorTiming.Current : Bindable<ControlPointInfo>` (the active effective timing source)
- `EditorChart.Rebind(GarbusChart)` and `ChartChanged`

- [ ] Write unit tests for initial selection, add/select/remove order, one-chart removal guard, shared
  timing on add, deep-copied per-chart timing on add, nearest-row selection after removal, and
  selection not changing serialized state.
- [ ] Make `EditorSong` the authoritative active-chart owner. Use ChartId for identity and expose
  explicit collection/selection events for Setup and undo restoration.
- [ ] Rebind `EditorChart` in place. On rebind, clear hit-object selection, replace the backing object
  list, re-sort, and emit remove/add or one reset event so compose/design consumers cannot retain stale
  drawables.
- [ ] Add `EditorTiming` and update it whenever active chart or timing ownership changes.
- [ ] Change `GarbusEditor` to accept/cache `SongFile`, `EditorSong`, `EditorChart`, and `EditorTiming`.
  Do not cache a raw `ControlPointInfo`.
- [ ] On selection, stop playback, clear selected timing/design state, preserve/clamp playhead time,
  and rebind before restarting any screen-specific state.
- [ ] Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestEditorSong|FullyQualifiedName~TestEditorChart"
```

Expected: active-chart lifecycle tests pass without reconstructing the editor screen.

- [ ] Propose commit: `feat: add editor song and active chart lifecycle`

---

### Task 7: Move undo, redo, dirty state, and v1 upgrade state to the whole song

**Files:**
- Rename/modify: `Garbus.Game/Edit/GarbusChartChangeHandler.cs` to
  `GarbusSongChangeHandler.cs`
- Modify: `Garbus.Game/Edit/EditorSong.cs`
- Modify: `Garbus.Game/Edit/EditorChart.cs`
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs`
- Modify: `Garbus.Game.Tests/Editor/TestChangeHandler.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneEditorShell.cs`

**Interfaces:**
- `GarbusSongChangeHandler(EditorSong, EditorChart)`
- Whole-song serialized state hash
- Active-chart restoration keyed by ChartId

- [ ] Rewrite change-handler tests to cover song metadata, resources, preview, chart metadata,
  add/remove chart, shared/per-chart timing, hit objects, and design points.
- [ ] Snapshot the complete song with `GarbusSongSerializer`. Preserve unchanged chart and hit-object
  references by diffing first on ChartId and then on encoded object identity.
- [ ] Restore the previously active ChartId when it survives. If undo removes it, select the chart at
  the prior list index or the preceding chart.
- [ ] Keep selection-only navigation out of snapshots and undo history.
- [ ] Compute `HasUnsavedChanges` as `SongFile.NeedsVersionUpgrade || currentHash != savedHash`.
  Confirm that opening v1 starts dirty with no artificial undo step, and successful Save clears both
  dirty causes.
- [ ] Keep each metadata commit, resource-path edit, add/remove, and timing conversion to one undo
  step.
- [ ] Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChangeHandler|FullyQualifiedName~TestSceneEditorShell"
```

Expected: whole-song undo/redo, identity preservation, dirty state, and upgrade state pass.

- [ ] Propose commit: `refactor: track editor changes across the whole song`

---

### Task 8: Rebind every editor timing and chart consumer

**Files:**
- Modify: `Garbus.Game/Edit/EditorClock.cs`
- Modify: `Garbus.Game/Edit/EditorClipboard.cs`
- Modify: `Garbus.Game/Edit/Compose/BeatSnapGrid.cs`
- Modify: `Garbus.Game/Edit/Compose/HitObjectPlacementBlueprint.cs`
- Modify: `Garbus.Game/Edit/Screens/BottomBar/SummaryTimeline.cs`
- Modify: `Garbus.Game/Edit/Screens/BottomBar/TimeInfoDisplay.cs`
- Modify: `Garbus.Game/Edit/Screens/Timeline/TimelineStrip.cs`
- Modify: `Garbus.Game/Edit/Screens/Timeline/TimelineTickDisplay.cs`
- Modify: `Garbus.Game/Edit/Screens/Timeline/TimelineTimingChangeDisplay.cs`
- Modify: `Garbus.Game/Edit/Screens/Timing/TimingPointChanges.cs`
- Modify: `Garbus.Game/Edit/Screens/Timing/TimingPointList.cs`
- Modify: `Garbus.Game/Edit/Screens/Timing/TimingPointSettings.cs`
- Modify: `Garbus.Game/Edit/Screens/Timing/TimingSectionAdjustments.cs`
- Modify: `Garbus.Game/Edit/Screens/TimingTab.cs`
- Modify chart-bound Compose/Design/Verify drawables found by the audit command below
- Create: `Garbus.Game.Tests/Editor/TestSceneEditorChartSwitching.cs`
- Modify: timing, compose, design, timeline, bottom-bar, and clipboard editor tests

- [ ] Add an integration test with two charts containing distinct objects, design points, and
  per-chart timing. Select back and forth and assert every visible/editor subsystem follows the active
  chart without stale rows, markers, selections, BPM, or snap calculations.
- [ ] Add the same test in shared-timing mode and assert both charts expose the same timing object.
- [ ] Make `EditorClock` accept timing-source changes while preserving and clamping current time.
- [ ] Replace direct DI resolution/caching of `ControlPointInfo` with `EditorTiming.Current` bindings.
  Subscribe/unsubscribe when the current value changes; do not poll in `Update()`.
- [ ] Rebind Compose and Design object collections on `EditorChart.ChartChanged`; clear selections before
  disposing/rebuilding rows and drawables.
- [ ] For shared timing edits with `Adjust objects on timing change`, apply section adjustment to every
  chart in one transaction. With the toggle off, mutate only song timing. Retain active-only behavior
  in per-chart mode.
- [ ] Audit remaining production references:

```powershell
rg -n "\.ControlPointInfo|Resolved.*ControlPointInfo|CacheAs\(.*ControlPointInfo" Garbus.Game\Edit Garbus.Game\Screens Garbus.Game\Gameplay
```

Expected: each result either resolves through `PlayableChart` in gameplay or through `EditorTiming` in
the editor; no component captures the original chart's timing permanently.

- [ ] Run focused editor suites:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneEditorChartSwitching|FullyQualifiedName~TestSceneTimingTab|FullyQualifiedName~TestSceneTimeline|FullyQualifiedName~TestSceneBottomBar|FullyQualifiedName~TestSceneDesignTab|FullyQualifiedName~TestSceneCompose"
```

Expected: all chart-switch and existing subsystem tests pass.

- [ ] Propose commit: `refactor: rebind editor consumers to active chart timing`

---

### Task 9: Build the two-column Setup UI and chart-list commands

**Files:**
- Modify: `Garbus.Game/Edit/Screens/SetupTab.cs`
- Replace: `Garbus.Game/Edit/Screens/Setup/MetadataSection.cs`
- Replace: `Garbus.Game/Edit/Screens/Setup/DifficultySection.cs`
- Replace: `Garbus.Game/Edit/Screens/Setup/ResourcesSection.cs`
- Create: `Garbus.Game/Edit/Screens/Setup/SongMetadataSection.cs`
- Create: `Garbus.Game/Edit/Screens/Setup/SongResourcesSection.cs`
- Create: `Garbus.Game/Edit/Screens/Setup/ChartMetadataSection.cs`
- Create: `Garbus.Game/Edit/Screens/Setup/ChartListSection.cs`
- Create: `Garbus.Game/Edit/Screens/Setup/ChartRow.cs`
- Create: `Garbus.Game/Edit/Screens/Setup/TimingOwnershipSection.cs`
- Modify: `Garbus.Game/Edit/Screens/Setup/FormRow.cs`
- Modify: `Garbus.Game/Edit/Screens/Dialogs/ConfirmDialog.cs`
- Replace/extend: `Garbus.Game.Tests/Editor/TestSceneSetupTab.cs`

- [ ] Write visual/behavior tests for the two headings and exact field ownership: Song contains Title,
  Romanized Title, Artist, Romanized Artist, Source, Track, and Background; Chart contains Chart Name,
  Charter, Level, and Difficulty.
- [ ] Add tests for bounded/scrollable chart list rendering, selected-row highlight, mouse/keyboard
  selection, add/select, remove/nearest selection, last-chart disablement, and destructive removal
  confirmation when objects or design points exist.
- [ ] Add timing-control tests for both conversion directions, non-identical per-chart confirmation,
  deep-copy semantics, null ownership invariants, and one-step undo/redo.
- [ ] Build Setup as a two-column `GridContainer` under one shared dialog/file-selector overlay. Make
  columns independently scrollable and allocate remaining Song-column height to the chart list.
- [ ] Commit or cancel a focused text edit before chart selection so text never writes to the newly
  active chart accidentally.
- [ ] Make chart row labels use Chart Name, fall back to Difficulty, and append `Lv.N` only for a
  positive level.
- [ ] In per-chart timing mode, Add deep-copies the previously active timing. In shared mode, Add leaves
  chart timing null. Generate a new ChartId and blank chart content in both modes.
- [ ] Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneSetupTab|FullyQualifiedName~TestEditorSong|FullyQualifiedName~TestChangeHandler"
```

Expected: Setup ownership, layout, list commands, timing conversion, and undo tests pass.

- [ ] Propose commit: `feat: edit songs and embedded charts from setup`

---

### Task 10: Migrate editor file commands, resources, verification, and test mode

**Files:**
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs`
- Rename/modify: `Garbus.Game/Edit/Screens/Dialogs/OpenChartDialog.cs` to `OpenSongDialog.cs`
- Modify: `Garbus.Game/Edit/Screens/Dialogs/SaveAsDialog.cs`
- Modify: `Garbus.Game/Edit/Screens/ChartTitleDisplay.cs`
- Modify: `Garbus.Game/Edit/Screens/Verify/ICheck.cs`
- Modify: `Garbus.Game/Edit/Screens/Verify/Issue.cs`
- Modify: `Garbus.Game/Edit/Screens/Verify/IssueTable.cs`
- Modify: `Garbus.Game/Edit/Screens/Verify/Checks/CheckAudioPresent.cs`
- Modify: `Garbus.Game/Edit/Screens/Verify/Checks/CheckBackgroundPresent.cs`
- Modify: `Garbus.Game/Edit/Screens/Verify/Checks/CheckObjectsBeforeTimeZero.cs`
- Modify: `Garbus.Game/Edit/Screens/Verify/Checks/CheckObjectsBeyondTrackEnd.cs`
- Modify: `Garbus.Game/Edit/Screens/VerifyTab.cs`
- Modify: `Garbus.Game/Screens/MainMenuScreen.cs`
- Modify: `Garbus.Game.Tests/Editor/TestChecks.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneEditorIntegration.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneMainMenu.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneTestMode.cs`

- [ ] Change New to `GarbusSong.CreateDefault()`, Open to `SongFile.Load`, Save/Save As to the full
  song, fallback filename to `new-song.garbus`, and prompts/messages from chart-file to song-file
  terminology.
- [ ] Make resource rows read/write `Song.Resources`, reload audio only when Track or root directory
  changes, and show `Save the song first to add resources.`
- [ ] Make `Set preview point` write `Song.PreviewTime` as one undoable change.
- [ ] Redefine `CheckContext` to contain Song, active Chart, SongFile, effective timing, and track
  length. Run resource/structure checks once for the song and object/rule checks for the active chart.
- [ ] Add ChartId/display name to all-chart issue rows and select the referenced chart before seeking.
- [ ] In test mode, deep-clone the complete song through the v2 serializer, resolve the active chart,
  pass its `PlayableChart` plus a fresh shared track to `PlayScreen`, and preserve the editor return
  time behavior.
- [ ] Cover opening v1 in the editor: converted values appear in Setup, `HasUnsavedChanges` starts true,
  no file write occurs, Save writes version 2, and the dirty marker clears.
- [ ] Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChecks|FullyQualifiedName~TestSceneEditorIntegration|FullyQualifiedName~TestSceneMainMenu|FullyQualifiedName~TestSceneTestMode|FullyQualifiedName~TestSceneEditorShell"
```

Expected: editor file lifecycle, v1 upgrade, resources, verify, and test mode pass.

- [ ] Propose commit: `refactor: complete song-root editor workflows`

---

### Task 11: Remove legacy chart-root APIs and migrate fixtures/test helpers

**Files:**
- Delete: `Garbus.Game/Charts/ChartFile.cs`
- Delete: `Garbus.Game/Charts/ChartStore.cs`
- Delete: `Garbus.Game/Charts/Format/GarbusChartSerializer.cs`
- Delete or internalize: `Garbus.Game/Charts/Format/ChartFileDto.cs`
- Modify: `Garbus.Game/Charts/ChartMetadata.cs`
- Rename/modify: `Garbus.Game/Charts/GarbusTestChartGenerator.cs` to `GarbusTestSongGenerator.cs`
- Modify: `Garbus.Resources/Charts/test-chart.garbus`
- Modify: `Garbus.Game.Tests/TestChartMetadata.cs`
- Modify: `scripts/ImportAllowedCharts.ps1`
- Modify: all remaining production/test references reported by the audit commands below
- Modify: `CLAUDE.md`, `PLAN-port.md`, and stale source comments where they describe chart-root storage

- [ ] Remove Title, Artist, Romanised Title/Artist, Source, Tags, AudioFile, and BackgroundFile from
  `ChartMetadata`. Remove `PreviewTime` from `GarbusChart` and remove every temporary compatibility
  bridge introduced during the migration.
- [ ] Rename the generator to return a `GarbusSong`, retain the bundled resource filename for fixture
  continuity, and regenerate it through the explicit test as version 2 with stable non-empty IDs.
- [ ] Convert test builders to a shared helper that creates a valid song plus active chart. Avoid
  scattered invalid `new GarbusChart()` editor fixtures.
- [ ] Update the allowed-chart import script and its comments for one song file containing embedded
  charts. Keep imported v1 files valid through runtime conversion, and ensure any rewritten/imported v2
  file retains its SongId and resource-relative paths.
- [ ] Audit for forbidden production ownership and old names:

```powershell
rg -n "ChartFile|ChartStore|GarbusChartSerializer|GetAvailableCharts|GroupKey|Metadata\.(Title|Artist|RomanisedTitle|RomanisedArtist|Source|Tags|AudioFile|BackgroundFile)|Chart\.PreviewTime" Garbus.Game Garbus.Game.Tests scripts
```

Expected: no production references. Any occurrence in a literal v1 test fixture or migration-only DTO
is explicitly named/commented as legacy.

- [ ] Audit direct timing access:

```powershell
rg -n "Chart\.ControlPointInfo|editorChart\.ControlPointInfo|Resolved.*ControlPointInfo" Garbus.Game
```

Expected: none outside the effective-timing resolver/migration layer.

- [ ] Regenerate and verify the bundled fixture:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "Name~RegenerateBundledTestChart"
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChartFormat"
```

Expected: the explicit generator writes version 2 and the fixture-match test passes.

- [ ] Run solution validation:

```powershell
dotnet build Garbus.Desktop.slnf
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj
git diff --check
git status --short
```

Expected: build and all tests pass; diff check is clean; status contains only intended implementation,
fixture, spec, and plan changes.

- [ ] Propose commit: `refactor: remove legacy chart-root storage`

---

### Task 12: Manual end-to-end validation and documentation closeout

**Files:**
- Modify only if validation finds defects: files from Tasks 1–11
- Review: `docs/superpowers/specs/2026-07-19-song-storage-architecture-design.md`
- Review: `docs/superpowers/plans/2026-07-19-song-storage-architecture.md`

- [ ] Open a literal v1 file in the editor. Confirm it appears as one song/one chart, uses shared
  timing, shows the conversion dirty marker, and does not change on disk before Save.
- [ ] Save the converted file, reopen it, and confirm version 2 IDs, metadata, resources, timing,
  objects, and design points persist.
- [ ] Add a second chart, edit its metadata and objects, switch repeatedly between both charts, and
  confirm Compose, Timing, Design, timeline, Verify, and test mode follow selection.
- [ ] Convert shared timing to per-chart timing and back. Exercise both confirmation paths and undo/redo.
- [ ] Save As into a different directory and confirm Track/Background copy and reload correctly.
- [ ] Put the two-chart song in the AppData library. Confirm one song group/two chart rows, preview,
  background, selection persistence, and launch of the requested chart.
- [ ] Place a copied file with the same SongId beside it. Confirm duplicate logging and distinct rows,
  with neither file silently merged.
- [ ] Run one final full build/test/diff check after any manual-test fixes.
- [ ] Record any deliberately deferred behavior as a follow-up rather than expanding this change beyond
  the specification.
- [ ] Propose final commit for validation fixes/documentation only if this task changed files.

## Definition of done

- One version 2 file stores one UUID-addressed song and at least one UUID-addressed chart.
- V1 opens through deterministic, lossless-for-supported-fields conversion and upgrades only on Save.
- Song metadata/resources/preview are never stored on charts.
- Effective timing is shared or per-chart, never mixed, and every editor consumer rebinds correctly.
- Setup has the required Song/Chart columns and chart Add/Remove lifecycle.
- Song select discovers one group per file and launches embedded charts by ChartId.
- All legacy chart-root APIs and compile bridges are removed.
- Automated and manual validation steps pass.
