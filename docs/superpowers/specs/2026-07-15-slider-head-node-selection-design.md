# Slider head-node selection — design

Date: 2026-07-15
Status: implemented

Builds on `2026-07-10-slider-node-selection-design.md` (node selection inside
`SliderSelectionBlueprint`). Read that first — this spec only covers extending the node-selection
model to the slider's head.

## Problem

The slider head is not a node in the data model — it is implicit. `GarbusSliderPath` defines the
path as "the implicit head at time 0 followed by control points"; the head's time and angle *are*
the slider's `StartTime`/`AngleDeg`, and every `GarbusPathControlPoint` carries offsets relative
to it. Consequently:

- There is no object a `HashSet<GarbusPathControlPoint>` selection could hold for the head, so
  the head can never be picked, dragged as a node, deleted, eased, or included in a node flip.
- In the blueprint the head is a passive `EditSquarePiece`: clicking it selects the whole slider,
  dragging it rigid-moves the whole slider. There is no way to retime/re-angle the slider's start
  while leaving the rest of the path in place.
- The BAC source has the same design — nothing to port; this is new work.

## Decisions (confirmed with user)

- **Sentinel head selection (Approach A)**: head-selection state lives inside
  `SliderSelectionBlueprint` as a flag beside `selectedNodes`. The implicit-head data model, chart
  format, undo diff, and clipboard are untouched. (Materialising the head as `ControlPoints[0]`
  was rejected: it ripples through the format DTOs, nested-object generation, path validation,
  placement, and gameplay drawables, and the "first node is always 0/0" invariant would need
  enforcement anyway.)
- **Head drag = move the start, keep the tail** (osu's first-control-point behaviour): dragging
  the head mutates `StartTime`/`AngleDeg` and compensates unselected nodes so they stay put.
  Rigid whole-slider drag remains available via the outline/body (and via head + all nodes
  selected, which degenerates to a rigid move).
- **Head delete = promote the first control point** to be the new head.
- **The head has no `SweepEasing`** (easing lives on the segment *into* a node; the head has no
  incoming segment) — easing operations skip it.
- First slice may ship select + drag only, deferring delete/flip; the spec covers all of it.

## Design

### Selection state

- `SliderSelectionBlueprint` gains `private bool headSelected` and exposes
  `internal bool HeadSelected`.
- Lifecycle mirrors `selectedNodes`: cleared in `OnDeselected()` and by a body/outline click
  (`OnClick`). It is a plain bool, so undo/redo orphan-pruning needs no extension — the head
  always exists while the slider does.
- Selection semantics unify with nodes:
  - plain click on the head handle → clear `selectedNodes`, select only the head;
  - Ctrl+click → toggle the head in/out of the combined selection;
  - plain click on an already-selected head inside a multi-selection keeps the group (so a drag
    moves it all) — same rule as `selectNode`.

### Head handle

- The passive `EditSquarePiece head` is replaced by interactive head handles wired like
  `NodeDragPiece` (`SelectRequested`/`DragStarted`/`Dragging`/`DragEnded`), using the sentinel
  **`CpIndex = -1`** so all callbacks route through the same blueprint methods. Visual: keep the
  square (head) vs circle (node) distinction — either a parameterised `NodeDragPiece` shape or a
  small `HeadDragPiece` sibling; selected state = solid fill, exactly like nodes.
- Handles must consume `OnMouseDown` (as `NodeDragPiece` does) so the blueprint's `OnClick`
  doesn't clear the selection and `BlueprintContainer` doesn't run selection-cycling.
- **One head handle per visible wrap copy**, allocated from the same `wrapCopiesBuffer` loop that
  places node handles (position: grid offset 0, y = `DrawHeight`). Today the head square has no
  ghost-band twin at all — a seam-adjacent head is only clickable via the outline — so this is
  also a small parity fix.
- Handles only receive input while the slider blueprint is selected (existing gate), so "click
  head on an unselected slider selects the whole slider" is preserved for free.
- `ReceivePositionalInputAt`, `ScreenSpaceSelectionPoint`, and `FinalNodeScreenPosition`'s
  no-nodes fallback re-point at the primary (`WrapK == 0`) head handle.

### Dragging

`dragNode` generalises to a moved set **M** that may contain the head. M = the combined selection
(head flag + `selectedNodes`) when the grabbed handle's node is in it, else just the grabbed
node/head (which becomes the sole selection on mouse-down, per existing `selectNode` rules).

The grabbed handle defines the snapped deltas, exactly as today: Δt from `result.Time` (for the
head, `Δt = proposedTime − StartTime` since its offset is 0), Δa via
`EditorAngleMapping.MinimalDiff` on its absolute angle.

- **Head ∉ M** — unchanged from today: selected nodes get `TimeOffset += Δt`,
  `RotationOffset += Δa`.
- **Head ∈ M** — the head moves, offsets are head-relative, so:
  - `StartTime += Δt`; `AngleDeg = NormalizeDeg(AngleDeg + Δa)`;
  - nodes **in** M: offsets unchanged (they ride along with the head);
  - nodes **not in** M: `TimeOffset −= Δt`, `RotationOffset −= Δa` (absolute position fixed).
- **Validity (time, all-or-nothing per event)**: build the prospective offset list
  (`cp ∈ M ? offset : offset − Δt` in the head-in-M case) and require
  `GarbusSliderPath.AreTimesValid`, **plus `StartTime + Δt ≥ 0`** (the hit-zone rule placement
  already enforces — objects may not start before time 0). `timeShiftValid` grows a
  head-in-set variant.
- Angle applies unconditionally when Δa ≠ 0 (offsets are free integers), as today.
- Existing guards stay: one `BeginChange`/`EndChange` per gesture; `EditorChart.Update` fires only
  when something changed (the in-place-refresh gotcha).

Note this covers the inverse case too: dragging a *node* handle while the head is part of the
multi-selection puts the head in M, so the head moves and unselected nodes are compensated — one
transform routine, not a special case.

### Deletion (head promotion)

`removeNodes` gains head-awareness (Delete key path and `HandleQuickDeletion` on a hovered head
handle both feed it):

- Selection covers the head **and** every control point → remove the slider from the chart
  (existing empty-path rule).
- Head in the set, at least one control point survives:
  1. remove the selected control points as today;
  2. promote the new first control point `cp0`: `StartTime += cp0.TimeOffset`,
     `AngleDeg = NormalizeDeg(AngleDeg + cp0.RotationOffset)`, remove `cp0`, then rebase every
     remaining node: `TimeOffset −= cp0.TimeOffset`, `RotationOffset −= cp0.RotationOffset`.
  3. `cp0`'s `SweepEasing`/`Smooth` are discarded — its incoming segment no longer exists (the
     head has none).
- Removing nodes cannot break the ordering invariant (a merged link is only zero-length if the
  invariant was already violated), matching the existing no-revalidation stance in `removeNodes`.
- One `BeginChange`/`EndChange` transaction; `selectedNodes`/`headSelected` cleared after.

### Easing

- Ctrl+Q/W/E/R (`setSelectedNodesEasing`) and the Inspector's Easing dropdown act on
  `selectedNodes` only; a selected head neither blocks them nor receives easing. The dropdown's
  visibility condition stays "≥ 1 *control point* selected" — a head-only selection shows no
  Easing control.

### Inspector

- `collectSelectedNodes` can't see the head; the per-frame change poll must also observe head
  state or a head-only pick never triggers a rebuild. Extend the snapshot with the set of
  `SliderBody` items whose blueprint reports `HeadSelected` (poll
  `SelectionHandler.SelectedBlueprints` the same way).
- "Selected Nodes" count includes the head (it *is* node 0 in user terms).

### Flip / rotate (`GarbusSelectionHandler`)

- Guards `sb.SelectedNodes.Count > 0` (in `flip` and `handleAngles`) become
  `… > 0 || sb.HeadSelected`.
- `handleAngles` yields the head's absolute angle (`slider.AngleDeg`) when the head is selected.
- `reflectSelectedNodes` with the head in the selection: reflection in head-relative offset space
  about sum `S` maps offset `x → S − x`; the head sits at offset 0 → it moves by `S`. Expressed as
  mutations (unselected nodes stay at fixed absolute angles):
  - `AngleDeg = NormalizeDeg(AngleDeg + S)`;
  - selected nodes: `RotationOffset = −RotationOffset` (reflected, then re-based to the new head);
  - unselected nodes: `RotationOffset −= S`.
  This stays a winding-preserving involution for `SelectionCentre` mode (applying it twice
  recomputes `S' = −S` and restores every offset); for `AroundPivot` it remains involutive for
  axis-aligned pivots, same caveat as the existing node flip.
- Head + every node selected degenerates to the existing rigid whole-slider mirror.

### Out of scope (YAGNI)

- Any data-model change (head stays implicit; no format/serialization change).
- Head participation in the clipboard or global selection.
- Cross-slider head/node mixed operations beyond what the flip loop already does per blueprint.

## Files touched

- `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs` — head-selected state, head handles
  (per wrap copy), unified drag transform with head compensation, delete-with-promotion,
  selection-point/final-node re-pointing.
- `Garbus.Game/Edit/Blueprints/Components/NodeDragPiece.cs` (or a new `HeadDragPiece`) — square
  head visual with selected fill, sentinel index.
- `Garbus.Game/Edit/Inspector.cs` — head-aware node-selection poll/snapshot and count.
- `Garbus.Game/Edit/GarbusSelectionHandler.cs` — flip guards, `handleAngles`, head reflection.
- `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` — coverage below.

No chart-format, serialization, or gameplay changes.

## Test plan (headless)

1. Click the head of an unselected slider → whole slider selected, head not node-selected
   (existing behaviour preserved).
2. Click the head handle of a selected slider → head selected, slider stays selected; Inspector
   node count includes it.
3. Ctrl+click head then a node → both in the selection; Ctrl+click head again → toggled off.
4. Drag the head handle → `StartTime`/`AngleDeg` change, unselected nodes keep their absolute
   time/angle (offsets compensated), drawable refreshed in place (not recreated).
5. Head drag rejects `StartTime < 0` and any move breaking `AreTimesValid`; angle still applies
   when only time is blocked (all-or-nothing per component, matching node drag).
6. Drag a node while head + that node are selected → rigid move of the pair; a fully-selected
   slider (head + all nodes) rigid-moves like a body drag.
7. Delete with head selected → first control point promoted (StartTime/AngleDeg absorb its
   offsets, remaining nodes rebased); undo restores.
8. Delete head + all nodes → slider removed from the chart.
9. Shift+RightClick a hovered head handle → head deleted via promotion.
10. Flip with head + node subset selected → reflected result keeps unselected nodes at fixed
    absolute angles; flipping twice restores the original (involution).
11. Head handle appears once per visible wrap copy for a seam-adjacent slider; clicking a
    ghost-band head copy selects the head.
12. Deselecting the slider clears head selection; body-line click clears it too.
