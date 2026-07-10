# Editor barlines — design

## Goal

Show **barlines** in the editor at every measure boundary — one line per
`TimeSignature.Numerator` beats — rendered in the compose playfield the way
osu!mania renders barlines in its playfield.

The Garbus editor compose view (`GarbusEditorPlayfield`) is a *rectangular
unrolled* playfield: **y = time** (vertical scroll), **x = angle** (the circle
unrolled). A barline is therefore a **horizontal line spanning the full width**
(all angles) at a measure's time — a direct mania analogue.

## Scope

- **In scope:** editor rendering, plus a reusable generator.
- **Out of scope (follow-up):** gameplay rendering. It will reuse the same
  generator with a label-less drawable (visual style A). This spec notes where it
  plugs in but does not build it.
- Barlines are **derived from timing, never serialized** to the chart — exactly
  like mania, which auto-generates them from `ControlPointInfo` at load time.

## Reference: how mania does it

- `osu.Game/Rulesets/Objects/BarLineGenerator.cs` — walks each
  `TimingControlPoint`, `barLength = BeatLength × TimeSignature.Numerator`, emits
  one barline per measure, honors `OmitFirstBarLine` (adds `barLength` to the
  start time to skip the section's first line).
- `osu.Game.Rulesets.Mania/Objects/BarLine.cs` — `BarLine : ManiaHitObject` with
  a `Major` flag. Garbus does **not** need `Major`: every barline here is a
  measure start, so all are uniform.

## Components

### 1. `BarLine : HitObject`
`Garbus.Game/Gameplay/Objects/BarLine.cs`

Lightweight object: `StartTime` (from `HitObject`) plus `int MeasureIndex`. Never
added to `Chart.HitObjects`; generated on demand and wrapped in drawables.

### 2. `BarLineGenerator`
`Garbus.Game/Gameplay/Objects/BarLineGenerator.cs`

The reusable pure generator — the piece both the editor (now) and gameplay
(later) consume.

Input: `ControlPointInfo` + an `endTime` (track length).
Output: `List<BarLine>`.

Algorithm (mirrors mania):
- For each `TimingControlPoint` in `ControlPointInfo.TimingPoints`:
  - `barLength = point.BeatLength * point.TimeSignature.Numerator`.
  - Start at `point.Time`; if `point.OmitFirstBarLine`, advance one `barLength`.
  - Step by `barLength`, emitting a `BarLine` per step, until the next timing
    point's time (or `endTime` for the last section).
- Assign a running `MeasureIndex` across the whole chart, **starting at 1** at the
  first emitted barline.
- Use `Precision.AlmostBigger` (or equivalent) for the float-safe end comparison,
  as mania does.

### 3. `EditorBarLineDisplay`
`Garbus.Game/Edit/EditorBarLineDisplay.cs`

A component added to `GarbusEditorPlayfield` **behind** the `HitObjectContainer`
(notes draw on top). It owns a `ScrollingHitObjectContainer` — the same mechanism
`BeatSnapGrid` already uses (see `Edit/Compose/BeatSnapGrid.cs`) — and fills it
with `DrawableBarLine`s from the generator.

- Resolves `EditorChart` (for `ControlPointInfo`) and the track length (via
  `EditorClock` / `ChartFile`, whichever exposes track length — match how existing
  timeline components read it).
- **Regenerates on `ControlPointInfo.ControlPointsChanged`.** Keep a field
  reference to the handler and **unsubscribe in `Dispose`** (per the CLAUDE.md
  lambda-subscription leak gotcha).
- Barlines are **always visible** across the playfield — unlike the snap grid's
  transient, near-cursor, fading lines. Scrolling/culling is handled by the
  framework's scrolling container.

### 4. `DrawableBarLine` (editor)
Nested in / alongside `EditorBarLineDisplay`.

- Full-width horizontal line: `RelativeSizeAxes = Axes.X`, ~2px height, light
  (e.g. white) at moderate alpha — visually **distinct** from the per-divisor
  colored snap-grid lines.
- A small `SpriteText` measure-number label anchored at the **left edge**, showing
  `MeasureIndex`.
- Direction handling (Up/Down anchors) mirrors `BeatSnapGrid.DrawableGridLine` for
  a vertically scrolling playfield.

## Data flow

```
ControlPointInfo + trackLength
        │
        ▼
  BarLineGenerator  ──►  List<BarLine>
        │
        ▼
  EditorBarLineDisplay  ──►  DrawableBarLine × N  (in a ScrollingHitObjectContainer,
                                                   behind the notes)
```

Regenerate trigger: `ControlPointInfo.ControlPointsChanged`.

## Measure numbering

Running count, **starting at 1** at the first emitted barline, continuing across
timing sections (global). Can switch to per-section reset later if desired.

## Testing

**`BarLineGenerator` unit tests** (`Garbus.Game.Tests/`):
- Single 4/4 section: barlines at `0, 4×beat, 8×beat, …`; measure indices 1,2,3…
- Numerator change across two timing points: second section steps by its own
  `Numerator × BeatLength`, indices continue.
- `OmitFirstBarLine` on a section: that section's first line is skipped.
- Track-end boundary: no barline past `endTime`.

**Editor headless test** (`Garbus.Game.Tests/Editor/`):
- Barlines populate the editor playfield; count matches generator output.
- Editing a timing point regenerates the display.
- Display unsubscribes and disposes cleanly (no leak; follows existing editor test
  patterns for subscription teardown).

## Non-goals / notes

- No `Major` / minor distinction — all barlines uniform.
- No gameplay rendering in this task (follow-up, style A, label-less, same
  generator).
- No chart-format change; barlines are never written to `.garbus`.
