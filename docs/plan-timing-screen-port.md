# Plan: port the rest of osu's Timing screen

Answering ISSUES.md: "can we make a plan to copy over the rest of osu's timing menu?"

Reference: `BAC\LocalDependencies\osu\osu.Game\Screens\Edit\Timing\` (21 files). Garbus already has
the load-bearing half — `TimingPointList` (≈ ControlPointList, rebuilt on Basic* widgets),
`TimingPointSettings` (≈ a trimmed TimingSection), `TapTimingControl` + `TapButton` +
`MetronomeDisplay` + `RepeatingButtonBehaviour`, and `WaveformComparisonDisplay`. What follows is
what osu has on top, filtered through Garbus's constraints (timing-only `ControlPointInfo` — no
effect/kiai/SV points; fixed hit windows; Basic* widgets, no OverlayColourProvider; no realm).

## Skip list (deliberately not ported)

- **EffectSection / SliderVelocityAdjustmentControl / IndeterminateSliderWithTextBoxInput** — these
  edit effect control points (kiai, scroll speed) and per-object slider velocity. Garbus's timing
  model is timing-only and gameplay scroll is constant; there is nothing for these to edit. Revisit
  only if effect points enter the chart format.
- **TimingScreen itself** — it is just the osu screen shell (EditorScreenWithTimeline split into
  list + settings); Garbus's `TimingTab` already fills that role.

## Phase A — table upgrade (highest visible value)

Port `ControlPointTable` + `RowAttribute` (+ the timing `RowAttributes/`) to replace the plain
`TimingPointRow` list:

- Virtualised rows (osu uses a `VirtualisedListContainer`; for Garbus chart sizes a plain flow is
  fine — keep our flow, take the LAYOUT: fixed columns for time / attributes, header row).
- Per-row attribute chips (BPM chip, time-signature chip) instead of one concatenated string.
- Keyboard selection (up/down arrows move selection) — small, high-feel.
- Keep Garbus behaviours pinned by existing tests: seek-on-select, re-click deselects, Add/Delete
  enabled-state semantics (`TestSceneTimingTab`).

## Phase B — timing section completeness

Extend `TimingPointSettings` toward osu's `TimingSection`:

- **LabelledTimeSignature** — numerator input for `TimeSignature` (Garbus's TimingControlPoint
  already carries it; the metronome + tick display already consume it).
- **Omit first barline** toggle (`OmitFirstBarLine` also already exists on the point).
- **BPM stepper** — port `DiscreteAdjustmentControl`/`FormDiscreteAdjustmentControl` (up/down
  buttons with `RepeatingButtonBehaviour`, which Garbus already has) around the existing BPM box.
- **GroupSection** — "time" row with a *Set to current time* button that MOVES the selected group
  (osu: remove group + re-add at new time, preserving point values, inside one undo transaction).

## Phase C — section-wide adjustments (needs a design decision)

osu's `TimingSectionAdjustments` offsets/stretches the OBJECTS in a timing section when its offset
or BPM changes ("move notes with timing changes"). The Phase 4 port note said this "does NOT apply",
but that was about legacy beatmap encoding — musically it applies to Garbus exactly as much as to
osu. Decide:

- Port it (objects between this timing point and the next shift with offset changes and rescale
  with BPM changes, one undo step), OR
- Keep the current "timing edits never move objects" contract and document it in CLAUDE.md.

Recommend porting — it is what charters expect when correcting a mis-timed section, and Garbus
already has `Snap all notes to current snap divisor` precedent for bulk object moves. Requires:
`EditorChart.PerformOnRange(start, end, action)` helper + tests for shift/stretch/undo.

## Phase D — tap-timing polish

osu's `TapTimingControl` extras Garbus currently lacks: adjust-offset/adjust-BPM buttons under the
metronome (±1/±10 ms and ±0.1/±1 BPM with repeat-on-hold — `RepeatingButtonBehaviour` is already
ported), and the reset button styling. Straightforward widget work on the existing control.

## Ordering / effort

A (table) ≈ 1 task; B ≈ 1–2 tasks (GroupSection needs the move-group transaction); C ≈ 1 task after
the design call; D ≈ half a task. A and D are independent; B's GroupSection should land before C
(both touch group mutation). All phases: headless tests in `TestSceneTimingTab` following the
existing real-click patterns.
