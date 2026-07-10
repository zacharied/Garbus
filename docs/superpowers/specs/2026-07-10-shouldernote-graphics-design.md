# ShoulderNote graphics — two squares + connecting arc

## Goal

Replace the ShoulderNote's current static "paddle" arc sprite with a composite visual: two purple
square sprites riding outward along the ±45° quadrant diagonals of the note's side, joined by a
circular arc that grows as they travel. Judgement is unchanged — the ShoulderNote remains a single
hit object judged on one timed shoulder-button press.

## Background

A `ShoulderNote` has a `Side` (Left/Right). Its `AngleDeg` is fixed to the side's cardinal line:
Right → 0° (East), Left → 180° (West). The playfield is polar: `x = cos θ·r`, `y = −sin θ·r`, θ = 0
points right and increases counter-clockwise. Hit objects emerge from the centre and reach the outer
ring at their `StartTime`; travel distance from centre is `scrollingContainer.DistanceFromCentreAtTime(time)`.

Per `docs/rules-specs/Inputs.md`, a ShoulderNote is "an arc connecting the two points above and below
the East and West cardinal lines … the arc grows bigger as the points grow apart in vertical distance."
The two points sit at the ±45° boundaries of the side's quadrant.

The current `DrawableShoulderNote` is point-positioned by the scrolling container at the E/W angle and
draws one curved "paddle" sprite sized to the ring radius. That single-anchor model can't place two
sprites at *different* angles, so the drawable is rebuilt to self-position.

## Design

### Component: `DrawableShoulderNote` (rewritten)

Becomes an `ISelfPosition` drawable (same pattern as `DrawableSliderBody`):

- `RelativeSizeAxes = Axes.Both`, so it fills the scrolling container; children are placed in
  playfield-centre polar coordinates. Implementing `ISelfPosition` makes the container skip
  point-positioning it (`GarbusScrollingHitObjectContainer.UpdateAfterChildrenLife`).
- Stays a `DrawableNote<ShoulderNote>`: existing `OnPressed` / `CheckForResult` are untouched, so the
  object is still judged as a single timed press. No nested hit objects.

**Children:**

- Two `Sprite`s using the `"square"` texture, `Colour4.Purple`, size ~80 (matching
  `DrawableCardinalNote`), anchored to the container centre.
- One `Arc` (existing `Garbus.Game/UI/Arc.cs`), tinted purple, thickness ~15px, no glow.

**Per-frame geometry (`Update`):**

Let `base` = `HitObject.AngleDeg` in degrees (0 for right, 180 for left), and
`r = scrollingContainer.DistanceFromCentreAtTime(HitObject.StartTime)`.

- Square A position = polar(`base + 45°`, `r`).
- Square B position = polar(`base − 45°`, `r`).
- Arc spans radians `[base − 45°, base + 45°]` (a 90° slice) at radius `r`.

The `Arc` draws at radius `min(ChildSize)/2`, so to make it grow with `r` it is given a fixed
`Size = new Vector2(2r)` (updated each frame) with `RelativeSizeAxes = None`, anchored centre. Its
`StartRadians` / `EndRadians` bindables are set to `base ± 45°` in radians. (Degrees→radians via the
same convention the polar helpers use.)

At `r → 0` (spawn) the squares sit near centre and the arc is tiny; at `r → ringRadius` (StartTime)
the squares reach the ring corners and the arc is the quadrant's 90° outer boundary.

### Animations

- **Spawn (`PrepareForUse`):** scale the visual `0 → 1` over 125 ms `Easing.In`.
- **Hit (`UpdateHitStateTransforms`, `ArmedState.Hit`):** fade out + scale up (~1.4×) over 350 ms
  `Easing.OutQuint`, then `Expire()`. Applied to squares + arc as a unit.
- **Miss (`ArmedState.Miss`):** fade colour to red and fade out over 1000 ms, then `Expire()`.
- Colour: purple throughout.

### Removed

- The `"paddle"` texture usage and the paddle-aspect / curve-radius sizing logic.

## Testing

- `TestSceneGameplay` already exercises shoulder-note judgement (auto-miss and key-press hit). These
  must still pass — judgement code is unchanged.
- Add a headless geometry assertion: a right ShoulderNote places its two squares symmetrically at
  ±45° around 0° (and a left one around 180°) at the expected radius, if the drawable exposes the
  square positions cleanly for inspection.

## Non-goals

- HoldShoulderNote visuals (the line-toward-centre variant) are out of scope; this change must not
  break its ability to reference a ShoulderNote path later.
- No judgement, timing, or input changes.
