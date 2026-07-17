# Cardinal chord highlight + connector

## Goal

When two or more cardinal-directed notes share the same start time (a "chord"), highlight them so the
coincidence is obvious:

- **Coloring (editor + gameplay):** every member of a chord is drawn **yellow** instead of white.
- **Connector (gameplay only):** the members of a chord are joined by a thin, semi-transparent yellow
  connector — a straight-edged polygon inscribed at their shared radius. The connector is purely
  cosmetic. It is **not** drawn in the editor.

## Definitions and locked decisions

- **Chord membership:** cardinal-*directed* notes — `CardinalNote` **and** `CardinalHoldNote` — whose
  `StartTime` values are **exactly** equal, in a group of **size ≥ 2**. `ShoulderNote`,
  `ShoulderHoldNote`, sliders, and slams are excluded (they are not routed to the cardinal lanes and are
  not part of this feature). A `CardinalNote` and a `CardinalHoldNote` at the same StartTime *do* form a
  chord together.
- **Match rule:** exact `StartTime` equality. `StartTime` is a `double` (ms), but two notes authored on
  the same beat run through the same beat-snap computation and land on a bit-identical value, so exact
  equality is the right match and no tolerance window is needed.
- **Group size:** any size `n ≥ 2`. Do **not** assume a 4-direction cap: `AngleDeg` is a free integer
  (`IHasMutableAngle`) and `Direction` is only derived from it for lane routing, so a chord can hold many
  members at arbitrary angles (7+ is expected). The grouping, coloring, and connector must all work for
  arbitrary `n`.
- **Connector shape:** a single closed polygon connecting the members in **angular order** around the
  ring — 2 → a straight segment, 3 → a triangle, and in general an `n`-gon. Never per-pair "mesh", never
  an arc. Because all members share a StartTime they are **co-radial** (same distance from centre at any
  instant), so the polygon is inscribed in the circle of the current radius; its vertices are the
  members' angles (sorted) and it grows outward as the notes travel. Sorting by angle guarantees a
  simple, non-self-intersecting polygon for any `n`.
- **Connector style (defaults, tweakable in code):** colour yellow, line thickness ~2px, alpha ~0.35.
- **Placement:** `ChordIndex` lives in `Garbus.Game/Objects/`. Gameplay builds and caches one instance
  via DI; the editor rebuilds it on chart mutation.

## Architecture (Approach 1: shared index + Ring-level overlay)

Three units with clean boundaries:

### 1. `ChordIndex` — pure grouping (no drawing, no framework types)

New plain class in `Garbus.Game/Objects/ChordIndex.cs`.

- **Input:** a sequence of `HitObject`s (the chart's hit objects).
- **Behaviour:** bucket the cardinal-directed notes (those that are `CardinalNote` or
  `CardinalHoldNote`) by `StartTime`; keep only buckets with ≥ 2 members.
- **Exposes:**
  - `bool IsInChord(HitObject note)` — true if that note is a member of a kept bucket.
  - An enumeration of groups, where each group exposes its members and each member's angle
    (`AngleDeg`) and start time. Enough for the connector overlay to build a polygon and for tests to
    assert grouping.
- **Not responsible for:** any colour, any drawable, any per-frame position — purely the set-membership
  question. This keeps it unit-testable in isolation.

What it does / how it's used / what it depends on: it answers "which notes are chord members and what
are the groups", callers pass in the hit-object list, and it depends only on the hit-object model.

### 2. Coloring (editor + gameplay)

Shared rule: a cardinal drawable is **yellow when its note `IsInChord`**, default **white** otherwise.

**Gameplay** — the chart is static during play (F5/Test deep-clones the WIP chart; normal play loads a
fixed chart):
- A `ChordIndex` is built once from the playfield's chart and cached (DI) so drawables can resolve it.
- `DrawableCardinalNote` and `DrawableCardinalHoldNote` set their tint in `PrepareForUse` (the
  pooled-reuse hook): yellow if `IsInChord`, otherwise reset to white (pooled drawables must reset, or a
  reused instance keeps a stale yellow).
- The tint applies to the **whole note**: the `CardinalNote` head sprite, and for `CardinalHoldNote`
  **both the head sprite and the hold body**. The body's held/dropped grey/white state and the miss
  red-fade still run as transforms on top; the chord tint is the base colour they modulate. (Setting the
  chord tint at the drawable `Colour` level, above the sprite/body pieces, is the simplest way to cover
  head + body at once while leaving the piece-level transforms intact.)
- The existing miss transform (`FadeColour(Red)`) still runs and overrides the tint during the miss
  animation. That is acceptable — miss is a fail state.

**Editor** — the chart is mutable (notes placed, moved, deleted; StartTime changes via drag/inspector):
- The `ChordIndex` is rebuilt whenever the chart changes. The composer already observes
  add/remove/update of hit objects (the `EditorChart`/composer add/remove/update seams described in
  CLAUDE.md); on any such change, rebuild the index and refresh the colour of the affected cardinal
  editor drawables (`EditorDrawableCardinalNote`, `EditorDrawableCardinalHoldNote`).
- Colour is set at the drawable `Colour` level (not on an inner piece) so the ±360° ghost twin created
  by `EditorDrawableGarbusHitObject.CreateVisual` inherits the tint automatically.
- No connector in the editor.

### 3. `ChordConnectorOverlay` — gameplay only

A new drawable (`Garbus.Game/UI/ChordConnectorOverlay.cs`) added to `Ring`, drawn **below all hit
objects** — behind both the ring's own `HitObjectContainer` (paths) and the lane container, so the notes
always render on top of the connector. In `Ring`'s child list it sits ahead of `HitObjectContainer`
(e.g. just after `PlayfieldRadialLines`). All cardinal lanes share the same polar centre and full size,
so member positions are directly comparable across lanes; a connector spanning lanes must live in the
ring, not inside a lane. **Not** a hit object — a single overlay, so no synthetic objects enter the
chart / serializer / scoring / editor.

**Geometry is derived from chord data, never from live note positions.** Because all members of a chord
share a StartTime they are **co-radial**: the shared radius is `ring.ProgressAtTime(StartTime)` and each
vertex is `polar(memberAngle, radius)`. This is computed from the `ChordIndex` group's static
angle+time data, so the connector keeps its **full shape (all original vertices)** regardless of which
members have already despawned during scroll-in — dissolving the "a note despawns before reaching the
ring" concern.

**Full-opacity visibility is presence-driven, checked each frame:** the group's polygon is held at full
opacity **iff at least one of its members is represented by a drawable that is both alive AND still
unjudged (`ArmedState.Idle`)** in its cardinal lane. The instant the chord reaches the ring and is judged
(Hit/Miss), the polygon is **frozen at its last shape and fades out in place** over `CONNECTOR_FADE_OUT`
(200 ms). It does **not** keep tracking `ProgressAtTime` through the note's post-judgement fade-out (which
would balloon the polygon out past the ring). The connector therefore appears when the first member spawns
and fades away just after the chord is judged, never lingering through the full note fade.

Presence transitions drive the fade with no subscriptions or counters — a per-start-time `shownStartTimes`
set fires the transform exactly once per edge:
- **not-present → present** (first spawn, or a rewind un-judging the chord): cancel any pending fade and
  snap to full opacity.
- **present → judged/despawned**: leave the frozen geometry in place and `FadeOut`.

Each frame, for every chord group:
- Determine presence: is any member's `HitObject` an alive, unjudged drawable in a cardinal lane?
- If present, compute `radius = ring.ProgressAtTime(StartTime)` and draw one thin, semi-transparent yellow
  polygon whose vertices are `polar(memberAngle, radius)` for **every** member of the group, ordered by
  angle and closed into a loop (a 2-vertex "loop" is just the single segment). The polygon grows outward
  as the radius grows while scrolling in.
- If not present, the polygon is left frozen at its last shape and its fade-out (started on the transition)
  plays out.

Depends on: the `ChordIndex` (which groups exist and their member angles/StartTime), the ring's
`GarbusScrollingHitObjectContainer` (shared radius via `ProgressAtTime`), and read-only access to the
cardinal lanes' alive objects (presence check).

## Data flow

```
chart hit objects ──► ChordIndex (buckets by StartTime, size ≥ 2)
                          │
        ┌─────────────────┼──────────────────────────┐
        ▼                 ▼                           ▼
  gameplay drawables  editor drawables         ChordConnectorOverlay (gameplay)
  tint whole note     recolor on chart          per-frame: if any member alive & unjudged →
  on PrepareForUse    change                    radius = ProgressAtTime(StartTime),
                                                 vertices = all member angles → polygon
```

## Edge cases

- **Single note at a time:** not in any bucket → white, no connector. (Unchanged behaviour.)
- **3+ coincident (arbitrary `n`):** all yellow; connector is an `n`-gon in angular order (7+ members
  supported).
- **Stacked same angle** (e.g. a `CardinalNote` and `CardinalHoldNote` at the same time *and* same
  angle): both yellow; the polygon has a zero-length edge there — degenerate and invisible, no special
  handling needed.
- **Members hit/missed independently / at different times:** the connector keeps its full shape (all
  original vertices, computed from chord data) at full opacity for as long as **any** member is still
  alive AND unjudged, then freezes and fades out over 200 ms once the last unjudged member is judged (or
  despawns). It never degrades to a partial polygon mid-chord, and it does not linger at full opacity
  through the post-judgement fade-out. Coloring on each member is unchanged (miss fades that member red).
- **A member despawns before the ring during scroll-in:** handled — geometry is from `ProgressAtTime`,
  not the departed drawable, so its vertex stays as long as another member is still alive and unjudged.
- **Editor live edits:** moving a note onto another's time colours both; moving it off returns the
  loner to white. Rebuild-on-change covers add, remove, and StartTime edits.
- **Pooled reuse (gameplay):** `PrepareForUse` must set the tint explicitly to yellow *or* white so a
  recycled drawable never inherits the previous object's colour.

## Testing

- **`ChordIndex` (headless, pure):**
  - Two cardinal notes at the same StartTime → both `IsInChord`; one group of 2.
  - A `CardinalNote` + `CardinalHoldNote` at the same StartTime → grouped together.
  - A single cardinal note → not in a chord.
  - Arbitrary `n` coincident (e.g. 3 and 7, at arbitrary angles) → one group of the right size.
  - A `ShoulderNote`/slider at the same StartTime as a cardinal note → excluded (does not join the
    group, does not itself become a member).
  - Distinct StartTimes → no group.
- **Editor (headless visual scene):**
  - Placing/moving two cardinal notes onto the same time colours both yellow; moving one away returns it
    to white. No connector drawable appears in the editor.
- **Gameplay (headless visual scene, manual clock):**
  - A coincident pair renders yellow (head + hold body for a hold) and the connector overlay produces a
    segment between them at the expected radius (`ProgressAtTime(StartTime)`).
  - The connector keeps its full shape at full opacity while at least one member is alive and unjudged,
    including after another member has despawned during scroll-in. Once the chord is judged it fades out
    (still present but below full opacity a little into the fade) and is fully gone well before the notes'
    1000 ms miss fade ends — proving it fades rather than snapping or lingering.

## Non-goals / YAGNI

- No tolerance/near-miss matching.
- No connector in the editor (explicitly excluded).
- No per-pair mesh; no arc connector.
- No new serialized chart fields — chord state is derived at runtime, never persisted.
- No difficulty/scoring impact — purely visual.
