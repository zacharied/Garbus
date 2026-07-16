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
- **Match rule:** exact `StartTime` equality. No tolerance window (chart times are integer ms).
- **Group size:** any size 2–4 (up to N/E/S/W). Both the coloring and the connector handle 3 and 4.
- **Connector shape:** a single closed polygon connecting the members in **angular order** around the
  ring — 2 members → a straight segment, 3 → a triangle, 4 → a quadrilateral. Never per-pair "mesh",
  never an arc. Because all members share a StartTime they are **co-radial** (same distance from centre
  at any instant), so the polygon is inscribed in the circle of the current radius; its vertices are the
  members' angles and it grows outward as the notes travel.
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
  reused instance keeps a stale yellow). Tint is applied to the head sprite (and, for the hold, is
  independent of the body's held/dropped colour logic — the chord tint sits on the head; body colour
  behaviour is unchanged).
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

A new drawable (e.g. `Garbus.Game/UI/ChordConnectorOverlay.cs`) added to `Ring`, drawn **above the
lanes** (all four cardinal lanes share the same polar centre and full size, so positions are directly
comparable across lanes; a connector spanning lanes must live above them, in the ring).

Each frame:
- For every chord group, gather the members that are currently **present** — alive drawables that have
  not yet been hit/missed (once judged they animate out and should leave the connector).
- If fewer than 2 members remain present, draw nothing for that group.
- Otherwise all present members are co-radial: take the current radius from the ring's scrolling
  container (`ProgressAtTime(StartTime)`), and each member's angle. Draw one thin, semi-transparent
  yellow polygon whose vertices are `polar(angle, radius)` for each member, ordered by angle, closed
  into a loop (a 2-vertex "loop" is just the single segment).
- The polygon updates every frame, so it grows outward with the notes and shrinks/vanishes as members
  are consumed.

Depends on: the `ChordIndex` (which groups exist), the ring's `GarbusScrollingHitObjectContainer` (for
the shared radius via `ProgressAtTime`), and the ability to tell which members are still present
(query the alive/judged state of the corresponding drawables, or track spawned drawables).

## Data flow

```
chart hit objects ──► ChordIndex (buckets by StartTime, size ≥ 2)
                          │
        ┌─────────────────┼──────────────────────────┐
        ▼                 ▼                           ▼
  gameplay drawables  editor drawables         ChordConnectorOverlay (gameplay)
  tint yellow on      recolor on chart          per-frame: radius = ProgressAtTime,
  PrepareForUse       change                    vertices = member angles → polygon
```

## Edge cases

- **Single note at a time:** not in any bucket → white, no connector. (Unchanged behaviour.)
- **3 or 4 coincident:** all yellow; connector is a triangle/quad in angular order.
- **Stacked same angle** (e.g. a `CardinalNote` and `CardinalHoldNote` at the same time *and* same
  angle): both yellow; the polygon has a zero-length edge there — degenerate and invisible, no special
  handling needed.
- **Members hit/missed independently:** as each leaves, the connector re-forms among the rest and
  disappears below 2. Coloring on the remaining members is unchanged (miss fades them red as today).
- **Editor live edits:** moving a note onto another's time colours both; moving it off returns the
  loner to white. Rebuild-on-change covers add, remove, and StartTime edits.
- **Pooled reuse (gameplay):** `PrepareForUse` must set the tint explicitly to yellow *or* white so a
  recycled drawable never inherits the previous object's colour.

## Testing

- **`ChordIndex` (headless, pure):**
  - Two cardinal notes at the same StartTime → both `IsInChord`; one group of 2.
  - A `CardinalNote` + `CardinalHoldNote` at the same StartTime → grouped together.
  - A single cardinal note → not in a chord.
  - 3 and 4 coincident → one group of the right size.
  - A `ShoulderNote`/slider at the same StartTime as a cardinal note → excluded (does not join the
    group, does not itself become a member).
  - Distinct StartTimes → no group.
- **Editor (headless visual scene):**
  - Placing/moving two cardinal notes onto the same time colours both yellow; moving one away returns it
    to white. No connector drawable appears in the editor.
- **Gameplay (headless visual scene, manual clock):**
  - A coincident pair renders yellow and the connector overlay produces a segment between them at the
    expected radius; auto-miss/hit of a member drops it from the connector and the connector clears
    below 2 present members.

## Non-goals / YAGNI

- No tolerance/near-miss matching.
- No connector in the editor (explicitly excluded).
- No per-pair mesh; no arc connector.
- No new serialized chart fields — chord state is derived at runtime, never persisted.
- No difficulty/scoring impact — purely visual.
