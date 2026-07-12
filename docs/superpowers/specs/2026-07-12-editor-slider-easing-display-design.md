# Editor slider easing display — design

## Problem

Slider path nodes carry two geometry-affecting properties — `SweepEasing` (an easing
function) and `Smooth` (a Catmull-Rom spline flag) — that reshape the angle sweep during
gameplay. Both are currently **invisible in the editor's compose view**: an eased or smoothed
link renders as a straight chord, so authors can't see what they're editing.

- **Gameplay** (`DrawableSliderBody.thetaAt`) subdivides each link into `segments_per_link`
  sub-segments and applies both `SweepEasing` and `Smooth` to the *angle*, keeping *time*
  linear. That produces the curved swept geometry seen during play.
- **Editor** (`SliderPolylineVisual.computeVertices`) emits exactly one straight vertex per
  node — no subdivision, no easing, no smoothing.

The editor's coordinate space is a clean analogue of gameplay's: editor **x = angle** ↔
gameplay **θ (eased)**, editor **y = time** ↔ gameplay **radius (linear in time)**. So the
same per-link angle evaluation gameplay uses produces the correct curved editor polyline.

## Goal

The editor polyline exactly mirrors the played geometry — both `SweepEasing` and `Smooth`.
The two must not be able to drift, so the evaluation math is shared, not duplicated.

## Design

### 1. Extract the angle evaluation into a shared helper

Today `DrawableSliderBody.thetaAt` privately owns the "ease + optionally Catmull-Rom smooth
the angle across a link" math. Pull it into a pure static helper (`Objects/Path/SliderSweep.cs`):

- `ComputeSlopes(values, times)` → Catmull-Rom tangents (centred difference of value over
  time; one-sided at the ends), matching gameplay's current tangent computation.
- `ValueAt(values, slopes, times, linkEasing, linkSmooth, link, t)` → the value at parameter
  `t` (0..1) along `link`: apply `linkEasing` to `t`, then linear-interpolate (default) or
  cubic-Hermite (when `linkSmooth`) between the two node values using the precomputed slopes.
- A shared subdivision-count constant (currently gameplay's `segments_per_link = 12`).

Both functions are unit-agnostic: the "value" is radians for gameplay and degree-offsets
(`RotationOffset`) for the editor. Easing operates on `t`; Hermite tangents scale
consistently with link duration, so either unit is exact as long as the caller is consistent.

`DrawableSliderBody` keeps its per-node arrays (`nodeRadians`, `nodeTimes`,
`nodeThetaSlopes`, `linkSmooth`, `linkEasing`) but delegates the body of `thetaAt` to
`SliderSweep.ValueAt`, and uses `SliderSweep.ComputeSlopes` in `rebuildNodes`. Behavior is
unchanged — the pinned gameplay tests still pass.

### 2. Editor: subdivide links and split path vertices from node markers

`SliderPolylineVisual.computeVertices` changes from "one vertex per node" to walking each
**link** (head→cp0, cp0→cp1, …) and emitting subdivided sub-vertices:

- Build per-node arrays from `slider.Path.ControlPoints`: value = `RotationOffset` (head = 0),
  time = `TimeOffset` (head = 0), per-link `SweepEasing`/`Smooth` from the control point at
  each link's end node (a control point governs the segment leading *into* it). Compute slopes
  via `SliderSweep.ComputeSlopes`.
- For each link, for `k = 0..segments_per_link`, compute `t = k / segments_per_link`, then
  `x = centreX + ValueAt(...) * pxPerDeg` and `y = DrawHeight * (1 - lerp(time) / duration)`.
  Time stays linear (matching gameplay).
- Subdivide uniformly for all links, including plain linear ones, to match gameplay exactly.
  Sliders in the editor are few and vertices recompute only on actual change, so the extra
  points are negligible.

Because the polyline now has sub-vertices, the node **dots** must not sit on every vertex.
Split the geometry into two lists:

- the full subdivided **polyline** → fed to `SmoothPath`,
- the **node positions** (one per real node) → the dot markers.

`PathCopy.SetGeometry` takes both lists; the change-detection early-out compares the polyline
list as it does today (`vertexListEquals`). Wrap-copy offset logic is unchanged (a flat X
shift on the whole copy).

### Known limitation (out of scope)

Overshoot easings (`Back`/`Elastic`/`Bounce`) can push the curve past a node's angle. The
wrap-copy visibility range (`computeWrapCopies`) is derived from node `RotationOffset` extremes
only, so an overshoot poking past the seam could clip in its ghost twin. Tightening the editor
wrap range to account for eased extremes is a separate follow-up, not part of this change.

## Testing

`SliderSweep` is a pure function — the natural, headless test seam (no playfield/clock):

- **Endpoints preserved:** `ValueAt(t=0)` = start value, `ValueAt(t=1)` = end value, for both
  linear and smooth links.
- **Linear parity:** `Easing.None` + not-smooth reproduces plain `lerp` at sampled `t`.
- **Easing applied:** a non-identity easing (e.g. `InQuint`) yields a midpoint value that
  differs from the linear midpoint.
- **Gameplay parity:** feed the node arrays gameplay builds and assert `ValueAt` matches the
  pre-refactor `thetaAt` output at several `t` — pins "editor == gameplay."

Existing gameplay tests (`TestSceneComposerLifecycle`, slider node-drag tests, gameplay sweep
behavior) continue to guard `DrawableSliderBody` after it delegates. No rendering-level test is
added for `SliderPolylineVisual` (private, playfield-dependent); the shared-helper tests plus
the untouched wrap/rebuild logic cover the risk.

## Summary of changes

1. New `Objects/Path/SliderSweep.cs` — shared easing/smoothing angle math + subdivision count.
2. `DrawableSliderBody` delegates `thetaAt` / slope computation to `SliderSweep`.
3. `SliderPolylineVisual` subdivides each link through `SliderSweep`, splitting subdivided path
   vertices from node-marker positions; `PathCopy.SetGeometry` takes both lists.
4. Unit tests for `SliderSweep` (endpoints, linear parity, easing effect, gameplay parity).

Follow-up (not in scope): tighten editor wrap-copy range for overshoot easings.
