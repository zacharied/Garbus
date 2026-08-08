# Shoulder note spawn tween — design

## Problem

Cardinal notes spawn on the halo by scaling their square sprite from 0 to full about the sprite's
own centre, so the note grows *in place* over the motionless spawn hold. Shoulder notes are supposed
to read as the same family, but their spawn animation scales the **whole drawable** about the
playfield centre:

```csharp
// DrawableShoulderNote.UpdateInitialTransforms (and DrawableShoulderHoldNote)
this.ScaleTo(0).ScaleTo(1, SpawnAnimationDuration, Easing.In);
```

The drawable fills the playfield (centre origin) and positions its two squares at `radius` from the
centre each frame. Scaling the whole thing from 0 therefore makes the two squares **slide outward
from the playfield centre** while the arc balloons out of a point — nothing like a cardinal note
growing in place on the halo.

## Desired behaviour

- Each of the two square ends grows **in place** on the halo, exactly like a cardinal note (scale
  0 → 1 about the square's own centre, `Easing.In`, over `SpawnAnimationDuration`).
- The arc that joins the two squares grows **out of each square toward the midpoint** between them
  (the note's cardinal angle). The two growing halves **touch exactly when the square scale reaches
  full** — i.e. when the cardinal-style tween finishes.

Applies to both the tap `ShoulderNote` and the `ShoulderHoldNote` head (identical two-square + arc
visual). The hold note's sector body keeps its existing spawn behaviour unless it reads wrong.

## Approach

Both shoulder drawables already self-position all their children every frame (`Update` /
`UpdateVisuals`). The spawn animation joins that per-frame model instead of living in the transform
system, which also makes it replay correctly on rewind/scrub for free.

### Spawn progress

```
spawnProgress = clamp((Time.Current - LifetimeStart) / SpawnAnimationDuration, 0, 1)
eased         = Interpolation.ApplyEasing(Easing.In, spawnProgress)
```

`LifetimeStart` equals the halo-spawn time (`StartTime − LeadTime`) because
`InitialLifetimeOffset` is overridden to `LeadTime`. `SpawnAnimationDuration` is the existing
protected accessor for the motionless-hold duration. Using one `eased` value for both the squares
and the arc halves guarantees they finish together.

### Squares grow in place

- Drop the whole-drawable `ScaleTo` (remove the `UpdateInitialTransforms` override on both
  drawables; nothing else relies on it — the fade/scale hit-state transforms stay on the whole
  drawable and still work, since the drawable now sits at scale 1 throughout the spawn).
- Set `squareA.Scale = squareB.Scale = new Vector2(eased)` each frame. Squares have centre origin,
  so this grows them in place at their halo positions.

### Arc grows from both ends toward the middle

A single contiguous `Arc` cannot represent two segments with a gap between them, so the single arc
is replaced by **two half-arcs**, each with its own `Arc`:

- **Half A** anchored at square A (`base + 45°`): outer end fixed at `base + 45°`, inner end sweeps
  from `base + 45°` toward `base` as `eased` goes 0 → 1
  (`innerA = base + 45° − 45°·eased`).
- **Half B** anchored at square B (`base − 45°`): inner end sweeps from `base − 45°` toward `base`
  (`innerB = base − 45° + 45°·eased`).

At `eased = 1` both inner ends land on `base` and meet, reproducing today's full ±45° arc. Their
rounded end caps overlap at the join, so it reads as one continuous arc. At `eased = 0` both spans
are zero, so `Arc.regeneratePath`'s near-zero-span guard draws nothing (squares are also scale 0).

Both half-arcs keep the current `Size = 2·radius` sizing, so during the motionless hold they sit at
the constant halo radius and only their angular span animates.

The hold note applies the same head treatment inside `UpdateVisuals`; its sector body math is
unchanged.

## Test impact

`TestSceneSpawnTween` asserts on `drawable.Scale.X`, relying on the whole-drawable scale that this
change removes. Repoint it at a small test seam that exposes the eased spawn progress on the
shoulder drawable (a `SpawnProgress` accessor), preserving the existing duration / motion-coupling
guarantees:

- starts at 0 at the halo-spawn instant,
- grows partway mid-hold,
- reaches 1 exactly when motion begins,
- holds across the two spawn-duration cases (100 ms / 300 ms).

Add coverage that the arc halves are collapsed (zero span) at spawn start and meet at `base`
(full ±45° coverage, matched inner ends) at motion start.

## Out of scope

- The hold note's sector-body spawn behaviour.
- Any change to steady-state (post-spawn) geometry — the final frame is pixel-identical to today.
