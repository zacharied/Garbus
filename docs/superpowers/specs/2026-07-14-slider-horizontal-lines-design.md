# Slider horizontal lines (zero-time arcs) — design

## Goal

Let a slider path contain **horizontal lines**: two consecutive nodes sharing the same
`TimeOffset` (a "zero-length link"), including the special case of a child node at
`TimeOffset = 0` directly after the head. In gameplay these render as **perfect arcs** — a
segment swept at a constant radius (constant time) around the playfield.

Input/judgement handling for arcs is explicitly **out of scope**; the future grace-period
mechanism will cover catching them. This work is about *authoring* and *rendering* the shape.

## Background: what already works

- **Gameplay rendering** (`Objects/Drawables/DrawableSliderBody`) interpolates each link in polar
  space and clips it to the visible radial band. A link whose two nodes share a time shares a
  radius, so it already renders as a constant-radius arc — the code comments call this out
  ("constant-radius links render as arcs"). `clipToBand`, `AngleDegAt`, and
  `Objects/Path/SliderSweep` all guard against a zero time/radius delta (no divide-by-zero).
- **Editor polyline** (`Edit/Drawables/EditorSliderPolyline` + `SliderSweep`) maps `time → y`
  linearly, so a zero-time link is drawn as a horizontal segment. `ComputeSlopes` and `ValueAt`
  guard the zero-duration case.
- **Format / serialization** (`Charts/Format/`) stores each control point's `TimeOffset` verbatim
  in list order; nothing sorts or validates monotonicity. Round-trip is unaffected.
- **Duration** (`SliderBody.Duration = Max(TimeOffset)`) is unaffected by equal times.
- **Verify checks** do not inspect node ordering.

So **no gameplay-rendering, format, or Verify changes are needed.** The only thing blocking
horizontal lines is that the three editor authoring paths enforce *strictly increasing* node
times.

## The invariant

A slider path is the implicit **head at time 0** followed by control points with `TimeOffset`
values. Node times (including the head's 0) must satisfy:

1. **Non-decreasing** — each `TimeOffset >= ` the previous node's time (was: strictly `>`).
2. **At most one zero-length link in a row** — if a node shares its predecessor's time, the
   *next* node must be strictly later. Equivalently: no three nodes at the same time. A
   horizontal arc is always a single collapsed segment.
3. **Total duration > 0** — the furthest node must lie after the head, so an all-zero
   (invisible) path is rejected.

Both target cases satisfy this:
- Two consecutive equal-time nodes (e.g. offsets `100, 100`) — a single mid/trailing zero link. **Allowed.**
- A child at offset `0` after the head — a single *leading* zero link, which rule 3 then forces
  to be followed by a later real node. **Allowed** (with a subsequent node > 0).

Rejected: three-plus nodes at one time (rule 2), a path where every node is at time 0 (rule 3),
any decreasing time (rule 1).

## Single source of truth

Add a small static helper next to `GarbusPathControlPoint` (namespace `Garbus.Game.Objects`,
file `Objects/Path/`), so all authoring paths share one implementation and it is unit-testable.
It operates on the sequence of control-point `TimeOffset`s (the head at time 0 is implied):

```csharp
public static class GarbusSliderPath
{
    // Rules 1 & 2 only: non-decreasing, and no two consecutive zero-length links.
    // Used mid-placement, where the path's duration is still building up toward > 0.
    public static bool AreTimesOrdered(IReadOnlyList<double> offsets);

    // Full invariant: AreTimesOrdered AND total duration > 0 (rule 3).
    // Used whenever the path is complete (node drag, T-insert, placement commit).
    public static bool AreTimesValid(IReadOnlyList<double> offsets);
}
```

Reference logic for `AreTimesOrdered`:

```
double previous = 0;              // implicit head at time 0
bool previousLinkZero = false;    // there is no link into the head
foreach (double offset in offsets)
{
    if (offset < previous) return false;             // rule 1
    bool linkZero = offset == previous;
    if (linkZero && previousLinkZero) return false;  // rule 2
    previousLinkZero = linkZero;
    previous = offset;
}
return true;
```

`AreTimesValid(offsets) = AreTimesOrdered(offsets) && offsets.Count > 0 && offsets[^1] > 0`
(the last offset is the max because the list is non-decreasing, i.e. the duration).

## Authoring-path changes

All three build a *prospective* list of offsets and validate it, rather than checking a single
neighbour. Prospective-list construction is cheap (paths are short).

### 1. `Edit/Blueprints/SliderPlacementBlueprint.tryAddNode`
- Replace the `if (timeOffset <= previousOffset) return;` guard with: build the prospective
  offsets (`existing offsets` + `timeOffset`) and reject unless `AreTimesOrdered`. This uses the
  *ordering-only* variant so a leading zero-arc can be built up (the first click at time 0 leaves
  duration 0 momentarily).
- Tighten `IsValidForPlacement` to also require `HitObject.Duration > 0` (currently only
  `ControlPoints.Count > 0`), so an all-zero path cannot be committed.
- The rubber-band preview gate (`cursorTime - StartTime > lastOffset`) is loosened to `>=` so the
  preview still shows when the next node would land at the same time (a horizontal segment).

### 2. `Edit/Blueprints/SliderSelectionBlueprint.timeShiftValid` (node drag)
- Rewrite to build the post-shift offsets (each moved node's `TimeOffset + deltaTime`, others
  unchanged, in list order) and return `AreTimesValid`. This keeps the full invariant, so a drag
  can never collapse the path to duration 0 or produce a triple.

### 3. `Edit/Blueprints/SliderSelectionBlueprint.insertNodeAtCursor` (T key)
- Drop the `if (timeOffset <= 0) return;` and the exact-time-stack rejection
  (`controlPoints[insertIndex].TimeOffset == timeOffset`).
- Keep the existing sorted-position scan (insert before the first node with a strictly greater
  time). Build the prospective offsets with the new node at that index and accept only if
  `AreTimesValid`. Inserting at an existing node's time yields a single zero link; a second
  insert at the same time is rejected by rule 2.

Rotation-offset assignment for placed/inserted nodes is unchanged (`MinimalDiff` from the
previous node).

## Out of scope / unchanged

- Gameplay input & judgement of arcs (deferred to the grace-period work).
- `DrawableSliderBody`, `EditorSliderPolyline`, `SliderSweep`, the chart format/serializer,
  `SliderBody.Duration`, and Verify checks — all already tolerate zero-time links.

## Testing

**Unit (`GarbusSliderPath`):** single zero link ok; double zero link (triple node) rejected;
all-zero rejected; leading zero then real node ok; trailing zero (`100,100`) ok; decreasing
rejected. Cover both `AreTimesOrdered` and `AreTimesValid`.

**Placement (`TestSceneComposePlacement`):** a leading zero-arc (click at head time, then a later
click) commits with the first control point at offset 0; a third click at the same time is
rejected; a single node at time 0 will not commit (duration 0).

**Selection / node drag (`TestSceneComposeSelection`):** dragging a node onto a neighbour's time
creates equal offsets (arc); a drag that would make duration 0 or a triple is rejected (no
change applied).

**T-insert (`TestSceneComposeSelection`):** inserting at an existing node's time creates an arc;
a duplicate that would form a triple is rejected.

**Render:** one `EditorSliderPolylineTest` case asserting an equal-time link yields a horizontal
segment (its two node points share `y`).
