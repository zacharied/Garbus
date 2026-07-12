# CardinalHoldNote rename + ShoulderHoldNote

## Goal

Two changes, in order:

1. **Rename** the existing `HoldNote` hit-object type (a held *cardinal* note) to `CardinalHoldNote`
   throughout the codebase, so the "hold" family is explicitly parameterised by which base note it
   holds.
2. **Add `ShoulderHoldNote`** — a held *shoulder* note — mirroring the `CardinalHoldNote` wiring end to
   end (object, drawables, editor blueprints/tool, lane routing, serialization, tests).

Backwards compatibility never matters in this project (no `.garbus` charts exist yet), so renames may
propagate into the serializer discriminator and file names freely, with no compatibility shim.

## Background

A `CardinalHoldNote` (currently `HoldNote`) is a `CardinalNote` with a `Duration`: a square head judged
like a cardinal note on the press, trailing a straight radial line that represents the hold, with the
tail judged by how much of `[StartTime, EndTime]` was held. Its angle is stored and mutable.

A `ShoulderNote` is the analog-shoulder counterpart of a cardinal note: it has a `Side` (Left/Right),
from which its `AngleDeg` (180° West / 0° East), `ButtonInput` (L/R), and lane `Direction` are *derived*
(the angle is deliberately **not** mutable). It is drawn as two purple squares on the ±45° quadrant
diagonals of its side, joined by a circular arc that grows as the note travels outward
(`ShoulderNoteGeometry` + `DrawableShoulderNote`), and is judged as a single timed press.

`ShoulderHoldNote` is to `ShoulderNote` what `CardinalHoldNote` is to `CardinalNote`: the same
two-square-plus-arc head, given a duration and a hold body, judged with the same head-plus-deferred-tail
algorithm.

Per `docs/presentation-specs/Presentation.md`, a held shoulder note "is presented as a line extending
out of a shoulder note towards the center of the circle. It follows the same path as its parent
ShoulderNote." Because the parent shoulder occupies a 90° angular slice (not a single ray), the hold
body here is a **transparent filled sector** spanning that slice — see the drawable below. (The
presentation doc's `HoldCardinalNote` / `HoldShoulderNote` section names are updated to match the class
names `CardinalHoldNote` / `ShoulderHoldNote`, and the `HoldShoulderNote` visual description is
corrected to the sector.)

## Part A — Rename `HoldNote` → `CardinalHoldNote`

A pure rename with no behavior change. Every reference in `Garbus.Game/` and `Garbus.Game.Tests/`
changes. Type/file renames:

| Old | New |
| --- | --- |
| `HoldNote` | `CardinalHoldNote` |
| `EditorDrawableHoldNote` | `EditorDrawableCardinalHoldNote` |
| `HoldNotePlacementBlueprint` | `CardinalHoldNotePlacementBlueprint` |
| `HoldNoteSelectionBlueprint` | `CardinalHoldNoteSelectionBlueprint` |
| `HoldNoteCompositionTool` | `CardinalHoldNoteCompositionTool` |
| `HoldNoteDto` | `CardinalHoldNoteDto` |
| serializer discriminator `"hold"` | `"cardinal-hold"` |

Three of the current concrete classes are instead **generalised into shared bases** (see "Shared hold
bases" below), so they are not simple renames:

- `HoldNoteHead` → concrete generic `HoldNoteHead<TParent>`; the cardinal head is
  `HoldNoteHead<CardinalHoldNote>` (no `CardinalHoldNoteHead` class).
- `DrawableHoldNoteHead` → concrete generic `DrawableHoldNoteHead<THead>`; used directly (no
  `DrawableCardinalHoldNoteHead` class).
- `DrawableHoldNote` → abstract base `DrawableHoldNote<THitObject, THead>` + a thin
  `DrawableCardinalHoldNote` subclass carrying only the cardinal visuals.

**Shared component exception — `HoldNoteEndDragPiece` → `HoldEndDragPiece`.** This is a generic
draggable duration handle with no cardinal-specific behavior, and the new `ShoulderHoldNote` selection
blueprint reuses it. It is renamed to the neutral `HoldEndDragPiece` (a shared component) rather than
tied to either note type.

Rename touch-points (from the codebase inventory):

- Objects: `Objects/HoldNote.cs` → `Objects/CardinalHoldNote.cs`; `Objects/HoldNoteHead.cs` →
  generic `Objects/HoldNoteHead.cs` (`HoldNoteHead<TParent>`, see shared bases).
- Drawables: `Objects/Drawables/DrawableHoldNote.cs` splits into the abstract base +
  `Objects/Drawables/DrawableCardinalHoldNote.cs`; `Objects/Drawables/DrawableHoldNoteHead.cs` becomes
  the generic head drawable. The nested-object plumbing moves into the base.
- Editor: `Edit/Drawables/EditorDrawableHoldNote.cs`,
  `Edit/Blueprints/HoldNotePlacementBlueprint.cs`, `Edit/Blueprints/HoldNoteSelectionBlueprint.cs`,
  `Edit/Blueprints/Components/HoldNoteEndDragPiece.cs`.
- Tools/registration: `Edit/Tools/GarbusCompositionTools.cs`, `Edit/GarbusHitObjectComposer.cs`
  (drawable `switch` + tools list), `Edit/GarbusBlueprintContainer.cs` (selection `switch`).
- Lane/gameplay: `UI/Ring.cs` (`laneFor`), `Screens/PlayScreen.cs` (`CreateDrawableRepresentation`).
- Serialization: `Charts/Format/ChartFileDto.cs` (`JsonDerivedType` + DTO class),
  `Charts/Format/GarbusChartSerializer.cs` (encode + decode switches).
- Data: `Charts/GarbusTestChartGenerator.cs`.
- Tests: `TestChartFormat.cs`, `Visual/TestSceneGameplay.cs`, `Editor/TestChecks.cs`,
  `Editor/TestEditorChart.cs`, `Editor/TestSceneComposePlacement.cs`,
  `Editor/TestSceneComposeSelection.cs`, `Editor/TestSceneEditorPlayfield.cs` (its own local drawable
  factory), `Editor/TestSceneEditorIntegration.cs`, `Editor/TestTimingSectionAdjustments.cs`.

Bundled test chart is regenerated (see Part B, testing).

## Shared hold bases (refactor, spans both hold types)

To avoid duplicating the hold judgement/input logic across the cardinal and shoulder hold drawables,
the current concrete `HoldNote` drawables are generalised first, then both concrete hold types plug in:

### `HoldNoteHead<TParent>` (concrete generic hit object)

`Objects/HoldNoteHead.cs` — replaces the old `HoldNoteHead`. A nested judgemental head that delegates
to its parent:

```
public class HoldNoteHead<TParent> : Note, IHasAngle where TParent : Note, IHasAngle
{
    public readonly TParent Parent;
    public HoldNoteHead(TParent parent) => Parent = parent;
    public int AngleDeg => Parent.AngleDeg;
    public override GarbusButtonInput ButtonInput => Parent.ButtonInput;
}
```

`CardinalHoldNote.Head` is a `HoldNoteHead<CardinalHoldNote>`; `ShoulderHoldNote.Head` a
`HoldNoteHead<ShoulderHoldNote>`. Both parents already implement `IHasAngle` (cardinal via
`IHasMutableAngle`, shoulder via its derived `AngleDeg`). No per-type head class.

### `DrawableHoldNoteHead<THead>` (concrete generic drawable)

`Objects/Drawables/DrawableHoldNoteHead.cs` — the current head drawable made generic over
`THead : Note`. Purely judgemental (`DisplayResult = false`, draws nothing, public `UpdateResult`,
auto-miss on window elapse). Instantiated directly by the base as `new DrawableHoldNoteHead<THead>(head)`.

### `DrawableHoldNote<THitObject, THead>` (abstract base drawable)

`Objects/Drawables/DrawableHoldNote.cs` — `DrawableNote<THitObject>, ISelfPosition`, with
`where THitObject : Note, IHasDuration` and `where THead : Note`. Owns everything that is not visual:

- **State:** `holdPresses`, `catchRecords`/`currentCatchRecord`, `headPopPlayed`, the resolved
  `GarbusScrollingHitObjectContainer`, and a `Container<DrawableHoldNoteHead<THead>>` with a `Head`
  accessor.
- **Input:** `OnPressed` (matches `HitObject.ButtonInput`, increments `holdPresses`, judges the head via
  `Head.UpdateResult()` on the first note-lock-permitted press), `OnReleased`, `MissForcefully` no-op.
- **Judgement:** `updateCatchRecords` over `[StartTime, EndTime]`; `CheckForResult` deferring the tail
  until the head is judged, folding the head grade into short holds (`headCarries` via
  `Head.HitObject.HitWindows.WindowFor(Miss)`); `resultFor` mapping caught-fraction to a graded result.
  All copied verbatim from today's `DrawableHoldNote` — behaviour-preserving.
- **Lifecycle:** `OnFree` reset; an `Update` loop that fires the head-pop hook once when `Head.IsHit`
  flips, then calls `UpdateVisuals()` and `updateCatchRecords()`.
- **Nested plumbing:** generic `CreateNestedHitObject`/`AddNestedHitObject`/`ClearNestedHitObjects`
  around `DrawableHoldNoteHead<THead>`.
- **Shared visual state exposed to subclasses:** protected accessors such as `Holding`
  (`holdPresses > 0`) and `HoldActive` (`Time.Current` within `[StartTime, EndTime]`) for the
  drop-greying, plus `Judged`.

**Abstract / virtual hooks subclasses implement:**

- `protected abstract void UpdateVisuals();` — position/build the head and body for the frame.
- `protected virtual void OnHeadHit() { }` — the head-hit pop (cardinal pops the head sprite; shoulder
  may pop its squares).
- `UpdateHitStateTransforms(ArmedState)` — per-type hit/miss transforms (already virtual).

Subclasses add their own visual children in the ctor / BDL.

### `DrawableCardinalHoldNote : DrawableHoldNote<CardinalHoldNote, HoldNoteHead<CardinalHoldNote>>`

`Objects/Drawables/DrawableCardinalHoldNote.cs` — the cardinal visuals only: the square `headSprite`
and the `SmoothPath` trailing radial line, `UpdateVisuals` positioning both in polar space (today's
`updateVisuals`), `OnHeadHit` popping the sprite, and the sprite/line hit/miss transforms. All the
judgement/input code that used to live here now comes from the base.

## Part B — Add `ShoulderHoldNote`

### Object: `ShoulderHoldNote`

`Objects/ShoulderHoldNote.cs`:

```
public class ShoulderHoldNote : Note, IHasCardinalDirection, IHasAngle, IHasDuration
{
    public required HorizontalDirection Side { get; set; }
    public double Duration { get; set; }
    public double EndTime => StartTime + Duration;

    public int AngleDeg => Side.ToAngleDeg();              // derived, NOT mutable (like ShoulderNote)
    public override GarbusButtonInput ButtonInput => Side switch { Left => ButtonL, Right => ButtonR };
    public CardinalDirection Direction => Side == Left ? West : East;

    public HoldNoteHead<ShoulderHoldNote> Head { get; private set; }
    protected override void CreateNestedHitObjects(...) => AddNested(Head = new HoldNoteHead<ShoulderHoldNote>(this) { StartTime = StartTime });
}
```

This is `ShoulderNote`'s side-derived members + `CardinalHoldNote`'s duration + the shared nested head.
It does **not** implement `IHasMutableAngle` (the angle follows `Side`).

### Gameplay drawable: `DrawableShoulderHoldNote`

`Objects/Drawables/DrawableShoulderHoldNote.cs` —
`DrawableHoldNote<ShoulderHoldNote, HoldNoteHead<ShoulderHoldNote>>` (the shared base). All hold
judgement/input comes from the base; this subclass supplies only the visuals via `UpdateVisuals`,
`OnHeadHit`, and `UpdateHitStateTransforms`.

**Visual — head** = two purple `"square"` sprites + a growing `Arc`, positioned each frame with
`ShoulderNoteGeometry.SquarePosition(AngleDeg, headRadius, ±1)` and arc radians `AngleDeg ± 45°`, where
`headRadius = clamp(DistanceFromCentreAtTime(StartTime), 0, ScrollLength)` — identical to
`DrawableShoulderNote`.

**Visual — trail** = a semi-transparent purple filled sector, drawn with the framework's
`CircularProgress` (a filled annular sector: `Progress` fraction of the full circle, `InnerRadius`
fraction giving the hole):

- `outer = clamp(DistanceFromCentreAtTime(StartTime), 0, ScrollLength)` (head radius),
  `inner = clamp(DistanceFromCentreAtTime(EndTime), 0, ScrollLength)` (tail radius, smaller because
  later).
- `Size = new Vector2(2·outer)`, anchored/origin centre on the playfield centre.
- `InnerRadius = outer > 0 ? inner / outer : 0` — the hole is the tail; the filled ring is the held
  band `[inner, outer]`.
- `Progress = 90/360 = 0.25` — the shoulder's 90° angular slice.
- `Rotation` set so the 0.25 wedge is centred on the side's cardinal **screen** angle. `CircularProgress`
  fills clockwise from its local up (12 o'clock); the playfield uses `θ=0` right, increasing CCW,
  screen-y down. A geometry helper computes the rotation from `AngleDeg`; a headless test pins that a
  right note's wedge is centred on the east screen direction and a left note's on the west, spanning
  ±45°.
- Alpha ~0.35 (transparent), colour purple. Greys toward the dropped colour while the base reports the
  hold active but not held (`HoldActive && !Holding`), matching `DrawableCardinalHoldNote`'s trail.
- Hidden (`Progress`/size collapse or `InnerRadius → 1`) once the tail reaches the ring (fully
  consumed), and near spawn.

All of this lives in the subclass's `UpdateVisuals` (head squares + arc + sector), reading the base's
`Holding`/`HoldActive`/`Judged` accessors for the drop-greying.

**Animations** mirror the shoulder note / cardinal hold: spawn pop (scale 0→1, 125 ms via `PrepareForUse`),
hit (fade + scale up, expire), miss (fade to red, expire) in `UpdateHitStateTransforms`, applied to head +
trail as a unit.

**Nested head** is the shared `DrawableHoldNoteHead<HoldNoteHead<ShoulderHoldNote>>`, created by the base's
generic plumbing — no per-type head drawable.

### Editor drawable: `EditorDrawableShoulderHoldNote`

`Edit/Drawables/EditorDrawableShoulderHoldNote.cs` —
`EditorDrawableGarbusHitObject<ShoulderHoldNote>`. Combines the two existing editor drawables:

- Timeline x from `ShoulderXFraction(Side)` (as `EditorDrawableShoulderNote`), no ghost twin.
- Translucent duration body growing over the duration + a head square with extended
  `ReceivePositionalInputAt` hit-testing (as `EditorDrawableCardinalHoldNote`).
- Nested-stub plumbing (`EditorDrawableNestedStub`) as the cardinal hold does.

### Editor placement: `ShoulderHoldNotePlacementBlueprint`

`Edit/Blueprints/ShoulderHoldNotePlacementBlueprint.cs` — the cardinal-hold placement's
**drag-to-stretch-duration** flow with the shoulder placement's **side selection**:

- Creates `new ShoulderHoldNote { Side = ... }` (no `AngleDeg` — it's derived).
- Side chosen by the nearer lane strip during the placement's begin phase (from
  `ShoulderNotePlacementBlueprint`).
- Mouse-down starts the object at the snapped time; drag stretches `Duration`; second click / release
  ends placement (from `CardinalHoldNotePlacementBlueprint`).
- `ReplacesExistingObject` (if applicable) only against a same-side shoulder-hold, mirroring the
  shoulder rule.

### Editor selection: `ShoulderHoldNoteSelectionBlueprint`

`Edit/Blueprints/ShoulderHoldNoteSelectionBlueprint.cs` — the cardinal-hold selection blueprint (two
`HoldEndDragPiece` handles retiming start/end; height tracks duration) with x positioned via
`ShoulderXFraction(Side)` and no ghost twin.

### Tool + registration

- `Edit/Tools/GarbusCompositionTools.cs`: add `ShoulderHoldNoteCompositionTool` (label "Shoulder Hold",
  a short icon), `CreatePlacementBlueprint => new ShoulderHoldNotePlacementBlueprint()`.
- `Edit/GarbusHitObjectComposer.cs`: add `ShoulderHoldNote → EditorDrawableShoulderHoldNote` to the
  drawable switch and `ShoulderHoldNoteCompositionTool` to the tools list.
- `Edit/GarbusBlueprintContainer.cs`: add `ShoulderHoldNote → ShoulderHoldNoteSelectionBlueprint`.
- `UI/Ring.cs` `laneFor`: `ShoulderHoldNote → shoulderLanes[shoulderIndex(Side)]`.
- `Screens/PlayScreen.cs` `CreateDrawableRepresentation`: `ShoulderHoldNote → DrawableShoulderHoldNote`.
- `Editor/TestSceneEditorPlayfield.cs` local factory: add the same editor-drawable case.

### Serialization

- `Charts/Format/ChartFileDto.cs`: `[JsonDerivedType(typeof(ShoulderHoldNoteDto), "shoulder-hold")]`
  and `ShoulderHoldNoteDto : HitObjectDto { string Side; double Duration; }`.
- `Charts/Format/GarbusChartSerializer.cs`: encode `ShoulderHoldNote → ShoulderHoldNoteDto { Side,
  Duration }`; decode `ShoulderHoldNoteDto → new ShoulderHoldNote { Side = parseEnum<HorizontalDirection>,
  Duration }`. Both switches otherwise throw on unknown types, so all four slots must be added.

### Polymorphic consumers (verify, no change expected)

`ShoulderHoldNote` implements `IHasDuration`, so these pick it up automatically and are verified rather
than edited: `Edit/Screens/Timing/TimingSectionAdjustments.cs` (scales `Duration`),
`Edit/Screens/Timeline/TimelineObjectMarkers.cs` (wider timeline bar),
`Edit/Screens/Verify/Checks/CheckObjectsBeyondTrackEnd.cs` (uses `EndTime`).

## Testing

- **Geometry:** a headless test on the trail sector rotation — a right `ShoulderHoldNote` wedge centred
  on east ±45°, a left one on west ±45° — analogous to `ShoulderNoteGeometryTest`. If the rotation is
  extracted into a pure helper (recommended), test the helper directly.
- **Serialization:** extend `TestChartFormat` roundtrip with a `ShoulderHoldNote` case (asserting `Side`
  and `Duration`), mirroring the shoulder + cardinal-hold cases.
- **Gameplay:** extend `Visual/TestSceneGameplay` with a `ShoulderHoldNote` judgement case (auto-miss +
  key-press hit through the head, held-tail grading), mirroring the cardinal-hold `shortHold`/playThrough
  helpers. Remember the manual-clock gotcha: step in sub-window increments.
- **Editor placement/selection:** extend `TestSceneComposePlacement` (drag-to-stretch a shoulder hold,
  side picked by nearer lane) and `TestSceneComposeSelection` (selectable by head; end-drag retiming),
  mirroring the cardinal-hold and shoulder tests.
- **Bundled chart:** add a `ShoulderHoldNote` to `GarbusTestChartGenerator` and regenerate
  `Garbus.Resources/Charts/test-chart.garbus` via the `[Explicit]`
  `TestChartFormat.RegenerateBundledTestChart`.
- Full headless suite green: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.

## Non-goals

- No change to judgement timing, hit windows, input mapping, or the cardinal-hold behaviour (the rename
  is behaviour-preserving).
- The shared hold bases (`DrawableHoldNote<,>`, `DrawableHoldNoteHead<>`, `HoldNoteHead<>`) are the only
  new abstraction; the two hold **hit objects** (`CardinalHoldNote`/`ShoulderHoldNote`) stay separate
  because they differ in angle mutability. `ShoulderNoteGeometry` and `HoldEndDragPiece` remain shared
  helpers.
- No warning-indicator / catcher interaction for shoulder holds.
