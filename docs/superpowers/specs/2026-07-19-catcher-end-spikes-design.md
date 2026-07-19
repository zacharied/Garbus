# Catcher end-spikes — design

## Summary

Add decorative/feedback **spikes** that grow inward from the two ends of each stick-catcher
arc, crossing over the playfield ring toward the centre. Each spike is a needled flame with a
thin bright core line of the catcher's pure colour and a soft glow that is transparent at the
edges, brightening toward the tip while dissolving to fully transparent. The spikes appear
whenever the catcher is active (out of the deadzone) and **intensify** while the catcher is
actually eating a slider body.

There is one catcher per side: Left is blue (`Constants.LeftColour`, `#0564D0`), Right is
magenta (`Constants.RightColour`, `#D41159`).

## Behaviour

Per catcher, driven by `AnalogInputManager.SliderCatcher[Side]` plus a catch signal:

- **Deadzone** (`!SliderCatcher.Activated`): no arc, no spikes. Unchanged from today.
- **Active, not catching**: the arc is visible; a spike grows inward from *each* of the arc's
  two angular ends (`StartRadians` and `EndRadians`). The idle look is shallow and faint —
  reaches roughly `0.6×` the ring radius, with low glow intensity.
- **Catching** (any alive slider body of this catcher's side currently has its leading edge
  caught): the spikes intensify along three axes at once:
  - **Reach**: the tip extends deeper, to roughly `0.35–0.4×` the ring radius.
  - **Brightness**: core and glow brighten.
  - **Pulse**: a continuous ~1 s breathing pulse on reach/brightness while the catch lasts.

The transition between idle and catching should be smooth (interpolated), not a hard snap.

## Geometry and appearance

Matches the approved visual mockup, expressed in engine terms:

- **Silhouette**: a *needled flame* — concave sides tapering to a sharp point aimed at the
  playfield centre. Each spike's base sits on its arc end at the catcher radius
  (`1.06×` ring radius, matching `StickIndicator.RadiusScale`), and the base is slim
  (~1.4° angular half-width for the crisp core; the soft glow bleeds out to ~5°).
- **Two spikes per catcher**, one centred on each arc end angle. Half of each base overhangs
  past the arc end (the spike "comes out of" the end). They track the arc ends every frame as
  the stick sweeps, so they stay anchored while the catcher rotates and resizes.
- **Fill**: a thin bright **core line** of the pure catcher colour runs down the spine. The rest
  is a **soft glow that is transparent at the lateral edges**, brightening from base to tip
  (blue → cyan → white for Left; magenta → pink → white for Right) *and* dissolving to fully
  transparent alpha at the tip, so the needle melts into the playfield rather than ending on a
  hard edge.
- **Draw order**: spikes draw over the ring interior but under the arc stroke, so the arc caps
  the spike bases cleanly. `StickIndicator` (and therefore its spikes) already sits above the
  `Ring` in `GarbusPlayfield`'s child list.

Numeric starting values (tune during implementation; all radii are fractions of the current ring
radius `= GarbusScrollingHitObjectContainer.ScrollLength`):

| Parameter            | Idle (active) | Catching (pulse range) |
| -------------------- | ------------- | ---------------------- |
| Tip reach (× ring R) | ~0.60         | ~0.34 – 0.40           |
| Core half-width      | ~1.4°         | ~1.4°                  |
| Glow half-width      | ~3.6°         | ~4.8 – 6.0°            |
| Glow opacity         | ~0.45         | ~0.70 – 0.95           |
| Pulse period         | —             | ~1.0 s                 |

## Architecture

**Ownership.** A new drawable owned by `StickIndicator` renders the pair of spikes. Options:
either a single `CatcherSpikes` container that draws both, or two spike drawables the indicator
positions. `StickIndicator` already:
- resolves `AnalogInputManager` and reads `SliderCatchers[Side]`,
- sweeps `arc.StartRadians` / `arc.EndRadians` each `Update`,
- knows its `Side` (colour source).

So the spikes need no new wiring in `GarbusPlayfield`. The indicator passes the two end angles,
the side colour, `Activated`, and the catch flag to the spikes each frame.

**Catch signal.** `DrawableSliderBody.updatePath()` already computes `isLeadingEdgeCaught()`
every frame. Surface it as a read-only `bool IsBeingCaught` property set in that method. The
spikes (or `StickIndicator`) resolve the `[Cached] Ring` and each frame evaluate:

```
bool catching = ring.AliveHitObjects
    .OfType<DrawableSliderBody>()
    .Any(b => b.HitObject.Side == Side && b.IsBeingCaught);
```

`Ring.AliveHitObjects` already exists for exactly this kind of presence query. This keeps the
catch state derived from the authoritative per-frame body computation — no duplicated catch
logic and no cross-object mutable flags beyond the one exposed property.

## Rendering approach

The main technical risk: `SmoothPath` (used by `Arc` and `DrawableSliderBody`) is single-colour
via its framebuffer blit, so it cannot express the base→tip colour+alpha gradient directly.

**Primary plan**: render each spike as a **gradient-capable primitive** — an elongated
`Triangle`/quad given a `ColourInfo` vertical gradient (pure opaque catcher colour at the base
edge → bright, alpha-0 colour at the apex), oriented so its axis runs radially from the arc end
toward the centre. Wrap it in a `GlowEffect` (the same effect `DrawableSliderBody` already uses
for its additive halo) to produce the soft, edge-transparent bloom and the bright core read.
Idle↔catching drives the apex distance (reach), the gradient's brightness, and the pulse via
per-frame values or transforms.

**Fallback**: if a per-vertex/`ColourInfo` gradient on the chosen primitive proves fiddly, use a
pre-baked gradient sprite (flame texture carrying the radial + transverse falloff) tinted per
side. Note the tint caveat — a multiplicative tint cannot brighten toward white at the tip, so
the baked texture must encode the luminance ramp itself.

The concave needle silhouette can come from the primitive's shape (a narrow triangle already
reads as a needle) plus the glow softening; exact concavity is a polish detail, not load-bearing.

## Testing

Headless coverage (extend `Garbus.Game.Tests`, reusing the `TestSceneGameplay` manual-clock and
`AnalogInputManager` setup):

- Spikes are hidden when the catcher is in the deadzone.
- When active, two spikes are present and anchored to the arc's two end angles (verify they
  follow after moving the stick / changing catcher angle).
- Spikes intensify (measurably — reach/brightness) when a matching-side slider body reports
  `IsBeingCaught`, and relax when it stops.
- A body of the *other* side being caught does not intensify this catcher's spikes.
- Left spikes carry the blue colour, Right the magenta.

Prefer asserting on the derived catch flag / spike state parameters over pixel output, since the
glow/gradient render is not deterministically inspectable headless.

## Out of scope

- No change to catch *detection* or judgement — this is purely a visual layer over the existing
  per-frame catch computation.
- No new config/toggle for the effect.
- The existing `tipBox` "consumed" marker and escape-band fade on `DrawableSliderBody` stay as
  they are; spikes are an additive, independent feedback layer at the arc ends.
