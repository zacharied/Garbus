*The contents of this document differ from the current implementation.*

# Inputs specification

This is the canonical reference for the player input behavior corresponding to every Garbus hit object. 

## Physical model

The game is played using a symmetrical handheld game controller that is held by the player. The player's right thumb
rests on the "face buttons" and the left thumb rests on the "d-pad" (directional pad). These button surfaces are known as "cardinal buttons" and each consist
of four symmetrical buttons that each point towards a cardinal direction.

The player's thumbs may move down
to the "analog sticks" that sit inwards from and below the button surfaces. The analog sticks are levers in a circular
well that output their current position as X-Y coordinates. The stick snaps back to the center (0, 0) when not touched
by the player. The bounds to which the stick may be pushed by the player is restricted to a circle with a fixed radius
in the stick's coordinate space.

The player's remaining fingers (primarily the index finger on each hand) each press a sided "shoulder button" that sits in line with the
curvature of their index finger.

> **D-pad clause.** Due to physical limitations of the controller, only three buttons of the D-pad can be pressed at once.

## Game model

The gameplay is presented as a large circle (the playfield) on the player's display.
Small visuals (hit objects) emerge from the center of the circle. The shape
and color of the hit object represents a prompt that requests some action to be performed on the physical model. The outer
circumference of the circle serves as a "judgement line", such that at the moment a hit object contacts it, the user
must perform the prescribed action. See `Judgement.md` for more information. 
Hit objects move in any direction towards the outer circumference of the circle, with the angle of movement specifying conditions on
what must be performed physically to be deemed a successful input in the game's ruleset.
Every hit object has a StartTime; this is the gameplay clock time at which the input must be performed, and is also the time at which the object collides with the playfield circle's circumference.
The playfield circle is divided at the center into a Cartesian grid rotated 45 degrees, such that it forms four quadrants with each quadrant opening towards a unique cardinal direction.

This document will describe the mapping between the physical and game model. For how each hit object is presented visually, see `../presentation-specs/Presentation.md`.

## Analog stick inputs

Sliders and Slams are performed with an analog stick rather than a button. This section defines the terms they share; each object type below specifies the gesture it requires. The timing of these objects is defined in `Judgement.md`.

- **Stick angle** — the direction from the well's center to the stick's current position.
- **Stick radius** — the distance of the stick from center, from 0 (centered) to the well's fixed maximum.
- **Radius threshold** — a fixed radius near the maximum. The stick is **at the edge** when its radius is at or beyond this threshold.
- **Angle tolerance** — an object with an Angle is aligned when the stick angle is within this fixed tolerance of that Angle.
- **Side** — each hand's stick is an independent input; a stick object's Side selects which stick performs it.

The radius threshold and angle tolerance are tunable parameters.

## Hit object types

Hit objects are classified into discrete or composite. Discrete hit objects are a prompt to perform an action at a specific time. Composite hit objects are composed of two discrete hit objects: a **head** with a StartTime, and a **tail** with an EndTime, and thus prompt an action that must be maintained over a period of time.

### CardinalNote

A discrete object with a Direction, input by pressing one of the two cardinal buttons that correspond to its direction.

The playfield circle is divided at the center into a Cartesian grid rotated 45 degrees, such that it forms four quadrants with each quadrant opening towards a unique cardinal direction. The CardinalNote's Direction is equal to the cardinal direction corresponding to the quadrant in which its Angle resides.

Simultaneous CardinalNotes (that is, a set of CardinalNotes with equal StartTimes) are permitted.

Because there are two buttons per direction, a group of simultaneous CardinalNotes max out at two per direction. Across all directions, the input set supports eight simultaneous CardinalNotes, but due to the d-pad clause, the cap is actually seven.

### ShoulderNote

A discrete object with a Side, input by pressing the shoulder button on the corresponding side of the controller.

A group of simultaneous ShoulderNotes can consist of at most two notes; one on each side.

### HoldCardinalNote

The composite variant of CardinalNote, input by keeping pressed one of the two cardinal buttons corresponding to the direction.

Because the HoldCardinalNote simply checks that a button of its direction is pressed, one button can be used to activate an arbitrary number of HoldCardinalNotes.

If a HoldCardinalNote is currently being held, additional CardinalNotes or other HoldCardinalNotes can be pressed by the unpressed direction button.

### HoldShoulderNote

The composite variant of ShoulderNote, input by keeping pressed the shoulder button that corresponds to its side.

Unlike a Cardinal direction, a Side has only one button, so it cannot be pressed for a new ShoulderNote while a HoldShoulderNote on that side is held. Charts must not place a same-side ShoulderNote or HoldShoulderNote during a held HoldShoulderNote.

### Slider

A slider is a composite object with a Side and Angle, input by moving the analog stick of the slider's side to the edge of its circular well at an angle corresponding to the slider's angle.

A slider is correctly performed at a given instant when its side's stick is at the edge and its stick angle is within tolerance of the slider's required angle at that instant. This is the condition the Judgement spec calls the input being "correctly active". The slider's head fixes the required angle at its StartTime; between consecutive children the required angle is **linearly interpolated**, so a curved body sweeps the required angle smoothly over the slider's duration.

Because a stick object occupies its side's stick for its whole duration, simultaneous same-side stick operations require consideration. 

1. A same-side SlamCentered with similar angle to a slider's head node is permissible, as the SlamCentered input (moving the stick from centered towards edge) and the slider's input (moving the stick to the edge and keeping it there) are not mutually exclusive.
2. Likewise, a same-side SlamCentered can point *away* from the final child node of a slider, as the slider release and the slam have overlapping inputs.
3. A same-side SlamEdge may be placed along a slider path that is moving in the same direction as the SlamEdge.
4. Multiple same-side sliders may active at the same time, but it is disallowed for the two sliders to be radially further apart than than the catcher's radial tolerance.

### SlamCentered

A discrete object with a Side and Angle, input by flicking the analog stick of its side outward from center toward its angle.

The flick is detected as a rapid outward motion of the stick — its radius rising past the threshold — with its stick angle within tolerance of the slam's Angle.

### SlamEdge

A discrete object with a Side, Angle, and rotational Direction (clockwise or counter-clockwise), input by sweeping the analog stick — already at the edge — through its Angle in that rotational direction.

The input is detected when the stick, at or beyond the radius threshold, rotates so its stick angle passes through the slam's Angle while moving in the specified rotational direction. Like SlamCentered, it is early-permissive (see `Judgement.md`).