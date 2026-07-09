# Phase 4 — editor rebuild: design

Port the BAC editor (BAC repo `Edit/`, ~2,400 lines) onto Garbus, rebuilding the osu.Game editor
scaffolding it stands on. Approach: **hybrid** — vendor osu's well-isolated editor core and blueprint
stack with trims; rebuild all screens/chrome bespoke on framework `Basic*` controls (functionality
parity matters, osu's visuals do not).

## Decisions (user-confirmed)

- **Chart files are free-floating.** Charters save WIP `.garbus` files in a directory of their choosing;
  audio/background live beside the file. WIP charts never appear in song select (Phase 5's library is a
  separate `%APPDATA%` store). Test-play of WIP charts happens via the editor's Test mode only.
- **Entry:** a minimal, unstyled `MainMenuScreen` — Play (bundled test chart → `PlayScreen`), New Chart,
  Open Chart (`BasicFileSelector`, `.garbus` filter). `GarbusGame` boots into it.
- **Tabs (top-right):** Setup / Compose / Timing / Verify. No Design tab.
- **Menu bar: full**, including View and Timing menus (applicable items only — see Shell).
- **Top timeline: scrub + display only.** No object editing in the strip; editing stays in the grid.
- **Metadata: all eight osu fields** including Romanised Title/Artist (UTF-8; they exist so
  latin-only readers get readable song select entries, not for ASCII legacy).
- **Background: store only.** Picker + copy + format field this phase; nothing renders it yet.
- **Verify: shell + four checks** — audio present, background present, objects beyond track end,
  objects before time 0. Grows later with charting standards.
- **Format changes in place** — `version` stays 1, no compatibility layer (repo rule). No garbus charts
  exist yet.

## 1. Architecture

New code in `Garbus.Game/Edit/`, sibling to `Gameplay/`, in four strata:

1. **Vendored core** (ppy MIT header + "Adapted for Garbus:" line): `EditorClock` (wraps
   `FramedChartClock`), `BindableBeatDivisor` (the real bindable; owns `PREDEFINED_DIVISORS`,
   which `Charts/Timing` then references instead of its Phase 3 inlined copy), `EditorChangeHandler` (abstract snapshot
   base) + `GarbusChartChangeHandler` (serializes via `GarbusChartSerializer`), `EditorChart` (the
   `EditorBeatmap` counterpart wrapping `GarbusChart`: hit-object add/remove/update events,
   `SelectedHitObjects`, `PerformOnSelection`, transaction API, beat-snap provider), `SnapResult`.
2. **Vendored blueprint stack** (`Edit/Compose/`): `BlueprintContainer`, `SelectionHandler`,
   `SelectionBox` + the transform math we use, `HitObjectPlacementBlueprint` /
   `HitObjectSelectionBlueprint` bases, drag box, `ComposeBlueprintContainer`. Trims: skinning,
   `OsuConfigManager` bindings → plain bindables, OsuColour → hardcoded palette, localisation → plain
   strings.
3. **Ported BAC editor logic** (`Bac*` → `Garbus*`): `EditorAngleMapping` as-is; editor playfield,
   drawables, blueprints, composition tools, selection handler adapted onto the vendored stack.
4. **Bespoke screens** (`Edit/Screens/`): shell (tabs, menu bar, bottom bar) + the four tab screens on
   framework `Basic*` controls (`BasicMenu`, `BasicTabControl`, `BasicTextBox`, `BasicFileSelector`,
   `WaveformGraph`, `BasicContextMenuContainer` — all confirmed present in bare osu-framework).

## 2. Editor shell

`GarbusEditor : Screen` owns cached `EditorClock`, `EditorChart`, `BindableBeatDivisor`,
`GarbusChartChangeHandler`, clipboard. Top bar = menu bar (left) + tab control (right). Tab screens stay
loaded and toggle visibility. Shared bottom bar.

**Menus:**

- **File:** New, Open…, Save (Ctrl+S), Save As…, Exit.
- **Edit:** Undo, Redo, Cut, Copy, Paste, Clone. Paste at playhead; selections serialize through the
  `Charts/Format/` DTO layer.
- **View:** Timeline → show timing changes / show ticks / waveform opacity; auto-seek on placement;
  contract sidebars. Persisted as new `GarbusSetting` entries in `garbus.ini`. (osu's storyboard,
  breaks, hit markers, distance snap, background dim items don't apply.)
- **Timing:** Set preview point to current time; Snap all notes to current snap divisor (confirm
  dialog — a minimal bespoke dialog overlay, shared with the exit prompt).

**Dirty tracking:** change-handler state hash vs last-saved hash; `*` in title; exit prompts
save/discard/cancel. A new chart has no file path until first Save As and **no audio until
Setup→Resources assigns one** — the editor runs on a silent seekable placeholder clock until then.

## 3. Chart format changes (in place, version stays 1)

- `ChartMetadata` gains `romanisedTitle`, `romanisedArtist`, `source`, `tags`.
- Chart gains `backgroundFile` (nullable relative path, store-only this phase) and `previewTime`
  (nullable ms).
- Audio/background resolve **from the chart's directory on disk** (per-chart `Storage`-backed track
  store created on open/save), not the resource store. A brand-new chart needs Save As before Resources
  can copy files in.
- Bundled `test-chart.garbus` regenerates via the `[Explicit]` `RegenerateBundledTestChart` test.

## 4. Compose tab

### Structure

- **`GarbusHitObjectComposer`** on the vendored composer base: editor drawable ruleset (vertical
  down-scroll, constant algorithm) hosting `GarbusEditorPlayfield` (unrolled circle: angle grid,
  cardinal labels, shoulder lane strips, ghost bands). Full functionality parity for all BAC editor
  behaviour: instant/hold/shoulder/slam placement, multi-click slider placement (right-click commit,
  T node insertion), path-precise slider selection, ghost-twin interaction, wrap copies, drag-rotate
  with angle snap, anticlockwise context menu, pooled polyline rendering + rebuild counter.
- **Left toolbox** (collapsible via contract-sidebars): tool radio buttons (Select, Cardinal, Hold,
  Shoulder, Slam Centered, Slam Edge, Slider — hotkeys 1–7) + angle-snap radio group (default 45°).
- **Right toolbox:** beat divisor control (presets + arbitrary divisor).

### Top timeline (scrub + display only)

Vendored `ZoomableScrollContainer` hosting: `WaveformGraph` (opacity from View menu), beat ticks
(`ControlPointInfo` + divisor, toggleable), timing-change markers (toggleable), non-interactive object
markers (dot per object; duration bar for holds/sliders), fixed centre playhead. Click/drag = beat-snapped
seek. Zoom via Ctrl+scroll and ± buttons; **zoom syncs the grid scroll speed** (composer reads timeline
zoom + track length, as BAC does today).

### Bottom bar

Time info (mm:ss.fff + BPM at playhead) · summary timeline (whole-track scrubber: timing points +
preview point; click seeks) · playback control (play/pause, speed 0.25/0.5/0.75/1.0× via clock tempo) ·
Test button.

### Playback & navigation

Space = play/pause on `EditorClock`; the grid scrolls vertically synced to the music (time-positioned
drawables in the scrolling container). Scroll wheel and left/right arrows seek by one divisor step;
Z = seek to start, X = play from start, C = pause/resume, V = seek to end. Placement auto-seeks per the
View toggle.

### Test mode

Test button / F5 pushes a `PlayScreen` variant taking an in-memory chart + track: current `EditorChart`
deep-cloned through the serializer, gameplay starts at current editor time minus a short lead-in, Esc
pops back to the editor with the clock where test play ended.

## 5. Setup tab

Scrollable form, three sections:

- **Metadata:** Title, Romanised Title, Artist, Romanised Artist, Charter, Chart Name, Source, Tags —
  textboxes writing `ChartMetadata` inside change-handler transactions (undoable).
- **Difficulty:** stubbed placeholder section ("no per-chart difficulty settings").
- **Resources:** Audio Track / Background Image rows — current filename + Choose button
  (`BasicFileSelector`, audio/image extension filters). Picking copies the file into the chart
  directory and stores the relative name; picking audio hot-swaps the editor clock's track (waveform +
  track length update). Disabled with a "save the chart first" notice until the chart has a directory.

## 6. Timing tab

osu's `TimingScreen` structure trimmed to timing-only control points. Left: timing point list (offset,
BPM, time signature per row), add-at-playhead / delete. Right: selected-point settings — offset & BPM
(text entry + hold-to-repeat nudge, osu's adjustment semantics), time signature dropdown, tap-timing
control (metronome with **synthesized** tick samples, tap-to-set-BPM pad, waveform comparison display —
framework `WaveformGraph`-based). Row select seeks; edits are transactions (undoable). Controls rebuilt
on `Basic*`; "wholesale" means osu's logic/interaction semantics, not its widgets.

## 7. Verify tab

Issue-list shell: table (time / message / check name), Refresh button, row click seeks. Four checks
behind a tiny `ICheck` interface: audio file present; background present; objects beyond track end;
objects before time 0. The check list is a plain array.

## 8. Undo/redo internals

`GarbusChartChangeHandler`: on every transaction end (and standalone mutation), serialize the whole
chart and push onto a bounded (50) state stack. Restore diffs serialized hit-object lists — unchanged
JSON ⇒ object untouched; the delta is removed/re-added through `EditorChart` events, preserving
selection/drawable state elsewhere. Metadata/timing/settings restore by direct overwrite. The state hash
doubles as the dirty flag.

## 9. Testing

`GarbusEditorTestScene` on framework `TestScene` (replaces osu's `EditorTestScene`): boots the full
`GarbusEditor` headlessly with an in-memory chart + `ManualInputManager`. Coverage:

- Ported from `TestSceneBacEditor`: placement of every object type, slider multi-click + T-insertion,
  drag-rotate snap, wrap-seam copies, ghost-twin selection, path-precise slider hit-testing.
- New: save→open roundtrip through a temp directory; undo/redo across placement/delete/metadata;
  clipboard cut/copy/paste/clone; the four verify checks; timing point add/edit; tab switching.
- One manual windowed smoke run (real audio, space playback, test mode) before the phase is called done.

`PLAN-port.md` Phase 4 checkboxes update as items land.
