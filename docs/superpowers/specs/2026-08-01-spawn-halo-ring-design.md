# Drawing the spawn halo as a ring

## Goal

Draw the spawn halo — until now a purely geometric radius — as a thin gray ring on the playfield, so
the radius objects appear on is visible rather than implied.

## Relation to the spawn halo design

[2026-08-01-maimai-spawn-halo-design.md](2026-08-01-maimai-spawn-halo-design.md) lists this under
**Out of scope**:

> **No visible halo drawable.** maimai draws no halo; objects simply appear at that radius. Rendering
> a ring is a separate visual decision.

That separate decision is now made, in favour of drawing it. This spec supersedes that bullet, which
is updated to point here. Nothing else in that document changes: the radius function, the hold, and
the drawable coupling are all untouched. This is presentation only — no geometry, lifetime, or
judgement behaviour moves.

## Component

`Garbus.Game/UI/SpawnHaloRing.cs`, a sealed `Container` wrapping a single full-circle
`Arc(0, 2π)`.

`Arc` derives its radius from its own `ChildSize` and has no radius parameter. Rather than give it
one, the wrapper is sized to the halo:

```
RelativeSizeAxes = Axes.Both
Anchor = Origin = Anchor.Centre
Size = new Vector2(SpawnHaloFraction)
```

The relative size is the fraction itself, not twice it. `ScrollLength` is already a *radius*
(`min(W, H) / 2` of the playfield), so the two halvings cancel: a child at `RelativeSizeAxes.Both`
sees `ChildSize = fraction × playfieldSize`, and the `Arc`'s own `min(ChildSize.X, ChildSize.Y) / 2`
evaluates to `fraction × playfieldSize / 2` = `fraction × ScrollLength` — the halo radius as defined
in `docs/presentation-specs/Playfield.md`. Checked against the test calibration below: a 400 × 400
container at fraction 0.25 gives a wrapper of 100 px and a drawn radius of 50, matching
`0.25 × 200`. `Arc` is not modified.

This was chosen over two alternatives:

- **Adding a `RadiusFraction` bindable to `Arc`.** `Arc` is shared with the outer ring and
  `StickIndicator`; bending it for one caller adds surface to a general type without removing any
  wiring, since the fraction still has to be sourced from scrolling info.
- **Drawing inside `GarbusScrollingHitObjectContainer`.** It owns `HaloRadius` directly, but it is a
  vendored `HitObjectContainer` responsible for lifetime and layout. Every other piece of playfield
  furniture lives in `Ring`, and decorative children would blur that boundary.

## Placement

`SpawnHaloRing` joins `Ring`'s back-to-front furniture list **immediately after `ComboDisplay` and
before `HitObjectContainer`**:

    PlayfieldRadialLines → ChordConnectorOverlay → ComboDisplay → SpawnHaloRing →
    HitObjectContainer → laneContainer → judgementFeedback → Arc (outer ring)

Two forces fix this slot.

**In front of the combo.** At the default `SpawnHaloFraction` of 0.12 the ring is roughly 120 px
across on a 1080p window, which lands almost exactly around `ComboDisplay`'s 96 px digits. Drawing
the ring in front means the halo radius always reads exactly, rather than being broken up by
whatever the combo currently is.

**Behind hit objects.** The outer ring draws front-most, but it only ever clips an object in
passing. Objects *sit* on the halo for their entire spawn hold, so a front-most halo would put a
gray line through every spawning note for the whole hold. Behind the hit object container, a note
holding on the halo occludes the ring instead of being sliced by it.

## Parameters

| Name | Type | Default | Why |
| --- | --- | --- | --- |
| `Thickness` | `BindableFloat` on `SpawnHaloRing`, bound to the inner `Arc.Thickness` | 2 | The outer ring is 5 and the radial spokes are 3; "thin" sits below both. |
| Colour | set in the constructor | `Colour4.White.Opacity(0.35f)` | Reads as gray on the dark playfield. Translucency matters here specifically because the ring draws in front of the combo — it tints the digits rather than slicing them. |
| `Resolution` | fixed on the inner `Arc` | 64 | At ~120 px diameter, `Arc`'s default 32 gives ~12 px chords and visible faceting. Not tunable; there is no reason to lower it. |

`Thickness` is a bindable rather than an `init` property so the tuning scene can drive it live, which
matches how `Arc` and `PlayfieldRadialLines` already expose their own thickness. Translucency is
driven through the drawable's standard `Alpha`, so no custom colour member is added.

The ring takes no `GarbusSetting`. Consistent with `SpawnHaloFraction` and `SpawnDuration`, this is
art direction rather than player preference.

## Data flow

One direction, no per-frame work:

    GarbusScrollingInfo.SpawnHaloFraction → SpawnHaloRing.Size → Arc.ChildSize → drawn radius

`SpawnHaloFraction` is resolved through `GarbusScrollingInfo` with `CanBeNull` plus a private
fallback instance, the pattern `GarbusScrollingHitObjectContainer` and `DrawableGarbusHitObject`
already use, so bare test scenes without a cached scrolling info still get the production default.

`Arc` already invalidates its path only on `DrawSize` change, so a halo whose fraction is not moving
costs nothing per frame. Resize tracking is free: every size in the chain is relative.

## Edge cases

- **Fraction 0.** The wrapper hides rather than handing `Arc` a degenerate zero-radius path.
- **Live fraction change.** The tuning slider writes the bindable; the wrapper resizes and `Arc`
  regenerates. Nothing rebuilds.
- **Editor Mini preview.** It reuses `Ring`, so the halo ring appears there too, scaled down with
  everything else. This is intended — the preview is meant to match gameplay presentation.

## Testing

A visual test scene pins the geometry, hand-derived from the spec formula rather than from the
implementation's own values. It reuses the calibration anchor already established by
`TestSceneSpawnHaloSliderBody`: a 460 px playfield less its 30 px padding gives the container
400 × 400, so `ScrollLength` is 200, and at `SpawnHaloFraction` 0.25 the ring's radius must be 50.

- Radius is 50 at fraction 0.25 on a 200 px `ScrollLength`.
- A live fraction change moves the radius (0.25 → 0.1 gives 20).
- A resize moves the radius (halving the playfield halves it).
- Fraction 0 hides the ring.

The ring's radius is its wrapper's `DrawSize.X / 2`, a public framework property, so these assert
against real geometry without adding any test-only member to the production type.

**Tuning goes into the existing scene, not a new one.** `Garbus.Game.Tests/Tuning/TestSceneSpawnHaloTuning.cs`
already drives a real `GarbusPlayfield` over a looping stream and exposes halo fraction, spawn
duration, scroll range and playback rate. The ring is furniture, so it appears there automatically;
the scene gains two sliders — ring thickness and ring alpha — reaching the drawable via
`ChildrenOfType<SpawnHaloRing>()`. One scene owns all halo tuning.

## Out of scope

- **No animation.** The ring does not pulse, fade in, or react to objects arriving on or leaving the
  halo. It is static furniture.
- **No player-facing setting.**
- **No change to the halo geometry, the hold, or any drawable's spawn coupling.**
