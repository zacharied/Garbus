# Editor

## Purpose & scope

The chart authoring tool under `Garbus.Game/Edit/`, reached from the main menu. Covers the editor
shell (tabs, menus, hotkeys, undo/redo), the compose surface (blueprints, angle mapping, editor
drawables, selection/placement), the timeline strip, transport/test mode, and the Setup/Timing/Verify
tabs. This is the domain with the most hard-won gotchas — read the gotcha section before touching
compose or timeline code. The disk model (`ChartFile`, serializers) is in [charts.md](charts.md);
the general framework traps these gotchas instantiate are in [osu-framework.md](osu-framework.md).

## Shell & core

- `Edit/Screens/GarbusEditor.cs` — the shell: four `EditorTab`s (Setup / Compose / Timing / Verify;
  `EditorTab.cs`, `EditorTabScreen.cs`), a menu bar (File / Edit / View / Timing), a dialog overlay,
  hotkeys (Ctrl+S/Z/Y/X/C/V/D, F5, Space, transport keys), and dirty tracking (`HasUnsavedChanges` =
  current state hash vs hash-at-last-save). It DI-caches the editor clock, editor chart, change
  handler (also as `IEditorChangeHandler`), beat divisor, clipboard, `ChartFile`, and
  `ControlPointInfo`. It implements `IAllowSettings` (with `ShowSettingsGear => false`) so the global
  settings overlay is available without the floating gear (which would overlap the top-left menu
  bar); a **File › Game settings** item opens it by resolving the DI-cached `ISettingsOverlayControl`
  (`GlobalSettingsContainer`, see [screens.md](screens.md)). Menu grouping uses `GarbusMenuSpacer`
  (a divider `MenuItem` rendered by `GarbusMenu`).
- `Edit/Screens/Dialogs/` — the modal stack. `ModalOverlay` is the base every picker derives from: a
  dim backdrop, a centred `Panel`, and full keyboard ownership (see the gotcha below). `DialogFooter`
  is the shared bottom strip — setting checkboxes stacking upward from the bottom-left, Cancel then
  Confirm at the bottom-right. `FileSelectDialog` is the **one** file picker: File › Open, the main
  menu's Open, and the Setup tab's resource `FileChooserRow` all construct it with their own
  extensions and confirm label. `SaveAsDialog` adds a filename box above the same footer.
  `ConfirmDialog` is a plain `VisibilityContainer` and does **not** capture the keyboard.
- `Edit/EditorClock.cs` + `Edit/BindableBeatDivisor.cs` — vendored transport/beat-snap core.
- `Edit/EditorChart.cs` — the `EditorBeatmap` counterpart. It **aliases `Chart.HitObjects` directly**
  — no shadow copy; every mutation is exactly what serialization reads. `ApplyDefaults` takes no
  arguments (Garbus hit windows are fixed — no `ControlPointInfo`/difficulty). Its `Update` refreshes
  drawables **in place** (see gotchas).
- Undo/redo: `Edit/EditorChangeHandler.cs` + `Edit/GarbusChartChangeHandler.cs`. State is snapshotted
  as JSON; changes apply by a **per-object JSON-identity diff** (not osu's `.osu`-line diff), using
  `GarbusChartSerializer.EncodeHitObject` for the identity strings.

## Compose

- `Edit/EditorAngleMapping.cs` — the **sole angle↔timeline-x authority**. Grid left edge = 135°, the
  seam is on the 315° diagonal, with ghost wrap bands. `Direction` (`+1`/`-1`) sets normal vs
  reversed ("clockwise") view.
- `Edit/GarbusEditorPlayfield.cs` — the compose playfield. The judgement line is raised
  `JUDGEMENT_LINE_OFFSET` (40px) above the playfield bottom, leaving a hit zone objects scroll into
  after passing it.
- `Edit/Drawables/` — simplified editor sprites (separate from the gameplay polar drawables); x is
  driven from angle every frame plus a ±360° ghost twin near grid edges. **Non-pooled**, tracked in
  the composer's per-hit-object `drawableMap`. The twin's `Show`/`Hide` fires only on visibility
  transitions (per-frame re-asserting allocated a fade transform per drawable per frame — GC churn at
  drag rates; pin: `TestSettledTwinVisibilityIsNotReassertedEveryFrame`).
- `Edit/Compose/` — the vendored blueprint/composer stack: `BlueprintContainer`,
  `ComposeBlueprintContainer`, `HitObjectComposer`/`ScrollingHitObjectComposer`, placement/selection
  blueprints, `SelectionBox`, `BeatSnapGrid`, drag box, radio-button toolbox, `GarbusMenu`/toggle
  menu items.
- `Edit/GarbusSelectionHandler.cs` — group move/rotate; `Edit/Tools/` — composition tools.
- Slider **node selection** is local to `SliderSelectionBlueprint` (a `HashSet` of control points by
  reference) — not part of `EditorChart.SelectedHitObjects`/undo/clipboard. Besides clicking handles,
  a **Shift+drag box** selects the nodes/heads of already-selected sliders: `GarbusBlueprintContainer`
  overrides `OnDragStart`/`UpdateSelectionFromDragBox`/`OnDragEnd` so a Shift-held box routes the drag
  quad to each selected slider's `BeginNodeDragBox`/`UpdateNodeDragBox`/`EndNodeDragBox` instead of
  whole-object selection. Plain Shift replaces node selection and, on release, prunes selected sliders
  left with no node/head; Shift+Ctrl combines with the pre-drag node selection and prunes nothing.
  Both modifiers are latched at drag start (never re-read per frame), matching the object box's Ctrl.
  Head-only sliders (no control points) are ineligible — never boxed, never pruned.
### Inspector

`Edit/Inspector.cs` — the right-toolbox panel: a text summary of the current selection plus
selection-dependent controls (built in `addControls`). Values aggregate via `MultiValue`, rendering
disagreement as `<multiple>` (an indeterminate dash for the checkbox); every edit writes the whole
selection in one undo transaction. Node/head selection isn't event-observable, so the inspector polls
it each frame and re-reads values on a 250ms roll.

Rebuilds are throttled two ways, because a drag fires `HitObjectUpdated` per selected object per
mouse-move event and a full per-event reconstruction (two dropdowns with DI loads for a slam-edge
selection) was a GC storm at drag rates: events coalesce through `Scheduler.AddOnce(rebuild)`, and
within a rebuild the text summary is always rewritten but `addControls` runs only when
`buildControlsSignature` (selection identity + each control's aggregate state + each button's
eligibility) differs from the last build. **Any control added to `addControls` must contribute its
inputs to the signature or it goes stale** — a value-only change that the signature misses will leave
the control showing the old value. Pins: `TestSlamEdgeDragDoesNotRecreateInspectorControls`
(controls survive a drag; text still tracks), `TestExternalSideChangeRefreshesInspectorDropdown`
(signature-covered value change still rebuilds).

Editor dropdowns (`MultiValueEnumDropdown`, the setup tab's difficulty dropdown) derive from
`UI/PopoverDropdown` so an open menu pops over the content below instead of reflowing it; the flows
that stack them (the inspector's control rows, `ExpandingToolboxContainer`, the setup tab columns and
sections) are `UI/FrontFirstFillFlowContainer`s so the menu draws — and receives input — in front of
that content. Any new vertical stack hosting a dropdown must be front-first too, and the same base
caps the open menu's height so a long list (the Easing enum's three dozen entries) scrolls on the
wheel instead of running off the bottom of the window (details in [screens.md](screens.md)).

Each control appears only when the selection matches its condition:

- **Side** dropdown — every selected object carries a mutable `Side` (slider + both slam types).
- **Direction** dropdown — every selected object is a `GarbusSlamEdge` (its `RotationalDirection`).
- **Easing** (`SweepEasing`) dropdown + **Smoothing** (`Smooth`) checkbox — one or more slider
  control-point nodes are picked.
- **Merge sliders** button — the selection is ≥2 sliders (all objects sliders), no node/head is
  picked, and the sliders' `[StartTime, EndTime]` spans don't overlap (touching endpoints allowed).
  Pressing it reparents every other slider's nodes — their heads included — onto the earliest slider as
  new control points, then removes the emptied sliders, in one undo transaction. Disjoint spans in
  start-time order keep the merged path's node times non-decreasing; each joined head connects to the
  running frame by the minimal rotation (`EditorAngleMapping.MinimalDiff`) while that slider's own
  internal winding is preserved by rebasing its offsets onto the head's new offset. Pins: the
  `TestMerge*` cases in `TestSceneComposeSelection`.
- **Decompose into heads** button (`Edit/DecomposeSliderButton.cs`) — the selection holds any slider
  with a path (a head-only slider has nothing to split). It samples each such slider's swept angle at
  the active grid step (`TimingPointAt(StartTime).BeatLength / beatDivisor.Value`) from the head forward
  while `t ≤ EndTime` (grid-steps-only — an off-grid end gets no head) and replaces the slider with one
  head-only `SliderBody` per sample, all in one undo transaction. The sampling/split is the pure
  `SliderDecomposition.DecomposeIntoHeads(slider, step)`; the angle read is the model-side
  `SliderBody.AngleDegAt(time)` (the non-hot-path counterpart to `DrawableSliderBody.AngleDegAt`, both
  routed through `SliderSweep` so they can't drift). Pin:
  `TestSceneComposeSelection.TestDecomposeIntoHeadsSplitsSliderAtGridStep`.

## Timeline, transport, tabs

- `Edit/Screens/Timeline/` — the timeline strip (waveform, ticks, timing-change markers, zoom-synced
  scroll speed, View toggles).
- `Edit/Screens/BottomBar/` — transport, summary timeline, Test button. F5/Test launches a
  `PlayScreen` with a serializer deep-clone of the WIP chart, starting 1500 ms before the playhead;
  returning seeks the editor clock to the gameplay exit time. Escape exits back to the editor.
- `Edit/Screens/Setup/` — metadata/resources/difficulty form. `Edit/Screens/Timing/` — timing-point
  list + settings + tap-timing + metronome (full osu timing-screen feature set: chip table, time
  signature / omit-barline, repeat nudge, use-current-time group move, section-wide object
  adjustment, tap-timing rows). `Edit/Screens/Verify/` — runs `ICheck`s (audio/background present,
  objects before time zero / beyond track end) and lists clickable seeking issues.
  `Edit/Screens/Design/` + `DesignTab` author tutorial-message design points.
- `Edit/EditorClipboard.cs` — cut/copy/paste/clone (clone deliberately does **not** touch clipboard
  content, matching osu).

## Mini preview

A small, silent, read-only live gameplay preview docked in the Compose workspace — it shows the chart
being authored actually *played* (notes travelling out and being hit at the ring), so spacing, timing,
design and scroll speed read as gameplay rather than as a timeline.

- `Edit/Preview/MiniPreview.cs` — the host. A **non-interactive** `GarbusPlayfield` on a clock **slaved
  to the `EditorClock`** (the same wiring `ComposeTab` uses for the composer), rendering the editor's
  **live `GarbusHitObject` instances directly — no clone** — as presentation-only `autoHit` drawables
  (see the auto-hit capability in [gameplay.md](gameplay.md)). Because auto-hit drawables are a pure
  function of clock time, the preview is **stateless** under seek/rewind: scrub anywhere and it is
  correct, no tracking beyond an add/remove map. The playfield renders at a fixed `ReferenceSize` (the
  canonical draw height) scaled uniformly to fit the panel each frame, so the fixed-pixel note sprites
  keep gameplay proportions in the small box; the host masks to a rounded rect matching the panel chrome.
- Constructed `GarbusPlayfield(interactive: false, miniStyle: true)`: no analog input manager or stick
  indicators, and a plain-arc `MiniWarningIndicatorDisplay` in place of the gameplay blurred glow.
- Live edits flow through the editor's existing change events. `HitObjectAdded` / `HitObjectRemoved`
  add / remove **and `Dispose()`** the drawable (the non-pooled zombie gotcha — same as the composer);
  `HitObjectUpdated` needs no drawable work because the shared instance's `ApplyDefaults` fires
  `DefaultsApplied`, re-applying the drawable **in place** (never recreated). Every event also rebuilds
  the playfield's chord index via `SetHitObjects` so chord tinting stays live; timing / design /
  scroll-speed reflect automatically because the drawables read shared state on re-apply. All
  subscriptions keep a field reference and are unsubscribed in `Dispose`.
- The preview's playfield installs the shared `SliderPathPool` exactly as gameplay does (see
  [gameplay.md](gameplay.md)), and scrubbing exercises the rent/return cycle constantly: seeking past a
  slider's lifetime kills the body (its rented paths return to the pool), seeking back revives it (it
  re-rents). Deleting an on-screen slider returns **and detaches** the paths in `OnKilled` before
  `removeDrawable`'s explicit `Dispose()`, so the pool's instances survive the zombie-prevention
  dispose. Pins: `TestSceneMiniPreview.TestSeekPastAndRewindRerentsPooledSliderPaths`,
  `TestDeletingVisibleSliderLeavesPooledPathsUsable`.
- `Edit/Preview/InlineChartPreviewPanel.cs` — the draggable docked chrome: bottom-right anchored,
  clamped to the Compose workspace, with a solid backdrop + border and persisted right/bottom offsets
  (`MiniPreviewX`/`MiniPreviewY` config). It lazily constructs the `MiniPreview` on first show.
- Wiring in `GarbusEditor`: a `View › Mini Preview` toggle (`MiniPreviewEnabled`, on by default), gated
  to show only on the Compose tab, and suspended while Test mode (or any other screen) owns the editor,
  then restored on resume.

## osu-framework background

DI/BDL caching, drawable lifetime + transforms (editor drawables refresh in place on
`HitObject.DefaultsApplied`), positional input + child-order precedence, `ScrollContainer` content vs
viewport anchoring, `PlatformAction` key handling, event-subscription lifetime. All covered in
[osu-framework.md](osu-framework.md) — the gotchas below are the editor-specific instances.

## Gotchas

- **A modal needs three separate mechanisms to actually own the keyboard**, and `ModalOverlay` has
  all three because each covers a case the others miss. `OnKeyDown` returning `true` unconditionally
  stops anything unbound from reaching an *ancestor* — that alone silences the editor's hotkeys, so a
  test that only checks ancestor hotkeys will pass even with the other two removed. Deriving from
  `FocusedOverlayContainer` takes focus on show, because `InputManager` re-appends the focused
  drawable to the end of the input queue (dispatched *first*) **after** blocking has filtered it — so
  a text box focused behind the dialog keeps eating keystrokes otherwise. `BlockNonPositionalInput`
  strips everything queued before the overlay, covering key bindings and non-ancestor handlers. Pins:
  `TestSceneFileSelectDialog.TestHostReceivesKeysOnlyWhileDialogIsClosed` (the ancestor case),
  `TestFocusedTextBoxBehindDialogStopsReceivingInput` (the focus case — probe with Backspace, not a
  letter; character entry travels the text-input path, not the key path, so `ManualInputManager.Key`
  never types).
- **Vertical `FillFlowContainer` collapses the tab content to zero height.** The tab area is a padded
  plain `Container` (bar heights reserved via `Padding`), never a fill flow. Pin:
  `TestSceneEditorShell.TestTabContentHasHeight`.
- **`EditorChart` aliases `Chart.HitObjects`** — never build a second list; mutating the alias is
  what `Save` serializes.
- **Removed composer drawables must be explicitly `Dispose()`d.** `HitObjectContainer` detaches with
  `RemoveInternal(…, false)` (correct for the pooled path); an undisposed editor drawable stays
  subscribed to `HitObject.DefaultsApplied` and re-runs `Apply()` on every later update — zombies
  pile up quadratically into a GC storm. Pin: `TestSceneComposeSelection.TestRemovedObjectDrawableIsDisposed`.
- **`EditorChart.Update` refreshes drawables IN PLACE** (`DefaultsApplied` → drawable re-`Apply()` +
  scrolling relayout) — never remove+recreate on update; recreating tore down framebuffer-backed
  slider visuals per drag event (the node-drag GC storm). Two dependencies: editor drawables must
  swallow drawable-side `LifetimeEnd` writes (else judged objects with no hit-state transforms expire
  at their own start time on re-apply and never come back — the scrolling container only re-lays-out
  ALIVE entries), and node/handle drags must skip `EditorChart.Update` when nothing changed. Pins:
  `TestSceneComposerLifecycle`, `TestSliderNodeDragDoesNotRecreateDrawable`,
  `TestUpdateRefreshesDrawableInPlace`.
- **Drag deltas can be a full wrap (±360°) off** when the cursor sits over a ghost twin —
  `GarbusSelectionHandler.HandleMovement` must reduce the degree delta via `MinimalDiff` so "already
  there" is 0, not a spurious ±360 that rebuilds every selected object per mouse-move. Pinned by the
  incremental-drag tests in `TestSceneComposeSelection`.
- **The horizontal drag delta is in GRID degrees, not absolute angle.** In the reversed view
  (`EditorAngleMapping.Direction == -1`) grid degrees run opposite to absolute angle, so
  `HandleMovement` must map the screen-derived delta through `Direction` before adding it to
  `AngleDeg` — otherwise a rightward drag rotates the wrong way and the object bounces. Pin:
  `TestDragRotatesTowardCursorInReversedView` (assert a *partial* drag direction — a full clean drag
  re-resolves to the cursor by coincidence and hides the bug).
- **Lambda event subscriptions leak.** Timeline/metronome components subscribe to
  `ControlPointInfo.ControlPointsChanged`, the clock, selection, `HitObjectUpdated`. Keep a field
  reference and unsubscribe in `Dispose`.
- **`config.GetBindable(...)` copies need an owner or they are GC'd.** `GetBindable` returns a bound
  copy the config holds only *weakly*, and `BindTo` links weakly in both directions — so a copy kept
  alive only by a local, a closure that captures something else, or another bindable's bind link
  disappears at the next GC and the setting silently stops propagating. Store it in a field.
  `ToggleMenuItem` now owns the bindable handed to its constructor for exactly this reason (a menu
  toggle whose copy was collected keeps flipping its own checkbox while writing nowhere, so it reads
  as an inert setting). Pins: `TestSceneEditorViewMenuConfig`, `TestSceneComposeTabConfig`.
- **The top/bottom bars must come AFTER the tab container in `GarbusEditor`'s child list** (osu's
  order: content first, bars after). The compose blueprint stack claims positional input over the
  whole screen (`ReceivePositionalInputAt => true`), so bars listed earlier never receive clicks and
  menu dropdowns draw behind the tab content. Pins: `TestSceneEditorShell.TestTabSwitchingViaClick`,
  `TestFileMenuSaveViaClick`.
- **Wheel-seek convention:** wheel-down (negative `ScrollDelta.Y`) = forward in time, wheel-up =
  backward (matches osu's `Editor.cs`).
- **Fixed overlays on the `TimelineStrip` must use `AddInternal`, not `base.Content.Add`.** A
  `ScrollContainer`'s `base.Content` scrolls and auto-sizes to the full track width, so
  `Anchor.TopCentre` there pins to the track midpoint and drifts. The centre-marker playhead lives in
  `AddInternal`. Pin: `TestSceneTimeline.TestCentreMarkerPinnedToViewportCentre`.
- **`TransferBlueprintFor` runs on `HitObjectUpdated`** so a re-defaulted object keeps its selection
  blueprint (updating an object regenerates nested objects, so the blueprint must be re-pointed).
- **Placement auto-seek:** `HitObjectPlacementBlueprint.EndPlacement` seeks the clock to the placed
  object — wait for the seek before asserting screen positions in tests.
- **Placement replacement is scoped to the SAME object type; sliders never replace at all.**
  `EndPlacement` removes every existing object for which `ReplacesExistingObject` is true.
  `GarbusPlacementBlueprint` matches only an existing object of the *same runtime type* at the same
  angle+time (`ShoulderNote`/`ShoulderHoldNote` add a same-side check on top). Different types are
  designed to stack (see [ObjectStacking.md](../presentation-specs/ObjectStacking.md)), so without the
  type gate a slam or note dropped on a slider head deletes the whole slider — the head shares the
  `SliderBody`'s angle+time. `SliderPlacementBlueprint` overrides it to a flat `false`: a slider
  extends over time along a path, so two sliders sharing only a head angle+time are distinct objects
  and replacing would silently destroy the existing slider's authored path. Pins:
  `TestSceneComposePlacement.TestSlamOnSliderHeadKeepsSlider`, `TestSlamOnSlamStillReplaces`,
  `TestSliderOnSliderHeadKeepsBoth`.
- **Slider node selection is local to the blueprint.** Node handles receive input only while the
  slider is selected, so clicking a node on an unselected slider selects the whole slider; once
  selected, click picks a node (Ctrl toggles), and dragging one moves the whole node selection.
  Delete (via `IKeyBindingHandler<PlatformAction>`, seen before `SelectionHandler`) and
  `HandleQuickDeletion` (Shift+RightClick) remove nodes; emptying the path removes the slider.
- **A head-only slider (zero control points) is ineligible for head-node selection** — its head *is*
  the whole object, so the head handle disables interaction (in `Update`, gated on
  `controlPoints.Count > 0`) and declines mouse-down/drag, letting the click/drag flow to
  `BlueprintContainer` for whole-object select + group move. Without this, dragging one of several
  selected head-only sliders moved only that one. Pin:
  `TestDraggingOneOfSeveralSelectedHeadOnlySlidersMovesAll`.
- **A drag handle disposed mid-drag drops its `OnDragEnd`, stranding the change transaction.** A
  node/head drag opens a transaction (`changeHandler.BeginChange()`) closed on `DragEnded`.
  `SliderSelectionBlueprint.Update` rebuilds the handle set every frame and disposes trailing handles
  when a wrap copy drops — which can dispose the handle *currently* being dragged as the node crosses
  the seam. The framework never delivers `OnDragEnd` to a disposed drawable, so `EndChange` never runs
  and `TransactionActive` sticks `true` forever, silently killing Undo/Redo. Fix: the drag pieces
  (`NodeDragPiece`/`EditSquarePiece`/`HoldEndDragPiece`) fire `DragEnded` from *either* `OnDragEnd` or
  `Dispose`, whichever comes first, guarded to fire exactly once (a double `EndChange` throws). Pin:
  `TestNodeDragThatDropsWrapCopyDoesNotStrandTransaction`.
- **The compose judgement line is raised 40px above the playfield bottom** (`JUDGEMENT_LINE_OFFSET`).
  Every time-scrolling layer keys its trailing edge off its own `DrawHeight`, so ALL must share that
  bottom inset or they desync: the `HitObjectContainer` (inner padded container), `EditorBarLineDisplay`,
  and the beat-snap grid's `UnderlayElements`. The static grid backdrop stays full height. The exposed
  negative-time region is why `HitObjectPlacementBlueprint` rejects `StartTime < 0`. Pin:
  `TestSceneComposePlacement.TestPlacementInHitZoneRejectsNegativeTime`.
- **A hold note's head sprite straddles the start line**, so hit-testing must explicitly cover the
  head sprites (incl. the ghost twin) or the head's bottom half hangs outside the hit rectangle and
  can't be clicked. Pin: `TestHoldNoteSelectableByHead`.
- **Editor drawables only play samples while their clock (the `EditorClock`) is running**, so
  scrub-seeks past objects are silent while normal playback still sounds. Slider-node / hold-head
  stubs (`EditorDrawableNestedStub`) need the same gate — they don't derive from the gated editor
  base.

### Test harness note

The compose placement/selection harnesses must wire the composer subtree's `Clock` to the
`EditorClock` (as `ComposeTab` does), or the playfield maps time↔position against the ambient wall
clock and editor-time-relative behaviour (e.g. the hit zone at time 0 mapping to negative times)
can't be reproduced. See [testing.md](testing.md).
