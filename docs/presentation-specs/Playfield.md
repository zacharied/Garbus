*The contents of this document differ from the current implementation.*

# Playfield presentation specification

This is the canonical reference for how every Garbus hit object is presented visually. It describes the shape, motion, and on-screen feedback of each object type. For the input behavior these visuals prompt, see `../rules-specs/Inputs.md`; for timing and judgement, see `../rules-specs/Judgement.md`.

## Playfield model

The gameplay is presented as a large circle (the playfield) on the player's display. Small visuals (hit objects) appear on the **spawn halo** — a small circle concentric with the playfield — and move outward toward the outer circumference. The shape and color of a hit object represents a prompt that requests some action to be performed (see `../rules-specs/Inputs.md` for the physical mapping).

The playfield circle is divided at the center into a Cartesian grid rotated 45 degrees, such that it forms four quadrants with each quadrant opening towards a unique cardinal direction. A hit object's Angle is the direction from center along which it travels; the outer circumference serves as the judgement line, which the object reaches at its StartTime.

## Spawn halo and spawn phase

A hit object does not appear at the playfield centre. It appears on the **spawn halo**, a circle
concentric with the playfield whose radius is a fixed fraction of the playfield radius, at the
object's own Angle. It holds that position, motionless, while its spawn animation plays. The instant
that animation completes it begins travelling outward, reaching the ring at its StartTime.

Three parameters govern this:

| Parameter | Meaning |
| --- | --- |
| `TimeRange` | Sets radial velocity: one playfield radius per `TimeRange`. |
| `SpawnHaloFraction` | Halo radius as a fraction of the playfield radius. |
| `SpawnDuration` | How long an object holds on the halo — and how long its spawn animation runs. |

Writing `ScrollLength` for the playfield radius and `Δ` for the time remaining until an object's
StartTime, the derived quantities and the radius function are:

    haloRadius = ScrollLength × SpawnHaloFraction
    travelTime = TimeRange × (1 − SpawnHaloFraction)
    leadTime   = travelTime + SpawnDuration

    radius(Δ) = max(haloRadius, ScrollLength − Δ × ScrollLength / TimeRange)

An object appears at `Δ = leadTime` and holds at `haloRadius` until `Δ = travelTime`, where the floor
and the ramp meet without a seam. It reaches `ScrollLength` at `Δ = 0`. Radial velocity through the
travel phase is `ScrollLength / TimeRange`, independent of the halo.

The spawn animation's duration and the hold are the same quantity, so an object is never still
growing while it moves, and never fully grown while it is still.

The halo is not drawn. Objects simply appear at that radius.

An object with duration is governed point-by-point rather than as a whole, by its **emergence
front** — the point along it currently at `Δ = travelTime`, which is the point leaving the halo this
instant. Everything later than the front is still inside the hold window and maps to `haloRadius`;
it does not draw. The object therefore unfurls outward from its front rather than appearing at full
extent.

While every point of the object is still held — which is true of every object at the instant it
appears, whatever its duration — there is no emerged portion to draw and it renders as a stub on the
halo at its own Angle, exactly like a point note. A short object whose whole span fits inside the
hold window stays a stub for its entire spawn.

Because the radius function is flat before the front and linear after it, a durationed object is
only linear in time on its emerged side. Presentation that interpolates along it (the slider body's
polyline) must split at the front rather than interpolating across it, or the drawn shape bows away
from the radius map and the point drawn at the ring stops being the point whose time is now.

## Hit object presentation

### CardinalNote

A CardinalNote is presented as a generic shape with a square bounding box of dimensions less than 15% the size of the playfield circle. It moves in a direction corresponding to its Angle.

### ShoulderNote

A ShoulderNote is presented as an arc connecting the two points above and below the East and West cardinal lines (its Side, described as Left or Right). It moves out like an East or West CardinalNote, but the arc grows bigger as the points grow apart in vertical distance.

### CardinalHoldNote

A CardinalHoldNote is presented as a line extending out of a CardinalNote note towards the center of the circle. It follows the same path as its parent CardinalNote.

### ShoulderHoldNote

A ShoulderHoldNote is presented as a shoulder note (two squares on the ±45° quadrant diagonals joined by
an arc) with a transparent sector trailing inward toward the center of the circle. The sector spans the
same 90° slice as the shoulder head and fills the band between the head (at StartTime) and the tail (at
EndTime), shrinking to nothing as the tail reaches the ring.

### Slider

A slider is presented as a path extending outward from its head toward the edge, tracing its required angle over time; a multi-child slider curves as that angle interpolates between children.

When the stick crosses the radius threshold, a **catcher** is displayed: an arc slightly larger than the playfield circle, positioned at the stick's current angle, showing that the radius threshold has been met. It indicates where the player's edge input is currently aimed relative to the slider's required angle.

### Warning indicator

Because a stick object requires the player to pre-position the stick at the edge at a specific angle — which takes physical time, unlike a button tap — an approaching stick object may be telegraphed with a **warning indicator**: a blurred colored arc around the outside of the circle at the object's Side and angle. Its shape resembles the **catcher** described under Slider above, but it is rendered fully blurred, with the base arc shape not visible at all. The arc is colored by Side.

The indicator applies only to **SliderHead** objects (the *indicated objects*). Slams are deliberately excluded: a Slam is a sudden, precisely timed flick, so a "move the stick here in advance" cue works against the timing it demands.

The indicator for an indicated object *x* reveals itself when both of the following hold (see `../rules-specs/Inputs.md` for the stick-object terms these rules use):

- `x.StartTime - CurrentTime <= WarningTime` — *x* is within the warning window of reaching the edge (the lower bound is inclusive, matching the reveal window below).
- The gap between the previous same-side stick object and *x* is greater than `WarningTime`. This rule considers *stick objects* (any Slider, SlamCentered, or SlamEdge on the same Side), not only indicated objects. The gap is measured from the previous stick object's **end** (when the stick frees up) to *x*'s StartTime; when there is no earlier same-side stick object the gap is unbounded, so an isolated object always warns. This means a warning appears only when the stick has been idle on that Side, not when *x* follows closely on recent same-side activity that already has the player's stick engaged.

The reveal window is `[x.StartTime - WarningTime, x.StartTime)`: the indicator hides once *x* reaches the edge. 

`WarningTime` is a tunable parameter.

### SlamCentered

A SlamCentered is presented as an arrow shape similar in size to a CardinalNote. The arrow is rotated such that it is pointing along its angle.

### SlamEdge

*(Tentative — visual design TBD.)* A SlamEdge is presented as a marker at the edge at its Angle, with an indicator of the required rotational direction.
