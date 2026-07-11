*The contents of this document differ from the current implementation.*

# Presentation specification

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

A HoldShoulderNote is presented as a line extending out of a shoulder note towards the center of the circle. It follows the same path as its parent ShoulderNote.

### Slider

A slider is presented as a path extending outward from its head toward the edge, tracing its required angle over time; a multi-child slider curves as that angle interpolates between children.

When the stick crosses the radius threshold, a **catcher** is displayed: an arc slightly larger than the playfield circle, positioned at the stick's current angle, showing that the radius threshold has been met. It indicates where the player's edge input is currently aimed relative to the slider's required angle.

### SlamCentered

A SlamCentered is presented as an arrow shape similar in size to a CardinalNote. The arrow is rotated such that it is pointing along its angle.

### SlamEdge

*(Tentative — visual design TBD.)* A SlamEdge is presented as a marker at the edge at its Angle, with an indicator of the required rotational direction.
