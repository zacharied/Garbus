# Slam gesture detection & judgement — design

## Goal

Make `GarbusSlamCentered` and `GarbusSlamEdge` judgeable in gameplay. The drawables already spawn,
animate, and route through `PlayScreen.CreateDrawableRepresentation`, but their `CheckForResult` is
inherited and does nothing, so slams never hit or miss. This adds the analog-stick gesture detection
that resolves them.

This is a **first cut** on judgement: a detected gesture inside the timing window yields **Perfect**;
the window elapsing with no gesture yields **Miss**. The spec-faithful early-permissive grades (Perfect
±200 ms / Near late-only to +300 ms) are deliberately deferred — see [Deferred](#deferred).

## Background

Two slam hit-object types exist (`Garbus.Game/Objects/`), both early-permissive per
`docs/rules-specs/Judgement.md`:

- **SlamCentered** (`Side`, `AngleDeg`) — flick the stick outward from centre toward its angle. Detected
  as the stick radius rising past a threshold with stick angle within tolerance of the slam's angle.
- **SlamEdge** (`Side`, `AngleDeg`, rotational `Direction`) — with the stick already at the edge, sweep
  it through the slam's angle in the given rotational direction.

The existing analog input model (`Garbus.Game/Input/AnalogInputManager.cs`) carries a per-side
`SliderCatcher` that tracks only the *instantaneous* stick state (angle, radius, activated). Slider
children poll `SliderCatcher.IsCatchingAt(angleDeg)` each frame. Slams need *motion over time*, which
the current model does not retain — hence a new tracker.

## Components

### `StickGestureTracker` (new) — one per side, owned by `AnalogInputManager`

The isolated unit that holds this feature's novelty. Its only dependency is a stream of stick samples;
it has no knowledge of drawables, the framework's joystick plumbing, or hit objects. This keeps it
directly unit-testable.

State:

- A bounded ring buffer of recent samples `(double time, Vector2 position)`, pruned once older than a
  fixed retention (~350 ms, comfortably larger than the widest timing window).

API:

- `void AddSample(double time, Vector2 position)` — append one sample, prune stale entries.
- `bool FlickedTowards(int angleDeg, double sinceTime)` — true if any adjacent sample pair at or after
  `sinceTime` shows the radius crossing the **flick threshold** in the outward direction (below → at/
  above), with the angle at the crossing within **angle tolerance** of `angleDeg`.
- `bool SweptThrough(int angleDeg, RotationalDirection dir, double sinceTime)` — true if any adjacent
  sample pair at or after `sinceTime` has both endpoints **at or beyond the edge threshold** and whose
  angular travel, taken in `dir`, passes through `angleDeg`.

Scanning a recency buffer (rather than latching an edge-triggered "happened this frame" flag) is what
lets a gesture that completes slightly before the drawable polls — including *before* `StartTime`, as
early-permissive demands — still register.

### `AnalogInputManager` (modified)

- Construct two `StickGestureTracker`s keyed by `HorizontalDirection`, mirroring the existing
  `SliderCatchers` dictionary, and expose them (e.g. `StickGestureTrackers`) for drawables to resolve.
- In `Update`, feed each tracker one `AddSample(Time.Current, position)` using the same per-side stick
  position the `SliderCatcher` already maintains.
- `AnalogInputManager` is added first in `GarbusPlayfield`'s children, so it updates before the hit
  objects each frame — samples for frame *N* are present before any slam drawable polls on frame *N*.

### `DrawableSlamCentered` / `DrawableSlamEdge` (modified)

Each overrides `CheckForResult(bool userTriggered, double timeOffset)` (the base drives it every frame
while the drawable is alive; `timeOffset = Time.Current − StartTime`):

```
// window is a local constant for this first cut (see Deferred)
if (timeOffset < -window)
    return;                                   // watch window not open yet

bool matched = <tracker for HitObject.Side>.<query>(HitObject.AngleDeg, [dir,] StartTime - window);
if (matched)
{
    ApplyMaxResult();                         // Perfect
    return;
}

if (timeOffset > window)
    ApplyMinResult();                         // Miss — window elapsed with no gesture
```

- SlamCentered calls `FlickedTowards(AngleDeg, StartTime - window)`.
- SlamEdge calls `SweptThrough(AngleDeg, Direction, StartTime - window)`.

Resolve the trackers via `[Resolved] AnalogInputManager` (as `DrawableSliderChild` resolves it today).
Base `Judgement` already returns `MaxResult = Perfect` / `MinResult = Miss`, so no new judgement class is
needed for this pass.

## Tunable parameters (placeholders for this first cut)

Introduced as named constants and flagged as tunable; not final gameplay values.

| Parameter          | Placeholder | Notes                                                            |
|--------------------|-------------|------------------------------------------------------------------|
| Flick threshold    | 0.7 radius  | Distinct from `SliderCatcher.DEADZONE` (0.4); "near the maximum" |
| Edge threshold     | 0.7 radius  | Stick counts as "at edge" for SlamEdge sweeps                    |
| Angle tolerance    | 30°         | Compare with `SliderCatcher` ±36° half-size                     |
| Timing window      | 200 ms      | Symmetric for this pass; Perfect window from the spec           |
| Sample retention   | 350 ms      | Ring-buffer horizon; > widest window                            |

## Data flow

```
per frame:
  AnalogInputManager.Update
    -> StickGestureTracker[side].AddSample(Time.Current, stickPos)   // prune stale
  DrawableSlam*.CheckForResult(false, Time.Current - StartTime)      // base-driven
    -> tracker[Side].FlickedTowards / SweptThrough(AngleDeg, .., StartTime - window)
       -> ApplyMaxResult()  on match
       -> ApplyMinResult()  once timeOffset > window
```

## Testing

Primary coverage is **direct unit tests on `StickGestureTracker`** — no framework joystick plumbing is
required (existing tests do not drive analog input at all):

- `FlickedTowards`: an outward radius crossing at the target angle returns true; a crossing off-angle
  (beyond tolerance) returns false; a slow drift that never crosses the threshold returns false; a
  crossing older than `sinceTime` returns false.
- `SweptThrough`: an at-edge sweep passing through the target in the matching direction returns true;
  the same sweep in the opposite direction returns false; a sweep while inside the edge threshold
  returns false; angle-wrap across the ±180° seam is handled.
- Buffer pruning: samples past retention are dropped and do not satisfy queries.

A gameplay-scene test that drives the stick and asserts hit/miss is a stretch goal; the tracker unit
tests are the contract that pins behaviour.

## Deferred

- **Near grade and asymmetry.** The early-permissive family (Perfect / Near / Miss) with the asymmetric
  window (Perfect ±200 ms, Near late-only to +300 ms) and a proper slam `HitWindows`, replacing the
  local `window` constant and Perfect/Miss-only grading.
- **Per-event sampling.** Feeding samples from `OnJoystickAxisMove` in addition to per-frame `Update`,
  for finer resolution on very fast flicks that resolve within a single frame.
```
