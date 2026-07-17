# Zero-duration sliders in the editor — design

## Goal

Let the editor author and edit slider objects whose total duration is `0` — a constant-radius
arc existing at a single instant (head + one control point sharing `TimeOffset 0`). Today a
`Duration > 0` gate blocks this in the editor, and the editor's slider render/interaction paths
divide a node's `TimeOffset` by the slider `Duration` (`0/0`) so they explicitly bail on
`duration <= 0`, leaving such an object invisible and unselectable.

The gameplay half is already done: `DrawableSliderChild` grants the catch when its tracker ends
with zero records (an unsampled sub-frame/zero-width catch window), so a zero-duration slider is
no longer an unavoidable miss. The file format needs no change — `GarbusChartSerializer`
round-trips control points faithfully with no duration validation.

## Scope decisions (locked)

- **Gate relaxation is uniform** across all three editor authoring paths (placement commit,
  node-drag, T-insert). No asymmetry where one path allows the shape and another forbids it.
- **The "at least one control point" requirement stays for now** — a slider still needs a head +
  ≥1 node. This is deliberately retained but is expected to be relaxed later (see
  *Forward-compatibility* below): a head-only slider would be a **single-hit** object (the head is
  the one hit), whereas requiring a child forces every slider to be hit twice (head press + child
  catch). Keeping the floor now avoids the head-only degenerate (invisible, unselectable, auto-hit)
  until the gameplay/selection support for it exists.
- **The ordering invariant stays** — times non-decreasing, at most one zero-length link in a row.
  The only newly-permitted shape is head + a single node at `TimeOffset 0`.
- **Editor representation is a true zero-height horizontal line** pinned at the object's
  `StartTime` (the judgement line at the bottom of the compose grid), spanning head-angle →
  node-angle. No synthetic/minimum duration or fake vertical extent. The line is grabbable via
  its own drawable thickness (the `SmoothPath` outline radius) and the fixed-size node handles,
  neither of which depends on the container's `DrawHeight`.

## The single root cause

Every editor breakage traces to the vertical fraction `timeOffset / duration`, which is `0/0`
when the whole path collapses to `StartTime`. The three render paths avoid the NaN by
early-returning on `duration <= 0`, which is what makes the object invisible. Fixing the fraction
at its source (treat it as `0` when `duration == 0`, pinning every node to the bottom line) lets
all three paths render the degenerate line with no early-return.

## Changes

### 1. Relax the gate (2 edits cover all 3 paths)

- `GarbusSliderPath.AreTimesValid` — drop the `&& offsets[^1] > 0` clause:
  `AreTimesOrdered(offsets) && offsets.Count > 0`. This automatically relaxes **node-drag**
  (`SliderSelectionBlueprint.timeShiftValid`) and **T-insert**
  (`SliderSelectionBlueprint.insertNodeAtCursor`), which both route through it. Update the doc
  comment (it currently states "total duration > 0").
- `SliderPlacementBlueprint.IsValidForPlacement` — drop `&& HitObject.Duration > 0`:
  `base.IsValidForPlacement && HitObject.Path.ControlPoints.Count > 0`.

No format change, no gameplay change.

### 2. Render the degenerate line instead of bailing

- `EditorSliderPolyline.Build` — compute `yFrac = duration > 0 ? timeOffset / duration : 0` and
  use it in `toPoint` so every node pins to the bottom line (`y = drawHeight`). Update the
  "guarantees `duration > 0`" doc note.
- `SliderPolylineVisual.buildGeometry` — remove `if (duration <= 0) return;`; call `Build`
  unconditionally.
- `SliderSelectionBlueprint.Update` — remove the `duration <= 0` bail (which clears handles and
  outline), and guard the node-handle `y` computation the same way
  (`duration > 0 ? DrawHeight * (1 - cp.TimeOffset / duration) : 0`).

`EditorDrawableSliderBody`'s height stays `LengthAtTime(start, start) = 0`; no change needed — the
polyline/outline anchor at the `StartTime` line and carry their own thickness. The timeline strip
already renders a duration-0 object as a dot (`TimelineObjectMarkers` only draws a bar when
`Duration > 0`). Placement preview already uses `ScreenSpacePositionAtTime` directly (no duration
division), so the rubber-band already previews the horizontal line — only the commit gate blocked.

### 3. Tests

- `GarbusSliderPathTest` — flip `AreTimesValid({0})` to `True`; keep `AreTimesValid({})` `False`;
  add `AreTimesValid({0, 0})` → `False` (ordering still rejects a double zero-link).
- `TestSceneComposePlacement` — the case asserting a single node at head-time is *rejected* flips
  to *committed*; assert the added slider has `Duration == 0`.
- `TestSceneComposeSelection` — new test: a zero-duration slider is selectable by clicking its
  horizontal line, and its outline/handle render (no NaN).
- `EditorSliderPolylineTest` — a `Build` case at `duration == 0` asserting all node y's sit at the
  bottom line and no `NaN` leaks.

## Forward-compatibility: 0 control points (future single-hit sliders)

A head-only slider (zero control points) is a valid future goal: a single-hit slider caught once,
instead of head + child. It is **out of scope now** but the current work must not design against it.

What relaxing to 0 nodes will take later — kept small and symmetric with this change:

- **The floor is a single clause in exactly two spots**, mirroring the duration relaxation, so
  dropping it is a two-line change: `AreTimesValid`'s `offsets.Count > 0`, and placement's
  `HitObject.Path.ControlPoints.Count > 0` (both `IsValidForPlacement` and the `EndPlacement`
  commit argument). Keep these as the *only* enforcement of the floor — do not scatter new
  `Count > 0` assumptions through the render or edit paths.
- **The zero-duration render path must degrade without crashing at a single node.**
  `EditorSliderPolyline.Build` with 0 control points already emits a single vertex (no link, no
  line) rather than throwing; the `SmoothPath` simply draws nothing. Preserve that — the new
  duration-`0` branch must not introduce a `≥1 link` assumption.

What 0-node support will *additionally* require (the reason it is deferred, not the reason to
design around it):

- **Head-based hit-testing/selection.** With 0 nodes the outline and node handles are both empty,
  and selection is path-precise (`ReceivePositionalInputAt` reports only outline paths + node
  handles, per `SliderSelectionBlueprint`'s class doc), so a head-only slider would be
  unselectable. It will need the head marker itself to be a hit-test/selection target.
- **A head visual + real head judgement in gameplay.** `DrawableSliderBody` draws no path for a
  single node, and `DrawableSliderHead` is a judgement stub (auto-`ApplyMaxResult`, no visual). A
  single-hit slider needs the head to render and to be genuinely judged.

## Out of scope

- Gameplay catch model (already landed: zero-record tracker → hit).
- Any minimum-duration or snap-tick floor — duration exactly `0` is permitted.
- Slam gameplay drawables (still editor-only concepts; unaffected).
