# Shape-only slider control points — design

## Problem

Every slider path control point spawns a judged `SliderChild`, so shaping a slider's sweep requires
adding judged nodes. Complicated custom shapes need many control points, which inflates the slider's
share of a chart's judgement count (and therefore max score) purely for cosmetic/kinesthetic shaping.

## Concept

A control point may be marked **shape-only**: it contributes geometry to the body's sweep but is not
a node — it spawns no `SliderChild`, yields no judgement, plays no hitsound, and renders no gameplay
nub. The body segment it sits inside merges into the segment ending at the next judged node, so the
player must still trace the shaped sweep to earn that node's duration judgement.

The **last control point of a slider is never shape-only** — otherwise the tail of the body would be
ungraded dead weight. The implicit head is always judged (it is not a control point).

Terminology update for `docs/rules-specs/Judgement.md`: a **node** is the head or a non-shape-only
control point. "Every node yields exactly one Judgement" continues to hold; shape-only control
points are not nodes.

## Model

- `GarbusPathControlPoint` gains `public bool ShapeOnly` (default `false`).
- `SliderBody.CreateNestedHitObjects` skips shape-only points when creating `SliderChild`ren.
  `previousNode` advances only on spawned children, so each child's `HeadReference` (the
  pseudo-judgement chain) skips shape-only points with no additional logic.
- `SliderBody.GetSegmentStartTime(child)` walks backward past shape-only control points to the
  previous judged node, or the head (`StartTime`) when none precedes.

That is the entire judgement-side change. `DrawableSliderChild` already samples activation
continuously against `SliderBody.AngleDegAt` (which keeps using **all** control points, shape-only
included), so a merged segment demands catching through the shaped sweep; the opening/ending grace
windows apply at the merged segment's real endpoints only. `Duration`, body geometry, zero-length
segment rules, and the slam-coincidence rule (which references nodes) are unchanged.

## Gameplay presentation

Seamless: no nub, hitsound, or judgement feedback at shape-only points — all a direct consequence of
no `SliderChild` being spawned. The body renders identically to today.

## Serialization & validation

- The control-point DTO in `ChartFileDto` gains `ShapeOnly`, mapped both directions in
  `GarbusChartSerializer` (the song serializer bridges hit objects through it, so the mapping lives
  in one place). No version bump.
- **Decode-side validation:** decoding rejects any slider whose last control point is shape-only.
  This covers chart files, song files, and clipboard paste. The editor prevents authoring such a
  slider (below), so encode does not validate — a decode rejection means a hand-edited file.
- `GarbusTestChartGenerator` includes a shape-only point in a slider; the bundled chart is
  regenerated via the `[Explicit]` `RegenerateBundledTestChart` test.

## Editor

- **Inspector:** a "Shape only" `MultiValueCheckbox` joins the existing SweepEasing/Smoothing
  per-node group. It applies to every selected node except the slider's final control point, and its
  aggregate display state is computed over eligible nodes only — select-all-then-toggle works.
- **Compose visuals:** `SliderPolylineVisual` node dots and `SliderSelectionBlueprint` handles are
  driven by `Path.ControlPoints`, so shape-only points stay visible, selectable, and draggable.
  Shape-only dots render visually distinct from judged dots (e.g. hollow), with the styling exposed
  in a Tuning scene.
- **Delete guard (auto-promote):** deleting the final judged node is allowed; the new final control
  point is automatically promoted to judged (`ShapeOnly = false`) so the invariant holds through any
  edit. The Inspector rule above prevents toggling the final point directly.
- **Join sliders** copies `ShapeOnly` when reparenting control points (alongside `Smooth` /
  `SweepEasing`).
- Undo/redo identity strings and clipboard flow through the serializer and pick the field up for
  free. Placement is unchanged — place nodes, multi-select, toggle; no placement-time modifier.
- Test mode and the mini preview's auto-hit run on nested hit objects, so they respect shape-only
  points automatically.

## Testing

Per `docs/agents/testing.md` — hand-derived, spec-anchored expectations:

- **Judgement:** a slider with shape-only mid-points yields exactly one judgement per judged node;
  the head-reference chain skips shape-only points. Merged-segment grading through the bend is
  covered at the component level rather than by an end-to-end input simulation: the segment-timing
  pin (`GetSegmentStartTime`) establishes which judged node a shape-only point's segment grades
  against, and the sweep-geometry pin (`ShapeOnlyPointsStillShapeTheSweep`) establishes that
  `AngleDegAt` still bends through the shape-only point. `DrawableSliderChild` itself is unchanged
  and samples that same swept angle continuously, so no separate input-simulation test exists for it.
- **Model:** `GetSegmentStartTime` pins for children preceded by one and by several shape-only
  points, and for a first-segment child.
- **Serialization:** `ShapeOnly` roundtrip; decode rejection of a trailing shape-only point;
  bundled-file-vs-generator agreement via the existing `TestChartFormat` pins.
- **Editor:** the Inspector toggle skips the final node; delete auto-promotes the new final point;
  join-sliders preserves the flag. Compose dot distinction is asserted as a relation (shape-only
  marker differs from judged marker), never as bare styling values; the look itself is tuned by eye
  in the Tuning scene.
