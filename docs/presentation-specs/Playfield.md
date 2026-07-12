*The contents of this document differ from the current implementation.*

# Playfield presentation specification

This is the canonical reference for how every Garbus hit object is presented visually. It describes the shape, motion, and on-screen feedback of each object type. For the input behavior these visuals prompt, see `../rules-specs/Inputs.md`; for timing and judgement, see `../rules-specs/Judgement.md`.

## Playfield model

The gameplay is presented as a large circle (the playfield) on the player's display. Small visuals (hit objects) emerge from the center of the circle and move toward the outer circumference. The shape and color of a hit object represents a prompt that requests some action to be performed (see `../rules-specs/Inputs.md` for the physical mapping).

The playfield circle is divided at the center into a Cartesian grid rotated 45 degrees, such that it forms four quadrants with each quadrant opening towards a unique cardinal direction. A hit object's Angle is the direction from center along which it travels; the outer circumference serves as the judgement line, which the object reaches at its StartTime.

## Hit object presentation

### CardinalNote

A CardinalNote is presented as a generic shape with a square bounding box of dimensions less than 15% the size of the playfield circle. It moves in a direction corresponding to its Angle.

### ShoulderNote

A ShoulderNote is presented as an arc connecting the two points above and below the East and West cardinal lines (its Side, described as Left or Right). It moves out like an East or West CardinalNote, but the arc grows bigger as the points grow apart in vertical distance.

### HoldCardinalNote

A HoldCardinalNote is presented as a line extending out of a CardinalNote note towards the center of the circle. It follows the same path as its parent CardinalNote.

### HoldShoulderNote

A HoldShoulderNote is presented as a circle sector extending out of a shoulder note towards the center of the circle. It follows the same path as its parent ShoulderNote.

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
