# Slider node selection — design

Date: 2026-07-10
Status: approved approach (Option A), spec pending user review

## Problem

The editor can only select a slider as a whole. Node handles (`NodeDragPiece`) exist and are
drag-movable once the slider is selected, but there is no concept of a *selected node* — and
consequently no way to delete a node at all (`T` inserts, drag moves, nothing removes).

## Decisions (confirmed with user)

- **Approach A**: node selection lives inside `SliderSelectionBlueprint`, mirroring osu's
  `PathControlPointVisualiser` pattern. No changes to the global selection model
  (`EditorChart.SelectedHitObjects`), undo diff, or clipboard.
- **osu click model**: clicking a node on an *unselected* slider selects the whole slider only.
  Node picking requires the slider to already be selected (two clicks to reach a node).
- **Operations**: Delete key removes selected node(s); Ctrl+click multi-selects nodes within a
  slider; dragging a selected node moves all selected nodes together.

## Design

### Selection state

- `SliderSelectionBlueprint` owns a `HashSet<GarbusPathControlPoint> selectedNodes` (reference
  identity — control points are stable instances; `SliderBody.GetSegmentStartTime` already relies
  on this).
- Pruned every `Update()` against `HitObject.Path.ControlPoints` (drops references orphaned by
  undo/redo's JSON-diff restore — node selection silently clearing across undo is acceptable).
- Cleared in `OnDeselected()` (slider deselected → no node selection).
- `NodeDragPiece` gains a selected visual state: filled circle when selected vs the current
  border-only ring. The blueprint pushes the flag each `Update()`.

### Click routing

- `NodeDragPiece` handles `OnMouseDown` (returns true, osu-style — this stops
  `BlueprintContainer.performMouseDownActions` from running selection-cycling for clicks that land
  on a handle):
  - plain click → select only this node;
  - Ctrl+click → toggle this node in/out of the node selection.
- Handles only receive input while the slider blueprint is selected (the vendored
  `SelectionBlueprint.ShouldBeConsideredForInput` gate), which produces the chosen osu behaviour
  for free: on an unselected slider every hit — line or node — selects the whole slider.
- Clicking the outline/head of a selected slider: `SliderSelectionBlueprint` overrides `OnClick`
  to clear `selectedNodes` and returns `false` so `BlueprintContainer`'s existing click behaviour
  (keep selection, cycling, etc.) is untouched.

### Dragging

- On `OnDragStart` of a handle whose node is *not* selected: select only that node (osu
  behaviour), then drag as usual.
- Drag applies the grabbed node's snapped delta (`ΔTimeOffset`, `ΔRotationOffset`) to **all**
  selected nodes:
  - rotation delta: applied unconditionally when non-zero (offsets are free integers, no ordering
    constraint);
  - time delta: applied only if every selected node's resulting `TimeOffset` stays `> 0` and the
    full control-point list remains strictly time-ordered; otherwise the time component is skipped
    for this event (no partial application).
- The existing guards stay: one `BeginChange`/`EndChange` per drag gesture, and
  `EditorChart.Update(HitObject)` fires only when something actually changed.

### Deletion

- A component inside the blueprint (the blueprint itself) implements
  `IKeyBindingHandler<PlatformAction>`: on `PlatformAction.Delete` with a non-empty node
  selection, remove those control points and return true — intercepting before
  `SelectionHandler.DeleteSelected` would delete the whole slider. (Blueprint children sit deeper
  in the input tree than `SelectionHandler`, so they see the action first — same mechanism osu
  relies on.)
- `HandleQuickDeletion()` override (Shift+RightClick): if a node handle is hovered, delete that
  single node and return true; otherwise return false (whole-slider delete, as today). Note: on an
  unselected slider handles are not hoverable, so quick-delete there removes the whole slider —
  matches osu.
- **Empty-path rule**: if a deletion would leave `Path.ControlPoints` empty (duration 0), delete
  the whole slider from the chart instead — matches osu's "fewer than 2 points → remove slider".
- All deletions wrapped in `BeginChange`/`EndChange` + `EditorChart.Update(HitObject)` (or
  `EditorChart.Remove` for the empty-path case) so undo/redo works via the existing snapshot diff.
- The context-menu "Delete" item and Delete with no node selected keep today's whole-slider
  semantics.

### Out of scope (YAGNI)

- Mixing node selections across two sliders (osu doesn't support this either).
- Node-specific context menu entries.
- Drag-box selection of nodes.
- Making `SliderChild`/control points first-class members of the global selection (rejected as
  Option B — breaks the JSON-identity undo diff, clipboard, and nested-object regeneration
  assumptions).

## Files touched

- `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs` — selection state, click/clear
  routing, multi-drag, Delete/quick-delete handling.
- `Garbus.Game/Edit/Blueprints/Components/NodeDragPiece.cs` — mouse-down selection callbacks,
  selected visual state.
- `Garbus.Game.Tests/Editor/` — new/extended headless coverage (below).

No chart-format, serialization, or gameplay changes.

## Test plan (headless)

1. Click a node dot of an unselected slider → whole slider selected, zero nodes selected.
2. Click a handle of a selected slider → that node selected, slider stays selected.
3. Ctrl+click a second handle → both selected; Ctrl+click one again → toggled off.
4. Click the outline of a selected slider → node selection cleared, slider still selected.
5. Delete with nodes selected → those control points removed, slider survives; undo restores.
6. Delete the last remaining node → whole slider removed from the chart.
7. With two nodes selected, drag one → both move by the same time/angle delta; ordering
   constraint blocks invalid time moves.
8. Drag an unselected handle → it becomes the sole selected node and moves alone.
9. Shift+RightClick a hovered handle → that node deleted; on the outline → whole slider deleted.
10. Deselecting the slider clears node selection.
