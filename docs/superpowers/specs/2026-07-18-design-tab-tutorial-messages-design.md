# Design Tab — Design Points & Tutorial Messages

## Summary

Add a new **Design** editor tab between Timing and Verify. It mirrors the Timing tab's
layout, but instead of timing points it edits **design points** — time-ranged
(`StartTime`…`EndTime`) objects that apply arbitrary visual effects to the chart during
gameplay. The reference (and only initial) concrete effect is the **TutorialMessage**: a
black transparent full-screen overlay with a text message shown while the point is active.

Design points are a first-class peer of timing points: a sorted `DesignPointInfo` container
with a single `DesignPointsChanged` event and structural moves (Option B), so the list,
undo/redo, and timeline overlay all refresh off one event.

## Goals

- A Design tab that mirrors the Timing tab layout: timeline strip on top, a 40/60 split
  below (point list left, editable details right).
- A polymorphic, extensible design-point model with `TutorialMessage` as the first type.
- Full editor authoring: add / select / edit / delete, undo/redo, save/load, dirty tracking.
- Design points render their effect during gameplay (`PlayScreen`, which also covers the
  editor's Test/F5 mode).
- Design-point regions shown as a translucent band overlay on the timeline strip.

## Non-Goals

- No live preview of the effect inside the editor's Compose/Design tabs (gameplay-only).
- No second concrete effect type yet — only the extensibility to add one.
- No user-editable overlay opacity (fixed constant).
- No tap-timing / metronome analog in the right pane (Timing-specific).
- No custom per-point selection highlight on the timeline overlay (v1).

## Decisions (locked in)

| Question | Decision |
|---|---|
| Type model | Extensible abstract `DesignPoint` base + `TutorialMessage` concrete type; `"type"` discriminator in the file format. "Add" button creates a `TutorialMessage` directly (no type picker yet). |
| Where effects render | Gameplay only (`PlayScreen`). Editor Test mode is covered because it launches `PlayScreen`. |
| Editable fields | Start time, End time, Message text. Overlay opacity is a fixed constant (~0.6). |
| Model shape | **Option B** — a sorted `DesignPointInfo` container with a `DesignPointsChanged` event and structural `MoveDesignPoint`, mirroring `ControlPointInfo`. Not a bare `BindableList`. |
| Timeline region overlay | Included. Translucent band per point, event-gated on `DesignPointsChanged`. Appears on all tab timelines (shared `TimelineStrip`); toggled via a "Show Design Regions" View menu item. |
| Start/End "set to current time" | A small inline button to the **right of each** of the Start-time and End-time textboxes (row = `[textbox] [button]`), not a single button below like Timing's offset. |

## Architecture

### 1. Data model — `Garbus.Game/Charts/Design/`

**`DesignPoint`** (abstract)
- `Bindable<double> StartTime`
- `Bindable<double> EndTime`
- Bindables (not plain fields) so list rows and the settings pane react to edits and
  auto-unbind on disposal (the lambda-leak gotcha).

**`TutorialMessage : DesignPoint`**
- `Bindable<string> Text`
- Overlay opacity is a class constant (e.g. `const float OVERLAY_OPACITY = 0.6f`), not stored.

**`DesignPointInfo`** (parallel to `Charts/Timing/ControlPointInfo`)
- Holds design points **sorted by `StartTime`**.
- `IReadOnlyList<DesignPoint> DesignPoints { get; }`
- `event Action DesignPointsChanged;`
- `void Add(DesignPoint point)` — inserts in sorted order, raises the event.
- `void Remove(DesignPoint point)` — raises the event.
- `void Clear()` — raises the event (used by undo/redo rebuild).
- `void MoveDesignPoint(DesignPoint point, double newStartTime, double newEndTime)` —
  structural move: updates the bindables and re-sorts (remove + re-insert), then raises the
  event. This is the analog of `TimingPointChanges.MoveGroup`; it is how a Start/End edit
  makes the timeline overlay reposition off the single event, without per-point bindable
  subscriptions.

`GarbusChart` gains `public DesignPointInfo DesignPointInfo { get; init; } = new DesignPointInfo();`.

### 2. Serialization — `Charts/Format/`

**DTOs** (`ChartFileDto.cs`)
- `ChartFileDto` gains `public List<DesignPointDto> DesignPoints { get; set; } = new();`.
- `DesignPointDto` abstract, `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]`,
  with the `"type"` discriminator kept **first** per the existing convention.
- `TutorialMessageDto : DesignPointDto` = `"tutorial-message"` with `StartTime`, `EndTime`,
  `Text`.

**`GarbusChartSerializer`**
- `toDto`: map `chart.DesignPointInfo.DesignPoints` → `DesignPointDto` list.
- `fromDto`: build each `DesignPoint` and `DesignPointInfo.Add` it.
- No format version bump — old charts decode with an empty design-point list (compat does not
  matter per CLAUDE.md).

### 3. Editor plumbing — `Garbus.Game/Edit/`

**`EditorChart`**
- Expose `public DesignPointInfo DesignPointInfo => Chart.DesignPointInfo;` (mirrors the
  existing `ControlPointInfo` passthrough).

**`GarbusEditor`**
- DI-cache `chart.DesignPointInfo` so timeline/tab components can `[Resolved]` it (mirrors how
  `ControlPointInfo` is cached).

**`GarbusChartChangeHandler.ApplyStateChange`**
- Add a design-points rebuild block after the `ControlPointInfo` block: `DesignPointInfo.Clear()`
  then re-`Add` each point decoded from the target chart. Because the whole chart is snapshotted
  as JSON, add/edit/delete of design points participate in undo/redo automatically, and dirty
  tracking (state-hash comparison) picks them up with no extra work.

Edits from the UI follow the established transaction pattern:
`changeHandler.BeginChange()` → mutate via `DesignPointInfo` (Add/Remove/MoveDesignPoint or a
`Text` bindable set) → `editorChart.SaveState()` → `changeHandler.EndChange()`.

### 4. Design tab UI — `Edit/Screens/`

**`EditorTab` enum** — insert `Design` between `Timing` and `Verify`:
`{ Setup, Compose, Timing, Design, Verify }`. The tab bar auto-populates from
`Enum.GetValues<EditorTab>()`, so only the enum + the three wiring spots below are needed.

**`GarbusEditor`** — add a `designTab` field, an entry in `InternalChildren` (same
`State = { Value = Visibility.Hidden }` pattern), and a line in `updateTabVisibility`.

**`DesignTab : EditorTabScreen`** — mirrors `TimingTab` exactly:
- `TimelineStrip` + `+`/`–` zoom buttons on top.
- A `Container` padded by `TimelineStrip.HEIGHT` holding a 40/60 `GridContainer`:
  - Left: `DesignPointList`.
  - Right: `BasicScrollContainer` → `DesignPointSettings` (no tap-timing control below it).
- A shared `Bindable<DesignPoint?> selectedPoint` binding the list and settings panels.

**`DesignPointList`** (mirrors `TimingPointList`)
- Header row: `Start` · `End` · `Message` (text preview).
- One `DesignPointRow` per point, sorted by `StartTime`; refreshes on
  `DesignPointInfo.DesignPointsChanged` (scheduled, like the timing list).
- Row shows `StartTime`, `EndTime`, and a truncated text preview; bound copies of the point's
  bindables stored as fields (auto-unbind on disposal).
- Click selects + seeks the editor clock to `StartTime`; re-clicking the selected row
  deselects. Up/Down arrow-key navigation while the tab is visible.
- **Add** button: creates a `TutorialMessage` at the snapped playhead
  (`StartTime = snapped time`, `EndTime = StartTime + 2000ms`, `Text = "New message"`),
  through the change handler; selects the new point.
- **Delete** button: removes the selected point through the change handler.
- Reselect-by-`StartTime` after a refresh (undo/redo replaces instances), matching the timing
  list's approach.

**`DesignPointSettings`** (right pane; the design analog of `TimingPointSettings`)
- Auto-sizes vertically; rebinds to `selectedPoint`.
- **Start time (ms)** row: `[BasicTextBox] [set-to-current-time button]` (horizontal
  `FillFlowContainer`). Commit parses and calls `MoveDesignPoint`; the inline button sets the
  box to `editorClock.CurrentTime` and commits.
- **End time (ms)** row: same shape, same behavior for the end value.
- **Message** row: multi-line `BasicTextBox`; commit sets the `Text` bindable through the
  change handler.
- Validation: reject non-numeric input (restore from model); keep `EndTime > StartTime` (clamp
  or reject — reject and restore, matching the timing panel's "restore textbox on bad input").
- Test seams mirroring `TimingPointSettings` (`SetStartAndCommit`, `SetEndAndCommit`,
  `SetTextAndCommit`) for headless tests.

### 5. Timeline region overlay — `Edit/Screens/Timeline/`

**`TimelineDesignRegionDisplay`** (modeled on `TimelineTimingChangeDisplay`)
- `[Resolved] DesignPointInfo`, `[Resolved] EditorClock`.
- Pooled `Box`es, one per design point: `RelativePositionAxes = Axes.X`,
  `X = StartTime / trackLength`, relative `Width = (EndTime − StartTime) / trackLength`,
  full height, translucent fill.
- Recreated on `DesignPointInfo.DesignPointsChanged` via the `Cached` invalidate gate — the
  single event covers add/remove **and** Start/End edits (because `MoveDesignPoint` is
  structural and raises it). Text-only edits correctly do not invalidate.
- Unsubscribe in `Dispose` (lambda-leak gotcha).

**`TimelineStrip`** — add `designRegions = new TimelineDesignRegionDisplay()` to the layered
`AddRange`, placed **below** `timingChanges` so the red timing lines stay legible on top of the
translucent band. Wire a `Bindable<bool>` from a new `GarbusSetting.EditorShowDesignRegions`
(default `true`) to its `Alpha`, mirroring `EditorShowTimingChanges`.

**`GarbusSetting`** — add `EditorShowDesignRegions`; **`GarbusConfigManager`** —
`SetDefault(GarbusSetting.EditorShowDesignRegions, true)`.

**`GarbusEditor.createViewMenuItems`** — add
`new ToggleMenuItem("Show Design Regions", config.GetBindable<bool>(GarbusSetting.EditorShowDesignRegions))`.

Note: because `TimelineStrip` is a shared component whose children are baked at load, the
region band appears on the Compose and Timing timelines too. This is intentional (you see
tutorial windows while charting) and is the cheap, idiomatic scoping.

### 6. Gameplay rendering — `PlayScreen`

**`DesignOverlay`** (new drawable, hosted in `PlayScreen`)
- Reads `chart.DesignPointInfo.DesignPoints` (the play chart is a deep clone; read-only here).
- Stateless per frame: for each `TutorialMessage` where
  `StartTime ≤ gameplayClock.CurrentTime < EndTime`, show a full-screen black `Box` at
  `TutorialMessage.OVERLAY_OPACITY` with the message text centered on top; otherwise hidden.
- Stateless recomputation = rewind-safe (no revert bookkeeping needed).
- Placed in `PlayScreen.InternalChildren` above the playfield + HUD, below the results overlay.
- For a single active message the overlay is one shared black box + one `TextFlowContainer`
  whose text/alpha update each frame (overlapping windows: last-wins on text, or concatenate —
  v1 assumes non-overlapping tutorial messages; document the assumption).

## Testing (headless)

- **Serializer**: chart with design points round-trips (encode → decode → equal); old chart
  with no `designPoints` decodes to an empty list.
- **Undo/redo**: add, edit (Start/End/Text), and delete each undo and redo correctly via the
  change handler.
- **`DesignPointInfo`**: `Add` keeps sorted order; `MoveDesignPoint` re-sorts and raises
  `DesignPointsChanged`; `Text` set does **not** structurally reorder.
- **`DesignTab`**: Add creates a point and the list shows it; editing via `DesignPointSettings`
  test seams commits to the model; Delete removes it. Wire the tab's composer/clock as
  `ComposeTab` does so editor-time behavior is reproducible.
- **`TimelineDesignRegionDisplay`**: a point spans the expected fraction of the strip; moving
  it repositions the band; deleting hides it.
- **`DesignOverlay`**: with a manual clock, overlay is hidden before `StartTime`, visible with
  the right text within `[StartTime, EndTime)`, hidden after `EndTime`; correct across a rewind
  (step in sub-window increments per the `TestSceneGameplay` gotcha).
- Optional: extend `TestSceneEditorIntegration` to visit the Design tab and add a point.

## Open questions / assumptions

- **Overlapping tutorial windows** are assumed not to occur in v1 (one active message at a
  time). If needed later, define stacking/last-wins behavior.
- **Region band appearance** in v1: full-height translucent fill (a low-alpha neutral/blue
  tint), drawn below the timing lines. A thinner near-bottom band is a possible later refinement.
- **End-time invalid input** (`EndTime ≤ StartTime`) is rejected-and-restored in v1; a
  clamp-to-`StartTime+ε` alternative can be revisited.
