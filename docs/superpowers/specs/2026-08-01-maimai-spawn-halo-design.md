# Spawn halo and stationary spawn phase

## Goal

Hit objects stop appearing at the exact centre of the playfield. Instead they appear on a **spawn
halo** — a small circle concentric with the playfield — at their own angle, hold still there while
their spawn animation plays, and begin travelling outward the instant that animation completes.

This is the maimai note-spawn model. Two changes, one shared parameter set.

## Parameters

Three values on `GarbusScrollingInfo`, all bindable:

| Name | Meaning | Default |
| --- | --- | --- |
| `TimeRange` | Existing. Sets radial velocity: `ScrollLength / TimeRange` px/ms. | 700 ms (speed 10) |
| `SpawnHaloFraction` | Halo radius as a fraction of `ScrollLength`. Dimensionless so it survives resize. | 0.12 |
| `SpawnDuration` | How long an object holds still on the halo — and how long its spawn tween runs. | 125 ms |

Derived:

```
haloRadius = ScrollLength × SpawnHaloFraction
travelTime = TimeRange × (1 − SpawnHaloFraction)
leadTime   = travelTime + SpawnDuration
```

`SpawnDuration` is a fixed constant, not a function of scroll speed. At the fastest scroll speed
(`TimeRange` 350 ms) the hold is 29% of the total lead time, which may read as long; the tuning scene
exists to settle the default. Scaling it with scroll speed is deliberately out of scope.

## The mapping

Let `Δ = objectTime − currentTime`. The radius function is the existing linear map with a lower
bound:

```
radius(Δ) = max(haloRadius, ScrollLength − Δ × ScrollLength / TimeRange)
```

Three properties make this the whole change:

- At `Δ = travelTime` it evaluates to exactly `haloRadius` — the floor and the ramp meet without a
  seam.
- At `Δ = 0` it evaluates to `ScrollLength`. Objects still reach the ring at their own time.
- For `Δ < 0` it extrapolates past the ring unchanged, so the slider escape bands and the catcher
  band keep working.

Radial velocity is unchanged from today at every scroll speed, so the scroll-speed setting keeps its
current calibration and needs no recalibration.

The stationary phase is not a separate branch. It is the floor.

## Where the halo lives

The floor goes in `GarbusScrollingHitObjectContainer`, in `DistanceFromCentreAtTime` and
`ProgressAtTime`. `IScrollAlgorithm` and `ConstantScrollAlgorithm` are untouched.

The two accessors keep their existing difference: `DistanceFromCentreAtTime` is floored at
`haloRadius` and otherwise unbounded above, so callers can still clip shapes the ring has consumed;
`ProgressAtTime` is floored at `haloRadius` *and* keeps its existing ceiling at `ScrollLength`. The
change adds a lower bound to both; it does not touch the upper one.

A halo is a polar concept with no meaning in osu's linear scroll, and those two files are vendored
under the deviate-minimally rule. The container already owns the polar reinterpretation — it is the
right home. Keeping the algorithm halo-free also guarantees the editor composer stays insulated by
construction: it holds its own `EditorScrollingInfo` with a separate `ConstantScrollAlgorithm`
instance, so there is nothing halo-shaped for it to inherit.

Two consequences inside the container:

- `computeDisplayStartTime` stops routing through `IScrollAlgorithm.GetDisplayStartTime` and returns
  `StartTime − leadTime` directly.
- `LengthAtTime` is deleted. Its signature takes `startTime` and `endTime` but no `currentTime`, so
  it cannot express a length that now depends on when you look. It has no callers — the editor
  blueprints that call `LengthAtTime` reach the editor's linear `ScrollingHitObjectContainer`
  (`GarbusSelectionBlueprint.HitObjectContainer`), not this one.

`SpawnHaloFraction` and `SpawnDuration` invalidate `layoutCache` on change, exactly as `TimeRange`
does.

## Where the spawn tween couples in

Today the spawn tween is decorative. `DrawableGarbusHitObject.InitialLifetimeOffset` anchors
`UpdateInitialTransforms` at the centre-spawn instant, and each drawable hardcodes its own duration
(125 ms `ScaleTo` for notes and slams, 100 ms `FadeInFromZero` for hold bodies and slider bodies).
Nothing reads those numbers.

Under this model that duration *is* the stationary hold, so it needs exactly one owner.

`DrawableGarbusHitObject<T>` gains `protected double SpawnAnimationDuration`, reading
`GarbusScrollingInfo.SpawnDuration`, and its `InitialLifetimeOffset` becomes `leadTime` so the tween
fires at the halo-spawn instant rather than the centre-spawn instant. Every drawable with a spawn
tween descends from `DrawableGarbusHitObject<T>`, so one member reaches all eight sites:

- `DrawableCardinalNote`, `DrawableSlamCentered`, `DrawableSlamEdge`, `DrawableShoulderNote`,
  `DrawableShoulderHoldNote` — `ScaleTo`
- `DrawableCardinalHoldNote` — head `ScaleTo` and body `FadeInFromZero`
- `DrawableSliderBody` — three `FadeInFromZero` calls

All swap their literal for `SpawnAnimationDuration`. Tween end and movement start become the same
number by construction; they cannot drift.

This costs the current 125 vs 100 ms distinction. That is intended — a body that finishes fading
before its object starts moving reads as a stutter.

`GarbusScrollingInfo` is resolved `CanBeNull`, so bare test scenes without one must still produce a
sane `leadTime` and `SpawnAnimationDuration`. Today `InitialLifetimeOffset` falls back to a bare
`700` literal that silently duplicates the `TimeRange` default. Instead, the three defaults become
public constants on `GarbusScrollingInfo`, the bindables initialise from them, and the drawable's
fallback computes `leadTime` from those same constants — one source of truth for the default, no
second literal to drift.

## What needs no change

Every drawable already clamps defensively against the centre — `Math.Clamp(dist, 0f, ring)` in the
shoulder and hold notes, `renderBand(0f, ringRadius, …)` in the slider body. Flooring at the source
makes those lower bounds dead but harmless, so no geometry code changes.

Hold and slider bodies extrude out of the halo automatically: both band endpoints already resolve
through `DistanceFromCentreAtTime`, so while a tail is still inside the hold window the band's inner
edge pins at the halo and the body stretches outward from it.

Rewind, restart, and editor-preview scrubbing stay correct. `UpdateInitialTransforms` is
absolute-sequenced, and its window is exactly the flat region of the map, so any seek lands on a
consistent position-and-tween pair.

Judgement, hit windows, and the warning indicator are purely time-based and are unaffected.

## Behaviour changes

- A slider or hold whose entire duration falls inside the hold window renders as a stub at the halo
  before extruding. Previously its nodes had negative radii and were clipped away entirely. This is
  correct for the model — a short object should look like a point note during its spawn.
- The chord connector polygon opens at halo radius instead of growing from a point.
- The editor Mini preview inherits all of the above, since it reuses the gameplay drawables and the
  polar container.

## Out of scope

- **No visible halo drawable.** maimai draws no halo; objects simply appear at that radius. Rendering
  a ring is a separate visual decision.
- **No user-facing settings.** `SpawnHaloFraction` and `SpawnDuration` are art direction, not player
  preference. They get a tuning scene, not `GarbusSetting` keys.
- **No scroll-speed-dependent `SpawnDuration`.**

## Verification

`docs/presentation-specs/Playfield.md` is updated first — its playfield model currently states that
objects "emerge from the center of the circle" — to define the halo, the hold, and the radius
function above. Tests then anchor to that spec text, with hand-derived expected values:

- Radius equals `haloRadius` throughout the hold window.
- Radius equals `haloRadius` exactly at the `Δ = travelTime` boundary (floor meets ramp).
- Radius equals `ScrollLength` at `Δ = 0`.
- Radial velocity during the travel phase matches `ScrollLength / TimeRange`, unchanged from today.
- Entry lifetime start equals `StartTime − leadTime`.
- The hold duration is invariant as `TimeRange` varies.

`Garbus.Game.Tests/Tuning/TestSceneSpawnHaloTuning.cs` exposes `SpawnHaloFraction` and
`SpawnDuration` as live sliders so the defaults can be eyeballed in the visual test browser, per the
new-visual-elements rule.

`docs/agents/gameplay.md` is updated — its layout section states that objects spawn at the centre.
