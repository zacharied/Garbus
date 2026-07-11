# Editor: flip hit objects — design

## Goal
Two right-click actions on a compose-editor selection that mirror the selected geometry:

1. **"Flip around angle…"** — interactive. Drops a vertical pivot bar on the cursor; on click the
   selection is reflected about the angle under the cursor.
2. **"Flip selection"** — one-shot. Reflects the selection about the centre of its own angular
   bounding box.

Both are **true mirrors** (a reflection reverses handedness) and both are **node-aware**: when a
slider has individually-selected control-point nodes, the flip acts on just those nodes.

## Background / constraints
A Garbus hit object has **no free 2D position**. It is a *direction* `θ` (a ray from the playfield
centre) plus a *time* `t`; radius is `f(t)` (constant scroll). So a "bounding box" is an **angular
range**, not a spatial box, and every flip is an **angular reflection** — never a Cartesian translate.
Time is never touched by these ops.

Angle convention (`EditorAngleMapping`, `UI/GarbusScrollingHitObjectContainer.cs`): standard math
orientation, CCW positive, screen-y flipped. θ=0 East, 90 North, 180 West, 270 South. Reflection about
pivot φ is `θ → 2φ − θ`. On the circle this is **purely modular**, so a reflection is fully described by
`sumDeg = 2φ (mod 360)` — no seam-unwrapping is needed to *apply* it (unwrapping only matters when
*computing* the bbox centre).

The editor unrolls the circle onto the timeline x-axis: grid left edge = 135° (`ANGLE_ORIGIN`), the wrap
seam falls on the 315° diagonal, ±`GHOST_DEGREES` ghost bands.

### Object model
| Type | Angle | Handedness / other |
|---|---|---|
| `CardinalNote` | `IHasMutableAngle` (`int AngleDeg`) | — |
| `HoldNote` | `IHasMutableAngle` + `IHasDuration` | — |
| `GarbusSlamCentered` | `IHasMutableAngle` | `Side` (non-geometric) |
| `GarbusSlamEdge` | `IHasMutableAngle` | `Direction` CW/CCW (mutable) — reverses under mirror |
| `SliderBody` | `IHasMutableAngle` (head) + `IHasDuration` | `Path.ControlPoints[].RotationOffset` (int, CCW, relative to head); `Side` (stick assignment, non-geometric) |
| `ShoulderNote` | **derived** `AngleDeg => Side.ToAngleDeg()` (Right=0/East, Left=180/West) | angle not mutable — only ever E or W |

**Node absolute angle** (from `SliderSelectionBlueprint.Update`): `NormalizeDeg(head.AngleDeg +
cp.RotationOffset)`. This is why the slider bbox and node-level flip are now tractable.

**Node selection** (from the slider-node-selection feature) is local to `SliderSelectionBlueprint`:
`internal IReadOnlyCollection<GarbusPathControlPoint> SelectedNodes`. It is *not* part of
`EditorChart.SelectedHitObjects`. A slider with selected nodes is itself in the global selection (node
picking requires the slider to be selected). The selection handler already reaches slider blueprints via
`SelectedBlueprints` (used today in its `Update`), and lives in the same assembly, so it can read
`SelectedNodes` directly — **no change to `SliderSelectionBlueprint` is required.**

## The unifying model: flip *handles*
Both ops reduce to "reflect a set of angular **handles** about a pivot". The global selection expands to
handles as follows (per selected blueprint):

- **Point object** (Cardinal / Hold / SlamCentered / SlamEdge / Shoulder) → one handle at its angle.
- **Slider with a non-empty `SelectedNodes`** → one handle per selected node (absolute angle
  `head + rot`). The head and unselected nodes are *not* handles.
- **Slider selected as a whole** (empty `SelectedNodes`) → the whole slider: handles are the head and
  every node's absolute angle (the full swept extent).

The handle set drives **both** the transform and the bbox pivot, so "the thing you flip" and "the box you
flip it about" always agree.

## Core transform — `Flip(int sumDeg)`
One transaction on `GarbusSelectionHandler` (`EditorChart.BeginChange()` → mutate + `EditorChart.Update(h)`
per changed object → `EditorChart.EndChange()` ⇒ **single undo step**). Iterating `SelectedBlueprints`
(not just `SelectedHitObjects`) so slider node selection is visible:

```
foreach blueprint b in SelectedBlueprints:
  switch b.Item h:

    ShoulderNote shoulder:                       // no mutable angle: reflect E/W, re-derive Side
      a = NormalizeDeg(sumDeg - shoulder.Side.ToAngleDeg())
      shoulder.Side = InEastHemisphere(a) ? Right : Left

    SliderBody slider:
      var nodes = (b as SliderSelectionBlueprint)?.SelectedNodes
      if nodes is non-empty:                      // node-subset: head fixed, reflect only those nodes
        foreach cp in nodes:
          cp.RotationOffset = sumDeg - 2*slider.AngleDeg - cp.RotationOffset
      else:                                        // rigid whole-slider mirror about the pivot
        slider.AngleDeg = NormalizeDeg(sumDeg - slider.AngleDeg)
        foreach cp in slider.Path.ControlPoints:
          cp.RotationOffset = -cp.RotationOffset

    IHasMutableAngle mutable:                      // Cardinal / Hold / SlamCentered / SlamEdge
      mutable.AngleDeg = NormalizeDeg(sumDeg - mutable.AngleDeg)
      if h is GarbusSlamEdge slam:                 // handedness reversal
        slam.Direction = flip(slam.Direction)      // Clockwise <-> Anticlockwise
```

Only call `EditorChart.Update(h)` for objects actually touched.

`InEastHemisphere(a)`: East when `cos a ≥ 0`, i.e. `a ∈ [0,90] ∪ [270,360)`; the N/S ties (90/270)
resolve to East.

### Why the slider maths is a true mirror
Reflecting a node's absolute angle about φ: `2φ − (head+rot)`.
- **Rigid case** (head also reflects): `newHead = 2φ − head`, and preserving each node's reflected
  absolute means `newRot = (2φ − head − rot) − newHead = −rot`. So *reflect head, negate every offset* —
  exactly the chirality-reversing mirror, and it preserves any intentional multi-turn winding.
- **Node-subset case** (head fixed): `newRot = (2φ − head − rot) − head = 2φ − 2·head − rot =
  sumDeg − 2·head − rot`. A pure reflection of the offset coordinate about `(φ − head)`; offsets are free
  ints (no normalisation forced), so a distant pivot legitimately winds the node further.

`Side` fields (slider/slam stick assignment) sit on the E/W mirror axis and are non-geometric —
**untouched**. No source-model change (no need to make `SlamCentered.Side` settable).

## "Flip selection" — pivot = handle-set centre (seam-robust)
`Flip(ComputeSelectionReflectionSum())`, where the sum is the reflection about the centre of the **handle
set's** tightest angular arc:

1. Collect handle angles (per the model above), normalised to `[0,360)`.
2. Sort ascending; find the largest circular gap (including the wrap gap `first + 360 − last`).
3. The covering arc starts just after that gap; unwrap the rest into a monotone run (add 360 on wrap).
   `min` = start, `max` = last.
4. `sumDeg = NormalizeDeg(min + max)`.

`2·centre = min + max` is always an integer even when the centre is a half-degree, so `sumDeg` is exact.

Properties:
- **134°/136°** pair: covering arc [134,136], centre ≈135°, they **swap** — not reflected across the strip.
- **Single object**: `sum = 2θ` → angle unchanged, handedness still reverses ⇒ mirror in place
  (meaningful for a lone slam-edge; for a lone whole slider the pivot is its *swept-extent centre*, so its
  head generally moves as the shape mirrors about that centre).
- **Slider with a node subset**: the pivot is the centre of just those nodes → they mirror about their own
  local centre; head and other nodes stay.

The largest-gap helper (`ReflectionSum(IEnumerable<int> angles)`) lives in `EditorAngleMapping` (its
remit), keeping the handler small and unit-testable.

## "Flip around angle…" — interactive pivot bar
A new lightweight `FlipPivotOverlay` drawable, hosted **topmost** in the composer content, inactive by
default:

- `ReceivePositionalInputAt(pos) => active` — only steals input while a flip is in progress.
- Activated by `composer.BeginFlipAroundAngle(Action<int> onCommit)` (stores the callback, shows overlay,
  `active = true`).
- **OnMouseMove**: snap the cursor via `EditorAngleMapping.SnapX(localX / playfield.DrawWidth,
  composer.AngleSnap.Value)` → draw a vertical bar at the returned x-fraction; remember the returned pivot
  angle. (Optional: also draw a ghost twin bar when the pivot is within a ghost band.)
- **OnClick (left)**: `onCommit(NormalizeDeg(2 * pivot))`, then deactivate.
- **OnClick (right) / OnKeyDown(Escape)**: cancel, deactivate (no mutation).

Bar snaps to the existing angle-snap grid (`GarbusHitObjectComposer.AngleSnap`). No live preview of the
flipped objects — bar only. The overlay is geometry-agnostic: it only reports "pivot φ → `onCommit(2φ)`";
the handler's `Flip` closure reads the *current* selection (and node selection) at commit time.

## Menu items (universal, before base Delete)
In `GarbusSelectionHandler.GetContextMenuItemsForSelection`, yielded for **every** selection (before the
`base` items, which end with Delete):

```csharp
yield return new GarbusMenuItem("Flip around angle...", MenuItemType.Standard,
    () => composer.BeginFlipAroundAngle(sum => Flip(sum)));
yield return new GarbusMenuItem("Flip selection", MenuItemType.Standard,
    () => Flip(ComputeSelectionReflectionSum()));
```

(The existing Anticlockwise / Right-side ternary toggles remain above these for their type-homogeneous
selections.)

## Components & boundaries
- **`GarbusSelectionHandler`** — owns the menu items, node-aware `Flip(int)`, and
  `ComputeSelectionReflectionSum()`. Reads `SelectedBlueprints` (for `SelectedNodes`) and mutates through
  `EditorChart`; starts the interactive mode via the resolved `composer`.
- **`GarbusHitObjectComposer`** — gains `BeginFlipAroundAngle(Action<int>)` and hosts `FlipPivotOverlay`.
  Reuses `AngleSnap` and `Playfield`.
- **`FlipPivotOverlay`** — pure input/visual: cursor → snapped pivot angle + drawn bar → callback. Knows
  nothing about hit objects or reflection maths.
- **`EditorAngleMapping`** — gains `ReflectionSum(angles)` (largest-gap); already owns `NormalizeDeg`,
  `SnapX`, `MinimalDiff`.
- **`SliderSelectionBlueprint`** — unchanged (`SelectedNodes` already exposed).

## Testing (`Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`)
`Flip(int)` and `ComputeSelectionReflectionSum()` unit-test directly; the overlay drives via
`ManualInputManager`. Node cases select nodes through the blueprint (place slider → select → click a node
handle, per the node-selection tests) before invoking a flip.

Cases:
- CardinalNote reflects N↔S at φ=0 (`sum=0`); E/W unchanged.
- Whole SliderBody: head `AngleDeg` reflects **and** every `RotationOffset` negates (rigid mirror).
- GarbusSlamEdge: `Direction` flips CW↔CCW and angle reflects.
- ShoulderNote: `Side` flips at a vertical-ish pivot (φ=90) and is unchanged at φ=0.
- "Flip selection" on two point objects: they swap about their midpoint.
- **Seam-straddling** pair (134° & 136°): swap about ~135° — not across the strip.
- **Slider bbox uses head+all nodes** when the slider is whole-selected (pivot = swept-extent centre, head
  moves).
- **Node-subset flip**: select 2 of a slider's nodes → "Flip selection" mirrors only those about their own
  centre; head and other nodes unchanged. Whole slider stays put otherwise.
- **Node-subset "Flip around angle…"**: selected nodes reflect about the picked pivot; head fixed.
- Mutation is a **single** undo step across a mixed selection; undo restores, redo re-applies.
- Both menu items present for every selection type (each object type + a mixed selection + a slider with
  nodes selected).
- Interactive: `BeginFlipAroundAngle` + move cursor to a known x + left-click applies the expected
  reflection; Escape / right-click cancels with no mutation.

## Out of scope
- **Time reversal** (`t → 2τ − t`; internally reversing holds/sliders) — a separate DOF, deferred.
- Rotation / translation (not flips; no free XY exists).
- Keyboard shortcuts (context-menu only for now).
- Mixing node selections across two sliders (the node feature doesn't support it either).
