# Head-only (zero-child) sliders — design

## Goal

Let the editor author and edit a slider whose path has **zero control points** — a slider that is
just its head. This is the deferred *Forward-compatibility: 0 control points* case from the
[zero-duration slider design](2026-07-16-zero-duration-sliders-design.md), which pinned the
`≥1 control point` floor to exactly two clauses precisely so this follow-up would be a two-line
relaxation.

A head-only slider is a single hit at an arbitrary angle on one side (`SliderBody.AngleDeg` /
`Side`), with `Duration == 0` and `EndTime == StartTime`. In gameplay it must **display** — a circle
at the head's position — but its judgement is unchanged from today (the head auto-passes via
`DrawableSliderHead`'s stub). Genuine head judgement / making it a truly playable single-hit object
is explicitly **out of scope**.

## Scope decisions (locked)

- **Editor authoring is fully in scope**: place, select, edit (rotate, delete, T-insert to promote),
  and save/load a head-only slider.
- **Gameplay is display-only.** The head-only slider renders a circle so it is visible during play,
  but judgement stays exactly as it is today (`DrawableSliderHead.CheckForResult` auto-`ApplyMaxResult`;
  `DrawableSliderBody` resolves `IgnoreHit` once the head has judged). No new head hit-testing, no
  head hit window, no head press requirement.
- **The commit gesture is plain left+right.** Left-click sets the head (body start + angle), a
  right-click with no nodes added commits it as a head-only slider. (Chosen over adding a guard
  against the "left-click then right-click to cancel" case — there is no cancel gesture today and
  this matches the format contract.)
- **The ordering invariant is unaffected.** Dropping the floor removes only the `Count > 0`
  requirement; times must still be non-decreasing with at most one zero-length link in a row. Those
  rules never apply to an empty offset list, so nothing about ordering changes.
- **Normal sliders are untouched.** The gameplay head circle renders only in the single-node
  (head-only) case; multi-node sliders keep their existing body-line visual with no added head cap.

## What already degrades gracefully (no change needed)

The model and several edit paths already tolerate zero control points:

- `SliderBody.Duration` returns `0` when `ControlPoints.Count == 0` (no divide-by-zero); `EndTime`
  collapses to `StartTime`.
- `SliderBody.CreateNestedHitObjects` already yields a lone `SliderHead` and no `SliderChild`ren.
- `EditorSliderPolyline.Build` emits exactly one head vertex + one head node for a zero-CP path
  (a `SmoothPath` with a single vertex simply draws nothing; the compose view still draws the head dot).
- `SliderSelectionBlueprint.removeNodes` removes the whole slider when the path is emptied, and
  `FinalNodeScreenPosition` already returns the head centre for `Count == 0`.
- `Inspector` shows `ControlPoints.Count` as `0` and hides the node-easing dropdown when no node is
  selected (a head-only slider has no selectable node) — no crash.
- `GarbusChartSerializer` round-trips an empty control-point list as `[]` in both directions.
- Timeline strip already renders a `Duration == 0` object as a dot.

## The three floors + one uncertain consequence

Authoring is blocked by exactly the two clauses the prior work isolated (three call sites):

1. `GarbusSliderPath.AreTimesValid` — `AreTimesOrdered(offsets) && offsets.Count > 0`. The
   `Count > 0` clause gates **node-drag** (`timeShiftValid`) and **T-insert** (`insertNodeAtCursor`),
   both of which route through it.
2. `SliderPlacementBlueprint.IsValidForPlacement` — `… && HitObject.Path.ControlPoints.Count > 0`.
3. `SliderPlacementBlueprint` right-click — `EndPlacement(HitObject.Path.ControlPoints.Count > 0)`.

The one **uncertain consequence** once a head-only slider exists: selection reduces to the `head`
`EditSquarePiece` (no outline path — needs ≥2 vertices — and no per-node handles). The head piece is
already in `ReceivePositionalInputAt`, but the blueprint's own box height is
`LengthAtTime(StartTime, EndTime) == 0`, so we must confirm the fixed-height head child still
receives positional input (click + drag) inside a zero-height, unmasked parent. A TDD test pins this;
if it fails, the fix is to give the head piece a hit region independent of the parent's `DrawHeight`.

## Changes

### 1. Relax `AreTimesValid` (covers node-drag + T-insert)

`Garbus.Game/Objects/Path/GarbusSliderPath.cs` — drop the `&& offsets.Count > 0` clause so
`AreTimesValid(offsets) => AreTimesOrdered(offsets)`. An empty offset list (a head-only path) is now
valid. Keep the method as a named wrapper (call-site clarity) rather than deleting it. Update the
file-header / method doc that currently states "at least one control point (the head alone is not a
path)".

### 2. Relax placement (commit + validity)

`Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs`:
- `IsValidForPlacement` → drop the `&& HitObject.Path.ControlPoints.Count > 0` clause.
- Right-click commit → `EndPlacement(true)` (a right-click while `PlacementActive == Active` always
  commits; the head is already placed).
- Update the class-doc contract line ("requiring at least one node") to note a head-only slider is
  now permitted.

No format change. `tryAddNode`/preview already use `AreTimesOrdered` and are unaffected.

### 3. Gameplay display circle

`Garbus.Game/Objects/Drawables/DrawableSliderBody.cs` — the `updatePath` path currently draws
nothing when `nodeTimes.Length < 2`. In that single-node case, render the head node as a circle:

- **Radius = `Thickness / 2`** (7.5px by default — "the same radius as the slider body line").
- Positioned at the head node's polar coordinate (`polarToCartesian(nodeRadians[0], nodeRadii[0])`),
  which travels centre→ring exactly as the body's head would, and is only shown while its radius is
  within the visible band `[0, ringRadius]` (mirroring the body's clip; hidden before it emerges).
- Tinted the side colour and hosted so it fades on the auto-`Hit` / reddens on `Miss` alongside the
  existing containers (reuse `pathContainer`'s fade path or a sibling driven by the same
  `UpdateHitStateTransforms`).
- Rendered **only** in the head-only case; a multi-node slider's `nodeTimes.Length >= 2` branch is
  unchanged.

Judgement is untouched: `DrawableSliderHead` still auto-passes, and the body still resolves
`IgnoreHit` after the head judges.

### 4. Editor selection/editing (verify, fix only if needed)

No planned code change beyond confirming behaviour via tests. Once placement commits a head-only
slider:
- Clicking the compose head dot selects the slider (via the `head` piece in
  `ReceivePositionalInputAt`).
- Dragging it rotates `AngleDeg` (`GarbusSelectionHandler.HandleMovement`, which is node-count
  agnostic).
- Delete removes the whole slider (`SelectionHandler`; `HandleQuickDeletion` falls through on the
  head).
- Pressing `T` inserts the first node, promoting it to a one-child slider.

If the zero-height-parent selection test fails, add the minimal hit-region fix described above.

## Tests

- `GarbusSliderPathTest` — `AreTimesValid(new double[] { })` flips from `False` to `True`; the
  ordering-rejection cases (`{0,0}`, out-of-order) stay `False`. Rename/replace the `EmptyIsNotValid`
  case accordingly.
- `TestSceneComposePlacement` — new: slider tool, one left-click on the body, then right-click →
  a slider commits with `Path.ControlPoints.Count == 0` and `Duration == 0`.
- `TestSceneComposeSelection` — new: place a head-only slider (helper), click the head dot →
  it becomes the sole selected object; then Delete removes it; and (separately) `T` at the cursor
  promotes it to a one-node slider. This is the test that pins the zero-height-parent selection risk.
- `TestSceneGameplay` — new: a chart with a head-only slider renders a visible circle (a drawable
  present with non-zero size while in the visible band) and the object still auto-hits (max result).

## Out of scope

- **Genuine head judgement / a playable single-hit slider.** The head keeps its auto-pass stub; no
  head press, hit window, or scoring change. (This is the larger deferred piece; the display circle
  here does not commit us to any particular judgement model.)
- **A head cap on normal (multi-node) sliders.** The circle is strictly the head-only render path.
- **Any minimum-duration/tick floor**, format change, or slam-drawable change.
- **A cancel gesture for placement.** Right-click commits; there is no separate cancel today.
