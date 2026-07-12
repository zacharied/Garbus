# Editor: South-centered grid + reverse-direction view toggle — design

## Goal
Two changes to the compose editor's unrolled-angle timeline:

1. **South at the center** of the grid by default (instead of the current South↔East diagonal).
2. A **floating toggle button** at the top-left of the compose playfield that reverses the angle
   mapping direction, putting **North at the center**.

Both are purely a **view** concern: they change how the circle is unrolled onto the timeline x-axis.
No chart data changes and nothing is serialized.

## Background

`EditorAngleMapping` is the sole authority converting hit-object angles ↔ timeline x. The game's polar
convention is standard-math, CCW positive: **East=0°, North=90°, West=180°, South=270°**
(`Core/CardinalDirection.cs`).

Today `ANGLE_ORIGIN = 135` puts the grid's left edge on the North/West diagonal; the center column
(grid-degree 180) lands on the South/East diagonal (315°). Cardinals read left→right as
**West · South · East · North**, and the wrap seam falls on a diagonal so no cardinal straddles it.

Most editor drawables recompute their x from angle **every frame** via `EditorAngleMapping.ToX`, so they
follow any change to the mapping automatically. The exceptions cache geometry: the `AngleGrid`
(vertical lines + N/E/S/W letter labels) regenerates only on demand, and the two shoulder-lane strips
are positioned once in `GarbusEditorPlayfield.load`.

## Design

### 1. South at center — `ANGLE_ORIGIN = 90`

Change `EditorAngleMapping.ANGLE_ORIGIN` from `135` to `90`. The center column becomes South (270°).
Cardinals read left→right: **North(edge) · West · South(center) · East · North(edge)** — South dead
center, North split across the wrap seam (its ghost twin shows in both bands, which is exactly what the
ghost bands are for).

### 2. Reverse toggle — reflect about the East–West axis

Add a view-direction state to `EditorAngleMapping`: a sign `Direction ∈ {+1, −1}` (default `+1`).

The reversed view is the reflection `θ → −θ` (mirror about the horizontal E–W axis) applied at the
mapping boundary, then the same origin-90 formula. Concretely, define the effective angle:

```
effective(θ) = Direction == +1 ? θ : NormalizeDeg(−θ)
```

and apply it on the way **in** (`ToGridDegrees`) and its inverse on the way **out**
(`ToAngle`, `SnapX`). The reflection is its own inverse, so the same transform serves both directions.

Resulting states:

| Mode | Direction | Center | Rotation reads | West / East |
|---|---|---|---|---|
| Normal (default) | +1 | South | CCW | West left, East right |
| Reversed | −1 | North | CW | West left, East right |

West (180°) and East (0°) lie *on* the reflection axis, so they are invariant — they stay on their
sides. Only the center pole swaps (South↔North) and the rotational direction flips. This is the literal
meaning of "reverse the angle mapping direction."

**Method impact.** Only the angle↔grid boundary methods consult `Direction`:
`ToGridDegrees` (and therefore `ToX`, `GhostTwinX`), `ToAngle`, `SnapX`. The direction-independent pure
helpers are untouched: `NormalizeDeg`, `SnapAngle`, `MinimalDiff`, `ReflectionSum`, `VisibleWrapCopies`.
All 39 existing call sites of `ToX`/`ToAngle`/… stay unchanged — they read the current direction
implicitly.

### 3. Shoulder strips → West/East

A `ShoulderNote`'s true in-game angle is West (Left side) or East (Right side)
(`ShoulderNote.AngleDeg => Side.ToAngleDeg()`). The editor currently draws it in an *offset* lane strip
at the diagonal quadrant boundaries (`LEFT_SHOULDER_ANGLE_DEG = 225`, `RIGHT_SHOULDER_ANGLE_DEG = 45`)
so it doesn't overlap the cardinal W/E notes.

Drop the offset: set **`LEFT_SHOULDER_ANGLE_DEG = 180` (West)** and
**`RIGHT_SHOULDER_ANGLE_DEG = 0` (East)**. This:

- aligns each strip with the lane the note actually travels in (Left→West, Right→East),
- keeps the two strips symmetric about the (now South-centered) grid,
- makes them **invariant under the reverse toggle** (both sit on the reflection axis).

Trade-off (accepted): a shoulder square now shares a column with a same-time cardinal W/E note. They are
distinct colors (shoulder = purple) and only visually overlap if authored at the same beat.

Nothing else about shoulders changes — `ShoulderXFraction`, the placement blueprint's nearest-strip side
pick, and `wrapDistance` all keep working with the new constants.

### 4. State ownership & redraw

Source of truth: a DI-cached `Bindable<bool> ReverseAngleView` (default `false`), cached by `GarbusEditor`
alongside the existing `AngleSnap` / `BindableBeatDivisor`. A single subscription mirrors its value into
`EditorAngleMapping.Direction` (`false → +1`, `true → −1`) with `runOnceImmediately` so the static is
always asserted from the bindable on editor load (this also resets it between runs).

Redraw:

- **Per-frame x drawables** (cardinal/hold/slam/slider editor drawables, ghost twins, placement
  blueprints) recompute from `ToX`/`ToAngle` each frame → auto-update, no wiring needed.
- **`AngleGrid`** binds to `ReverseAngleView` (as it already binds `AngleSnap`) and calls `regenerate()`
  on change so the lines and N/E/S/W labels re-place. **Unsubscribe in `Dispose`** (CLAUDE.md leak rule).
- **Shoulder strips** sit on the reflection axis, so they need no direction wiring. (They still move once
  for the `ANGLE_ORIGIN` change, which is a compile-time constant.)

`ReverseAngleView` is a view preference — never written to the chart or serialized.

### 5. Floating toggle button

A small floating toggle drawable hosted top-left of the compose playfield, on the same vertical level as
the N/E/S/W letter labels the `AngleGrid` draws at the top. It shows the current x→θ rotation direction —
**`⇄ CCW`** in Normal mode (angle increases counter-clockwise with x; South centered), **`⇄ CW`** when
reversed (North centered) — and toggles `ReverseAngleView` on click. Minimal styling consistent with the existing
editor chrome; exact host container (composer content vs. playfield overlay) settled during planning,
constrained to render above the grid and receive clicks.

## Components & boundaries

- **`EditorAngleMapping`** — `ANGLE_ORIGIN = 90`; new `Direction` sign consulted by `ToGridDegrees`,
  `ToAngle`, `SnapX`. Pure helpers unchanged.
- **`GarbusEditorPlayfield`** — shoulder constants → 180/0; hosts (or exposes a slot for) the toggle
  button; `AngleGrid` gains a `ReverseAngleView` binding + regenerate-on-change + `Dispose` unsub.
- **`GarbusEditor`** — caches `Bindable<bool> ReverseAngleView`; one subscription drives
  `EditorAngleMapping.Direction`.
- **Toggle button drawable** — pure view control: reads/writes `ReverseAngleView`, displays centered
  pole. Knows nothing about hit objects.

## Testing

- **`TestEditorAngleMapping`** — update origin-dependent asserts (origin now 90); add:
  - cardinal x-positions in Normal mode (South center, North at edges),
  - reversed-mode round-trip (`ToAngle(ToX(a))` under `Direction = −1`),
  - reversed-mode cardinal positions (North center; West still left, East still right),
  - a `[TearDown]` (or `[SetUp]`) resetting `EditorAngleMapping.Direction = +1` so the static can't bleed
    between tests.
- **`TestSceneEditorPlayfield`** / compose scenes — update any assumption of `ANGLE_ORIGIN == 135` or the
  old shoulder angles (225/45).
- **New scene coverage** — toggling `ReverseAngleView` (via the button or the bindable) moves the North
  label/column to center and leaves West on the left / East on the right; shoulder strips don't move.

## Out of scope

- Persisting the toggle across editor sessions (config setting) — it resets to South-centered each load.
- Any change to gameplay rendering or the horizontal (time-based) Timeline strip.
- Keyboard shortcut for the toggle (button only for now).
