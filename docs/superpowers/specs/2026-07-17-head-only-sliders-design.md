# Head-only (zero-child) sliders — design

## Goal

Let the editor author and edit a slider whose path has **zero control points** — a slider that is
just its head. This is the deferred *Forward-compatibility: 0 control points* case from the
[zero-duration slider design](2026-07-16-zero-duration-sliders-design.md). Because that work already
made the model tolerate an empty path, the remaining change is small: a dedicated placement gesture
(`Ctrl`+left-click) plus a gameplay display circle.

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
- **The placement gesture is `Ctrl` + left-click.** With the slider tool active and no placement in
  progress, `Ctrl`+left-click drops a head-only slider directly (head angle/time snapped to the
  cursor, committed in one click). A plain left-click still begins a normal multi-click slider, and a
  **right-click with no nodes added still cancels** (does *not* commit a head-only slider). This keeps
  the "start a slider, right-click to back out" gesture intact and makes head-only creation explicit,
  so it can never happen by accident (including the tool-switch auto-commit path, see below).
- **The ordering invariant is unaffected**, and `GarbusSliderPath.AreTimesValid` needs **no change**.
  Its `Count > 0` clause only ever rejects an *empty* offset list, and no head-only path is validated
  against an empty list: node-drag has no handle to grab, T-insert always adds a node (making the
  prospective count ≥ 1), and placement commit does not route through `AreTimesValid`. Times must
  still be non-decreasing with at most one zero-length link in a row wherever they do apply.
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

## The placement floor + one uncertain consequence

Only **placement** blocks a head-only slider, at one clause:

- `SliderPlacementBlueprint.IsValidForPlacement` — `… && HitObject.Path.ControlPoints.Count > 0`.

This clause must *not* simply be dropped, because a placement is also auto-committed on tool-switch
(`ComposeBlueprintContainer` calls `EndPlacement(PlacementActive == Active)` when the tool changes,
and a right-click with no nodes calls `EndPlacement(false)`). Dropping the clause globally would let
"start a slider, switch tools" silently create a head-only slider. Instead the clause is relaxed
**only** for the explicit `Ctrl`+left-click gesture, via a one-shot flag (below). The right-click
path keeps `EndPlacement(HitObject.Path.ControlPoints.Count > 0)` unchanged, so a node-less
right-click still cancels.

The one **uncertain consequence** once a head-only slider exists: selection reduces to the `head`
`EditSquarePiece` (no outline path — needs ≥2 vertices — and no per-node handles). The head piece is
already in `ReceivePositionalInputAt`, but the blueprint's own box height is
`LengthAtTime(StartTime, EndTime) == 0`, so we must confirm the fixed-height head child still
receives positional input (click + drag) inside a zero-height, unmasked parent. A TDD test pins this;
if it fails, the fix is to give the head piece a hit region independent of the parent's `DrawHeight`.

## Changes

### 1. `Ctrl`+left-click placement of a head-only slider

`Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs`:
- Add a one-shot `bool committingHeadOnly` field and relax the gate to
  `IsValidForPlacement => base.IsValidForPlacement && (HitObject.Path.ControlPoints.Count > 0 || committingHeadOnly)`.
- In `OnMouseDown`, on a **left** button in the `Waiting` state: `BeginPlacement(true)` as today, but
  if `e.ControlPressed`, also set `committingHeadOnly = true` and `EndPlacement(true)` in the same
  event — committing the head-only slider in one click (its `AngleDeg`/`StartTime` were already
  snapped to the cursor by the base `UpdateTimeAndPosition` while `Waiting`).
- Leave the **right**-click branch as-is: `EndPlacement(HitObject.Path.ControlPoints.Count > 0)` — a
  node-less right-click still cancels.
- Update the class-doc contract line ("requiring at least one node") to describe the `Ctrl`+click
  head-only path.

`GarbusSliderPath.AreTimesValid` is **not touched** (see the floor section above). No format change;
`tryAddNode`/preview already use `AreTimesOrdered` and are unaffected. The preview's red "invalid"
tint for a normal 0-node in-progress slider is preserved (the flag is false until the explicit
commit).

### 2. Gameplay display circle

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

### 3. Editor selection/editing (verify, fix only if needed)

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

- `TestSceneComposePlacement` — new: slider tool, a single `Ctrl`+left-click on the body → a slider
  commits with `Path.ControlPoints.Count == 0` and `Duration == 0`. Plus a guard test: a plain
  left-click then a **right**-click (no nodes) commits **nothing** (still cancels), and starting a
  0-node slider then switching tools commits nothing.
- `TestSceneComposeSelection` — new: place a head-only slider (helper, via `Ctrl`+left-click), click
  the head dot → it becomes the sole selected object; then Delete removes it; and (separately) `T` at
  the cursor promotes it to a one-node slider. This is the test that pins the zero-height-parent
  selection risk.
- `TestSceneGameplay` — new: a chart with a head-only slider renders a visible circle (a drawable
  present with non-zero size while in the visible band) and the object still auto-hits (max result).
- `GarbusSliderPathTest` is **not changed** — `AreTimesValid` is untouched (`EmptyIsNotValid` stays).

## Out of scope

- **Genuine head judgement / a playable single-hit slider.** The head keeps its auto-pass stub; no
  head press, hit window, or scoring change. (This is the larger deferred piece; the display circle
  here does not commit us to any particular judgement model.)
- **A head cap on normal (multi-node) sliders.** The circle is strictly the head-only render path.
- **Any minimum-duration/tick floor**, format change, or slam-drawable change.
- **Changing the right-click behaviour.** Right-click still commits a slider that has nodes and
  cancels one that does not; head-only creation is the separate `Ctrl`+left-click gesture.
