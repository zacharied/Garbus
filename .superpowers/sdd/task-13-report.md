# Task 13 report — Vendor composer bases + toolbox components

## Status: COMPLETE

Build clean (0 errors, 7 pre-existing warnings, no new). Tests 51/51 green.

## Structure chosen and why

The one big structural fact: **Garbus has two different playfields.** The gameplay
`GarbusPlayfield` → `Ring` → `Lane` stack is *radial* and uses a bespoke
`GarbusScrollingHitObjectContainer` (in `Garbus.Game/UI/`) with its own plain `GarbusScrollingInfo`
(NOT an `IScrollingInfo`, not DI-cached). But the **editor** playfield is entirely separate: BAC's
`BacEditorPlayfield : ScrollingPlayfield` (Task 14 target) is a standard vertically-scrolling strip
(y = time, x = unrolled circle) built on osu's real `ScrollingPlayfield` /
`ScrollingHitObjectContainer` (Garbus's vendored ones in `Gameplay/UI/Scrolling/`), which resolve
`IScrollingInfo` via DI. So the composer's `Playfield` is a **`ScrollingPlayfield`**, and the snap
math works against `TimeAtScreenSpacePosition` / `ScreenSpacePositionAtTime`.

In osu, the `DrawableRuleset` (via `DrawableEditorRulesetWrapper`) creates the playfield, caches
`IScrollingInfo`, and does the hit-object drawable lifecycle. Garbus has no `DrawableRuleset`, so all
three responsibilities move into `ScrollingHitObjectComposer<T>`:

- **Playfield hosting:** the base `HitObjectComposer` lays out `LeftToolbox | PlayfieldContentContainer(playfield + blueprint overlay) | RightToolbox` and references `Playfield` directly (abstract). `ScrollingHitObjectComposer<T>` creates it via `CreatePlayfield()`.
- **IScrollingInfo ownership:** `ScrollingHitObjectComposer<T>.CreateChildDependencies` caches a composer-owned `EditorScrollingInfo` (new file — mirrors `DrawableScrollingRuleset.LocalScrollingInfo`: direction=Down, constant algorithm, time-range bindable). The playfield and `BeatSnapGrid`'s `DrawableGridLine` resolve it.
- **Non-pooled drawable lifecycle:** create-on-`HitObjectAdded`, remove-on-`HitObjectRemoved`, remove+re-create on `HitObjectUpdated`, initial population from `EditorChart.HitObjects` — exactly PlayScreen's non-pooled pattern via `CreateDrawableRepresentation(T)`.
- **TimelineTimeRange** (`Bindable<double>`, written by Task 17) → piped into `EditorScrollingInfo.TimeRangeBindable` (= `IScrollingInfo.TimeRange`).

**Playfield lazy-creation gotcha:** BDL runs base-first, so the base composer's BDL references
`Playfield` *before* the derived BDL runs. Solved by making `Playfield => playfield ??= CreatePlayfield()`
lazy in the derived class rather than assigning in derived BDL.

## Vendored vs fresh

| File | Origin |
|------|--------|
| `RadioButton.cs` | Vendored verbatim (pure model). |
| `EditorRadioButtonCollection.cs` (+ `EditorRadioButton`) | Vendored; `OsuButton`→`BasicButton`, `OverlayColourProvider`/`OsuColour`→hardcoded `Colour4`, `OsuSpriteText`/`SpriteIcon`→framework primitives. Radio semantics (one-selected, `Select()`/`Deselect()` wiring) preserved verbatim. |
| `HitObjectCompositionToolButton.cs` | Vendored verbatim (RadioButton carrying a CompositionTool). |
| `BeatSnapGrid.cs` | Vendored; `EditorBeatmap`→`EditorChart` for ControlPointInfo, `OsuColour.GetColourFor`→local hardcoded divisor palette, targets Garbus's vendored `ScrollingHitObjectContainer`. Beat-walk/fade logic verbatim. |
| `ExpandingToolboxContainer.cs` | **Fresh** ("Modeled on"). osu's derives from `ExpandingContainer` (entangled: OsuScrollContainer, editor config, hover animation). Rewritten as a fixed-width vertical `FillFlowContainer` (matches brief's `FillFlowContainer` typing). Hover expand/contract polish deferred. |
| `EditorToolboxGroup.cs` | **Fresh** ("Modeled on"). osu's extends `SettingsToolboxGroup` (animated, OsuColour-bound). Rewritten as a titled box with a single `Child` — keeps osu's `Child = ...` API so ported BAC code compiles unchanged. |
| `EditorScrollingInfo.cs` | **Fresh** ("Modeled on" `DrawableScrollingRuleset.LocalScrollingInfo`). The IScrollingInfo the composer caches in lieu of a DrawableRuleset. |
| `HitObjectComposer.cs` | **Fresh rework** ("Modeled on"). Extended from Task 12 stub. Dropped: Ruleset, IBeatSnapProvider, OverlayColourProvider, ternary/sample banks, toggles(Q~P), composer-focus fade, IPlacementHandler, DrawableRuleset. Kept: tool radio collection, number-key selection, toolbox columns, blueprint overlay, snapping. |
| `ScrollingHitObjectComposer.cs` | **Fresh rework** ("Modeled on"). See structure above. |

## CurrentTool / ActiveTool reconciliation

Task 12's stub had an **abstract `CompositionTool? CurrentTool` on the composer that no consumer
reads** — the blueprint containers read `HitObjects`/`Playfield`/`CursorInPlacementArea`, and hold
their OWN settable `ComposeBlueprintContainer.CurrentTool` (that's the one that actually drives
placement). I replaced the unused abstract with the brief's **`ActiveTool` (`Bindable<CompositionTool?>`)**
as the source of truth, plus a `CurrentTool => ActiveTool.Value` convenience getter for callers.
`toolSelected(tool)` writes `ActiveTool.Value` AND pushes `blueprintContainer.CurrentTool = tool`, so
placement is unchanged. Number keys 1-9 select via the radio collection (index order, 1 = the
auto-prepended Select tool). Making a selection re-selects the Select tool (osu behaviour).

## BigAssCircleHitObjectComposer mental-compile listing (every consumed base member → present)

- `: base(ruleset)` ctor → base is parameterless (no `Ruleset` in Garbus). Task 15's port drops `: base(ruleset)`. **Documented deviation.**
- `public new BacEditorPlayfield Playfield` → base `Playfield` is `abstract`, overridden in `ScrollingHitObjectComposer<T>`; Task 15 can `new`-shadow. ✓
- `CreateBlueprintContainer()` → `ComposeBlueprintContainer` ✓
- `CreateBeatSnapGrid()` → `BeatSnapGrid?` (virtual) ✓
- `CompositionTools` → `IReadOnlyList<CompositionTool>` (abstract) ✓
- `LeftToolbox.Add(new EditorToolboxGroup(...))` → `LeftToolbox` is `ExpandingToolboxContainer : FillFlowContainer`, `.Add` works ✓
- `EditorToolboxGroup` `{ Child = ... }`, `EditorRadioButtonCollection` `{ Items = ... }`, `RadioButton(label, action)`, `.Items[i].Select()` ✓
- `FindSnappedPositionAndTime(Vector2)` → `SnapResult` with `.Playfield` (as `ScrollingPlayfield`), `.ScreenSpacePosition`, `.Time` ✓
- `TimelineTimeRange` (`Bindable<double>`) → present; Task 15 writes the composer's own bindable instead of `drawableRuleset.TimelineTimeRange`. **Documented deviation.**
- `EditorClock.TrackLength`, `EditorClock` resolved ✓
- `CreateDrawableRuleset(...)` → **removed** (no DrawableRuleset); replaced by `CreatePlayfield()` + `CreateDrawableRepresentation(T)`. Task 15's port swaps the DrawableRuleset factory for these two. **Documented deviation.**

Blueprint containers (Task 12) still compile against the extended composer: they consume
`Composer.HitObjects` / `Composer.Playfield` / `Composer.CursorInPlacementArea` — all present and now
concrete in `ScrollingHitObjectComposer<T>`. `ComposeBlueprintContainer.CurrentTool` (its own settable
property) is written by `toolSelected`. ✓

## ReceivePositionalInputAt

Left as hardcoded `true` (osu's pattern — blueprints can be partially offscreen while scrolling). The
blueprint container is now a child of `PlayfieldContentContainer`, which bounds it spatially, so the
`true` only matters for the partial-offscreen case — same as osu. Not changed; documented.

## Build + test evidence

- `dotnet build Garbus.Desktop.slnf` → Build succeeded, 0 errors. Clean-rebuild warning set is
  identical to the pre-task baseline (7 warnings, all in unrelated pre-existing files:
  DrawableHoldNoteHead, GarbusEditor, HoldNote, OpenChartDialog, SaveAsDialog, 2× TestScene). No new
  warnings from Task 13 files.
- `dotnet test Garbus.Game.Tests` → `Passed! Failed: 0, Passed: 51, Skipped: 0, Total: 51`.

## Fix: drawable lifecycle

**Bug:** `removeHitObject` and `updateHitObject` both called `Playfield.Remove(GarbusHitObject)` — the
pooling/entryManager overload. Since `entryManager` is never populated by the non-pooled Add path, this
was a silent no-op: deleted objects' drawables stayed on screen; every Update added a duplicate drawable
without removing the old one.

**Fix in `ScrollingHitObjectComposer<T>`** (`Garbus.Game/Edit/Compose/ScrollingHitObjectComposer.cs`):
- Added `private readonly Dictionary<GarbusHitObject, DrawableHitObject> drawableMap`.
- `addHitObject`: stores the created drawable in the map before calling `Playfield.Add(drawable)`.
- `removeHitObject`: looks up the drawable, removes it from the map, then calls `Playfield.Remove(drawable)`
  (the `DrawableHitObject` overload — routes correctly through `HitObjectContainer.Remove`).
  The pre-existing vendored quirk of `Playfield.Remove(DrawableHitObject)` returning `false` on success
  is noted and the return value is deliberately ignored; `Playfield.cs` was not changed.
- `updateHitObject`: delegates to `removeHitObject` then `addHitObject` — clean remove+recreate.
- `Dispose`: clears the map.

**New test** (`Garbus.Game.Tests/Editor/TestSceneComposerLifecycle.cs`): headless `GarbusTestScene`
with an in-file `ComposerTestHarness` that caches all required DI deps (`EditorChart`, `EditorClock`,
`BindableBeatDivisor`) and hosts a minimal concrete `MinimalComposer : ScrollingHitObjectComposer<MinimalNote>`.
Three lifecycle tests:
1. `TestAddCreatesExactlyOneDrawable` — add via `editorChart.Add` → exactly 1 drawable.
2. `TestUpdateProducesExactlyOneDrawable` — update → still exactly 1, AND it is a new instance.
3. `TestRemoveLeaveZeroDrawables` — remove → 0 drawables.

All 3 (+ the auto-generated `TestConstructor`) passed on first run. Full suite: 55/55 green (51 prior + 4 new).

## Concerns / notes for downstream tasks

- **Task 15** must: drop `: base(ruleset)`; implement `CreatePlayfield()` (returns `BacEditorPlayfield`
  equivalent) + `CreateDrawableRepresentation(GarbusHitObject)` (the editor drawables) instead of
  `CreateDrawableRuleset`; write the composer's `TimelineTimeRange` bindable (Task 17 zoom sync) rather
  than a DrawableRuleset property.
- **Task 14** owns the Garbus editor playfield (`ScrollingPlayfield` subclass with `UnderlayElements`
  for the beat snap grid target, per `BacBeatSnapGrid`/`BacEditorPlayfield`).
- `ExpandingToolboxContainer` hover expand/contract animation is intentionally dropped (Phase 5 polish);
  the column is always full width.
- `EditorScrollingInfo.Direction` is fixed to `Down` (mania-style, judgement at bottom). If Task 14's
  editor playfield wants a different direction, expose it there.
