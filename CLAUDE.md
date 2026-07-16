# Garbus — agent onboarding

A standalone rhythm game built directly on **osu-framework** (no osu.Game dependency). It is the port
target for the BigAssCircle osu!lazer ruleset at `C:\Users\zachd\Code\BAC` — a rhythm-based action game
where hit objects spawn at the centre of a circular playfield and travel outward toward the ring, with
judgement at the edge timed to the music.

This is an experimental project and backwards compatibility will NEVER matter until this line is removed. Do not add historical context to documentation, do not add compatibility layers if a schema changes, and do not increment version numbers on anything. There are no garbus charts in existence yet so compatibility does not matter.

This repo's integration branch is master.

**Read `PLAN-port.md` first** — it is the canonical port plan and progress tracker: locked-in decisions,
the osu.Game→osu-framework dependency-split findings, the vendoring manifest, and per-phase checklists.
Update it as work lands. The BAC repo holds the gameplay/editor source being ported and the reference
clones of `osu` / `osu-framework` in `BAC\LocalDependencies` (both MIT — vendoring is fine with
attribution headers kept).

## Build / run / test

- **Build:** `dotnet build Garbus.Desktop.slnf` (the iOS slnf needs a workload not installed here)
- **Run:** `dotnet run --project Garbus.Desktop`
- **Tests:** `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj` (headless NUnit over visual test
  scenes), or run `Garbus.Game.Tests` directly for the visual test browser
- **Runtime logs:** `%APPDATA%\Garbus\logs\*.runtime.log` (config lives at `%APPDATA%\Garbus\garbus.ini`)

## Conventions

- Nullability is enabled solution-wide. DI-resolved / BDL-initialised fields use `= null!`.
- **Vendored osu.Game files** keep the ppy MIT attribution header plus an "Adapted for Garbus:" line
  summarising the trims. Vendor faithfully; deviate minimally and note why.
- **Terminology:** osu's "beatmap" is "chart" here; `Bac*` class prefixes from the source repo become
  `Garbus*` when ported.
- No mods, difficulty calculation, replays, skinning, or realm — deliberately dropped (see plan).

## Current state (Phase 4 complete)

The editor lives under `Garbus.Game/Edit/` and is reached from `MainMenuScreen`. Layout and key
classes:

- **Disk I/O:** `Charts/ChartFile` is the editor's handle on a `.garbus` at an arbitrary path (replaces
  osu's `WorkingBeatmap` — no realm). Load/`Save(path)`/`Save()`, `ImportResource` (copies audio/bg
  into the chart dir, self-copy guarded), and a lazily-cached directory `ITrackStore` (invalidated on
  save-to-new-dir). Chart format gained editor fields: extra `ChartMetadata` fields, `backgroundFile`,
  `previewTime`.
- **Core:** `Edit/EditorClock` + `Edit/BindableBeatDivisor` (both vendored). `Edit/EditorChart` is the
  EditorBeatmap counterpart — it **aliases `Chart.HitObjects` directly** (no shadow copy; every
  mutation is what serialization reads). Undo/redo: `Edit/EditorChangeHandler` +
  `Edit/GarbusChartChangeHandler` (snapshots the chart as JSON, applies changes by a per-object
  JSON-identity diff — not osu's `.osu` line diff). `EditorChart.ApplyDefaults` takes no arguments
  (fixed hit windows).
- **Shell:** `Edit/Screens/GarbusEditor` — four tabs (`EditorTab` Setup/Compose/Timing/Verify), menu
  bar (File/Edit/View/Timing), dialog overlay, hotkeys (Ctrl+S/Z/Y/X/C/V/D, F5, Space, transport
  keys), dirty tracking (`HasUnsavedChanges` = state hash vs hash-at-last-save). DI-caches
  `EditorClock`, `EditorChart`, change handler (also as `IEditorChangeHandler`), `BindableBeatDivisor`,
  `EditorClipboard`, `ChartFile`, and `ControlPointInfo`.
- **Compose:** vendored blueprint/composer stack (`Edit/Compose/`, `Edit/Blueprints/`) + ported BAC
  editing — `Edit/EditorAngleMapping` (the sole angle↔timeline-x authority; grid left edge = 135°, seam
  on the 315° diagonal, ghost wrap bands), `Edit/GarbusEditorPlayfield`, `Edit/Drawables/` (simplified
  editor sprites, separate from gameplay polar drawables; x driven from angle every frame + a ±360°
  ghost twin near grid edges), tools, placement/selection blueprints, `Edit/GarbusSelectionHandler`.
- **Timeline / transport:** `Edit/Screens/Timeline/` strip (waveform, ticks, timing-change markers,
  zoom-synced scroll speed, View toggles) and `Edit/Screens/BottomBar/` (transport, summary timeline,
  Test button). F5/Test launches a `PlayScreen` with a serializer deep-clone of the WIP chart, starting
  1500 ms before the playhead; returning seeks the editor clock to the gameplay exit time.
- **Setup / Timing / Verify:** metadata/resources/difficulty-stub form (`Edit/Screens/Setup/`); timing
  point list + settings + tap-timing + metronome (`Edit/Screens/Timing/`); `Edit/Screens/Verify/`
  runs four `ICheck`s (audio/background present, objects before time zero / beyond track end) and lists
  clickable issues that seek. `Edit/EditorClipboard` does cut/copy/paste/clone (clone deliberately does
  NOT touch clipboard content, matching osu).
- **Tests:** headless (`Garbus.Game.Tests/Editor/`), all green. `TestSceneEditorIntegration` walks the
  full authoring loop end-to-end (new → place cardinal/hold/seam-slider → edit title via the real Setup
  FormRow → add timing point → Save + decode-from-disk assert → undo×3/redo×3 → clipboard clone →
  Verify reports missing-audio → switch all four tabs).

**Gotchas that cost debugging cycles (avoid rediscovering):**

- **Vertical `FillFlowContainer` collapses a `RelativeSizeAxes.Both` child to zero height.** The editor
  tab area is a padded plain `Container` (top/bottom bar heights reserved via `Padding`), never a fill
  flow, for exactly this reason. `TestSceneEditorShell.TestTabContentHasHeight` guards it.
- `EditorChart` aliases `Chart.HitObjects` — do not build a second list; mutating the alias is what
  Save serializes.
- The composer's editor drawables are **non-pooled**; the composer tracks them in a per-hit-object
  `drawableMap` and adds/removes on `HitObjectAdded`/`Removed`. Lifecycle is manual — don't assume
  framework pooling. **Removed drawables must be explicitly `Dispose()`d** — `HitObjectContainer`
  detaches with `RemoveInternal(…, false)` (correct for osu's pooled path), and an undisposed
  drawable stays subscribed to `HitObject.DefaultsApplied`, re-running `Apply()` on every later
  update of that object (zombies pile up quadratically → GC storm). Pinned by
  `TestRemovedObjectDrawableIsDisposed`.
- **`EditorChart.Update` refreshes drawables IN PLACE** (`HitObject.DefaultsApplied` → drawable
  re-`Apply()` + scrolling-container relayout) — never remove+recreate on update; recreating tore
  down framebuffer-backed slider visuals per drag event (the slider node-drag GC storm). Two traps
  this depends on: editor drawables must swallow drawable-side `LifetimeEnd` writes (judged objects
  with no hit-state transforms otherwise expire AT their own start time on re-apply and never come
  back — the scrolling container only re-lays-out ALIVE entries), and node/handle drags must skip
  `EditorChart.Update` when nothing changed. Pinned by `TestSceneComposerLifecycle` +
  `TestSliderNodeDragDoesNotRecreateDrawable` / `TestUpdateRefreshesDrawableInPlace`.
- **Drag deltas can be a full wrap (±360°) off** when the cursor sits over an object's ghost twin —
  `GarbusSelectionHandler.HandleMovement` must reduce the degree delta via `MinimalDiff` so
  "already there" is 0 (no update fired), not a spurious ±360 that rebuilds every selected object
  per mouse-move event. Pinned by the incremental-drag tests in `TestSceneComposeSelection`.
- **The horizontal drag delta is in GRID degrees, not absolute angle.** In the reversed ("clockwise")
  view (`EditorAngleMapping.Direction == -1`) grid degrees run opposite to absolute angle, so
  `HandleMovement` must map the screen-derived delta through `Direction` (× ±1, same sense as
  `GridOffset`) before adding it to `AngleDeg` — otherwise a rightward drag rotates the object the
  wrong way and the growing cursor gap makes it bounce around unpredictably. Pinned by
  `TestDragRotatesTowardCursorInReversedView` (an endpoint check on a *full clean* drag hides this —
  a full wrap re-resolves to the cursor by coincidence; assert a partial/single-step drag direction).
- **Lambda event subscriptions leak** if not unsubscribed (timeline/metronome components subscribe to
  `ControlPointInfo.ControlPointsChanged`, clock, selection, `HitObjectUpdated`). Keep a field
  reference to the handler and unsubscribe in `Dispose`.
- **The top/bottom bars must come AFTER the tab container in `GarbusEditor`'s child list** (osu's
  Editor order: content first, bars after). The compose blueprint stack claims positional input over
  the whole screen (`ReceivePositionalInputAt => true`), so bars listed earlier never receive clicks,
  and menu dropdowns (children of the top-bar subtree) draw behind the tab content. Guarded by
  `TestSceneEditorShell.TestTabSwitchingViaClick` / `TestFileMenuSaveViaClick`.
- **Wheel-seek convention:** wheel-down (negative `ScrollDelta.Y`) = forward in time, wheel-up =
  backward (matches osu's `Editor.cs`). Pinned by tests.
- **Fixed overlays on the `TimelineStrip` must use `AddInternal`, not `base.Content.Add`.** A
  `ScrollContainer`'s `base.Content` (`ScrollContent`) scrolls horizontally AND auto-sizes its width
  to the full track, so `Anchor.TopCentre` there pins to the track *midpoint* and drifts off — not the
  viewport centre. The `CentreMarker` playhead lives in `AddInternal` (matches osu's `Timeline.cs`), so
  it stays at the viewport centre where the timeline scrolls the current time to. Pinned by
  `TestSceneTimeline.TestCentreMarkerPinnedToViewportCentre`.
- `TransferBlueprintFor` runs on `HitObjectUpdated` so a re-defaulted object keeps its selection
  blueprint — updating an object regenerates nested objects, so the blueprint must be re-pointed.
- Placement auto-seek: `HitObjectPlacementBlueprint.EndPlacement` seeks the clock to the placed object;
  wait for the seek before asserting screen positions in tests.
- Slider **node selection** is local to `SliderSelectionBlueprint` (a `HashSet<GarbusPathControlPoint>`
  by reference) — not part of `EditorChart.SelectedHitObjects`/undo/clipboard. Node handles only receive
  input while the slider is selected, so clicking a node on an unselected slider selects the whole
  slider; once selected, click picks a node (Ctrl toggles), and dragging one moves the whole node
  selection. Delete (via `IKeyBindingHandler<PlatformAction>`, which sees the action before
  `SelectionHandler`) and `HandleQuickDeletion` (Shift+RightClick) remove nodes; emptying the path
  removes the slider. Pinned by the node tests in `TestSceneComposeSelection`.
- **The compose judgement line is raised `GarbusEditorPlayfield.JUDGEMENT_LINE_OFFSET` (40px) above the
  playfield bottom**, leaving a "hit zone" the objects scroll into after passing it (`StartTime` <
  `EditorTime`). Every time-scrolling layer keys its trailing edge (= the judgement line) off its own
  `DrawHeight`, so ALL of them must share that same bottom inset or they desync: currently the
  `HitObjectContainer` (inner padded container inside its full-height mask), `EditorBarLineDisplay`, and
  the beat-snap grid's `UnderlayElements`. The static grid backdrop (angle lines, shoulder strips, ghost
  bands) stays full height. The exposed negative-time region is why `HitObjectPlacementBlueprint`
  rejects `StartTime < 0`. Pinned by `TestSceneComposePlacement.TestPlacementInHitZoneRejectsNegativeTime`.
- **The compose placement/selection harnesses must wire the composer subtree's `Clock` to the
  `EditorClock`** (as `ComposeTab` does), or the playfield maps time↔position against the ambient wall
  clock — so editor-time-relative behaviour (e.g. the hit zone at time 0 mapping to negative times)
  can't be reproduced in tests.

## Current state (Phase 3 complete)

Phase 3 additions — the native chart format:

- `Garbus.Game/Charts/` — `GarbusChart` (metadata + timing + hit objects; hit windows are fixed —
  `DefaultHitWindows` constants, no per-chart difficulty), `ChartMetadata`, `ChartStore`
  (decodes `.garbus` resources; cached in `GarbusGameBase`), `Charts/Timing/` (vendored
  `ControlPointInfo` stack, timing-only), `Charts/Format/` (versioned JSON DTOs +
  `GarbusChartSerializer`; `"type"` discriminator must stay first per hit object).
- `PlayScreen` loads `Garbus.Resources/Charts/test-chart.garbus` and resolves audio from the chart's
  `audioFile`. The bundled file's source of truth is `GarbusTestChartGenerator` — regenerate via the
  `[Explicit]` test `TestChartFormat.RegenerateBundledTestChart` after changing generator or format.
- 20 headless tests green (`TestChartFormat` adds roundtrip/version/bundled-file coverage).

## Phase 2 state (gameplay vertical slice)

- `Garbus.Game/Timing/` — the vendored gameplay clock stack: `FramedChartClock` (née
  `FramedBeatmapClock`; realm per-beatmap offset → plain `ChartOffset` property),
  `OffsetCorrectionClock`, `GameplayClockContainer`, `MasterGameplayClockContainer` (takes a `Track`
  directly), `IGameplayClock`. Platform offset constants (Windows +15ms, experimental WASAPI −25ms)
  are osu's verbatim — all other latency-critical audio lives in osu-framework itself and is untouched.
  **`GameplayClockContainer` sets `Content.Clock = GameplayClock` — a deliberate deviation from osu.**
  In osu the DrawableRuleset's FrameStabilityContainer applies the gameplay clock to the playfield
  subtree; Garbus dropped DrawableRuleset, so without this line the playfield silently runs on the
  ambient wall-time clock (object lifetimes compare against app-session time → hit objects never
  appear once the app has been open longer than the chart; clock resets don't affect gameplay).
- `Garbus.Game/Gameplay/` — the vendored osu.Game gameplay infrastructure: `HitObject`,
  `DrawableHitObject` (skinning stripped; hit sounds via `Gameplay/Audio/HitSoundContainer`, the thin
  `DrawableSample` wrapper replacing `SkinnableSound`), lifetime entries + pooling, `Playfield`,
  `HitObjectContainer`, judgements/`HitResult`/hit windows, constant scroll algorithm +
  `GarbusScrollingInfo`.
- Game domain (ported from BAC, `Bac*` → `Garbus*`): `Objects/` (+drawables), `UI/` (`GarbusPlayfield`
  → `Ring` → `Lane`s, `GarbusScrollingHitObjectContainer` doing the polar time→radius mapping),
  `Input/` (`GarbusAction` enum, `GarbusInputManager` KeyBindingContainer — gamepad + keyboard
  defaults, config rebinding deferred to Phase 5 — and `AnalogInputManager` for stick catchers),
  `Core/`, `Charts/` (`GarbusChart` + `GarbusTestChartGenerator`).
- `Screens/PlayScreen` — the minimal game loop replacing osu's `Player`: clock stack → input →
  playfield, score/combo/accuracy with rewind-revert, inline results overlay. `GarbusGame` boots into
  it; space pauses, R restarts.
- `Garbus.Game/Configuration/` — `GarbusConfigManager` (`IniConfigManager`, `garbus.ini`) with the
  global `AudioOffset` setting; cached in `GarbusGameBase`.
- `MainScreen` — the Phase 1 clock-stack skeleton, still reachable via the test browser.
- Tests (17 headless, all green): `TestSceneClockStack`, `TestSceneGameplay` (manual-clock lifetimes,
  auto-miss, hold/slider judgement, key-press hit via `ManualInputManager`), `TestScenePlayScreen`.
  Gotcha: manual-clock jumps larger than an object's alive window skip judgement entirely — step in
  sub-window increments (`TestSceneGameplay.playThrough`).
- Resources: `Textures/` (square, paddle, arrow from BAC), `Samples/Gameplay/soft-hitnormal.wav`
  (synthesized — osu-resources assets aren't freely reusable), `Tracks/test-track.ogg` (track lookups
  need the full filename — `TrackStore` only probes `.mp3`), `Charts/test-chart.garbus`.

Next: Phase 5 per `PLAN-port.md`.
