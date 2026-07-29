# Shift+drag node-selection box

## Goal

Let box selection (click-and-drag) select slider **nodes and heads** on already-selected
sliders, so an author can grab several control points at once instead of Ctrl+clicking them one
by one. The gesture is **Shift+drag**, a dedicated modifier that never touches whole-object
selection during the drag.

## Why Shift (not Ctrl)

`Shift + left-drag` is unused in the compose Select-tool path — nothing in
`BlueprintContainer.OnDragStart` keys on it (only `ControlPressed`). The only Shift bindings
nearby are `Shift+RightClick` (quick-delete), Shift-during-*placement* (flips the slam edge),
and keyboard `Ctrl+Shift+Z` / `Shift+1–9` — none conflict with a Select-tool left-drag.

Using Shift keeps **Ctrl+drag** meaning what it means today ("add whole objects to the
selection"), and makes Shift a clean "node box" modifier. Within node mode, **Ctrl** layers on
as the combine-vs-replace switch (below).

## Interaction model

Precondition for node mode: the **Select tool** is active and **≥1 slider is selected** that has
node targets (≥1 control point). Under those conditions, a `Shift`-held drag that begins in
**empty space** (not over a slider outline or handle — those still move the slider / drag a node,
exactly like the object box) enters node mode.

While in node mode the drag draws the normal drag box but applies it to nodes:

- **Replace (Shift only):** the box *is* the node selection. On drag start every selected
  slider's node selection is cleared; each frame, node/head handles inside the box are selected
  and handles outside are deselected. **On release, any selected slider that ended up with no
  selected node/head is deselected entirely.** Non-slider objects (notes) in the selection are
  left untouched.
- **Combine (Shift+Ctrl):** the existing node selection is preserved and boxed handles are added.
  A handle first added by the box and then dragged back out of the box drops again; nodes that
  were selected before the drag stay selected. No whole-object pruning happens in combine mode.

Details and edges:

- **Heads** are selectable by the box, except on a **head-only slider** (zero control points),
  where the head is not an independent node target. Such a slider is ignored by the node box
  entirely — never node-selected, never pruned — matching how clicking its head falls through to
  whole-object handling.
- An **empty Shift+drag** in replace mode selects no nodes and therefore prunes every selected
  slider (consistent with an empty normal box clearing the selection).
- **No slider selected** → a harmless no-op box (nothing is selected or changed).
- **Multiple selected sliders:** the box is spatially honest — it selects node/head handles from
  every selected slider it covers, and (replace mode) prunes the selected sliders it doesn't.

### Modifier evaluation timing (invariant)

Both modifiers are read **once at `OnDragStart`** and latched for the whole drag — never re-read
per frame. This matches the existing object box, where `Ctrl` is captured into
`selectionBeforeDrag` at drag start and `UpdateSelectionFromDragBox` never looks at
`e.ControlPressed` again. Pressing or releasing Shift/Ctrl mid-drag does not change the mode.

## Architecture

All wiring lives in `Garbus.Game/Edit/GarbusBlueprintContainer.cs` (the concrete
`ComposeBlueprintContainer`). The vendored `BlueprintContainer<T>` is **not** modified — the seam
is the already-`virtual` `UpdateSelectionFromDragBox`, plus overrides of the framework
`OnDragStart` / `OnDragEnd`.

State latched on `GarbusBlueprintContainer` for the duration of a drag:

- `bool nodeDragBoxActive`
- `bool nodeDragCombine`

Flow:

1. **`OnDragStart`** — call `base.OnDragStart(e)`. If the base engaged the box
   (`DragBox.State == Visibility.Visible`, i.e. it wasn't a blueprint-move) **and** `e.ShiftPressed`
   **and** at least one selected `SliderSelectionBlueprint` has node targets: set
   `nodeDragBoxActive = true`, `nodeDragCombine = e.ControlPressed`, and call
   `BeginNodeDragBox(nodeDragCombine)` on each selected slider blueprint. (Gate node mode to the
   Select tool so an active placement tool never triggers it.)
2. **`UpdateSelectionFromDragBox(selectionBeforeDrag)`** (override) — if `nodeDragBoxActive`,
   forward `DragBox.Box.ScreenSpaceDrawQuad` to `UpdateNodeDragBox(quad)` on each selected slider
   blueprint and return; otherwise `base.UpdateSelectionFromDragBox(...)`.
3. **`OnDragEnd`** — capture `wasNodeDrag = nodeDragBoxActive` and `replace = !nodeDragCombine`,
   call `base.OnDragEnd(e)` (hides the box), then: if `wasNodeDrag && replace`, remove from
   `SelectedItems` every selected slider whose `HasNodeSelection` is false; call
   `EndNodeDragBox()` on each selected slider blueprint; clear `nodeDragBoxActive`.

The whole-object selection is deliberately **not** mutated during the drag (only at end, by
replace-mode pruning), so every originally-selected slider keeps its handles eligible for the box
for the whole gesture.

### New members on `SliderSelectionBlueprint`

Node selection stays local to the blueprint (the existing `selectedNodes` set + `headSelected`
flag / osu `PathControlPointVisualiser` pattern). Add:

- `internal void BeginNodeDragBox(bool combine)` — if `combine`, snapshot the current
  `selectedNodes` (+ `headSelected`) into a before-drag set; else clear the node selection and use
  an empty snapshot.
- `internal void UpdateNodeDragBox(Quad screenQuad)` — for each control point (via its node
  handles, any wrap copy) and the head handle (when it is a node target), set
  `selected = handleCentreInQuad || snapshot.Contains(...)`. Reads existing handle drawables'
  `ScreenSpaceDrawQuad.Centre` (1-frame stale is imperceptible for a live drag).
- `internal void EndNodeDragBox()` — clear the snapshot.
- `internal bool HasNodeSelection => headSelected || selectedNodes.Count > 0;`

Hit-testing maps handle → control point by the handle's `CpIndex` (`controlPoints[CpIndex]`), the
same index the handles already carry; the head handle uses the `headSelected` flag.

## Testing

Add coverage to `Garbus.Game.Tests` `TestSceneComposeSelection`:

- Shift+drag box over a subset of a selected slider's nodes selects exactly those.
- Head selected when the box covers the head handle; head-only slider is ignored by the box.
- Replace mode clears a prior node selection and prunes selected sliders with no boxed node.
- Combine mode (Shift+Ctrl) preserves the prior node selection, adds boxed nodes, and prunes
  nothing.
- Multiple selected sliders: one box selects nodes across all covered sliders; uncovered selected
  sliders are pruned in replace mode.
- Modifier latching: mode is fixed at drag start (releasing Shift mid-drag keeps node mode).

No Tuning scene — the feature adds no new visual element (it reuses the existing `DragBox` and
node/head handles).

## Out of scope

- Moving nodes via the box (the box only selects; dragging a handle still moves as today).
- Box-selecting nodes on sliders that are **not** already selected (node mode only narrows within
  the already-selected set; it never adds a new slider to the selection).
- Any change to Ctrl+drag object-selection behavior when no slider is selected.
