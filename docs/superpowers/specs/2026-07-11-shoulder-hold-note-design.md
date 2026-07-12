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
| `HoldNoteHead` | `CardinalHoldNoteHead` |
| `DrawableHoldNote` | `DrawableCardinalHoldNote` |
| `DrawableHoldNoteHead` | `DrawableCardinalHoldNoteHead` |
| `EditorDrawableHoldNote` | `EditorDrawableCardinalHoldNote` |
| `HoldNotePlacementBlueprint` | `CardinalHoldNotePlacementBlueprint` |
| `HoldNoteSelectionBlueprint` | `CardinalHoldNoteSelectionBlueprint` |
| `HoldNoteCompositionTool` | `CardinalHoldNoteCompositionTool` |
| `HoldNoteDto` | `CardinalHoldNoteDto` |
| serializer discriminator `"hold"` | `"cardinal-hold"` |

**Shared component exception — `HoldNoteEndDragPiece` → `HoldEndDragPiece`.** This is a generic
draggable duration handle with no cardinal-specific behavior, and the new `ShoulderHoldNote` selection
blueprint reuses it. It is renamed to the neutral `HoldEndDragPiece` (a shared component) rather than
tied to either note type.

Rename touch-points (from the codebase inventory):

- Objects: `Objects/HoldNote.cs`, `Objects/HoldNoteHead.cs`.
- Drawables: `Objects/Drawables/DrawableHoldNote.cs`, `Objects/Drawables/DrawableHoldNoteHead.cs`
  (including the nested-object `switch` in `DrawableCardinalHoldNote.CreateNestedHitObject`).
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

    public ShoulderHoldNoteHead Head { get; private set; }
    protected override void CreateNestedHitObjects(...) => AddNested(Head = new ShoulderHoldNoteHead(this) { StartTime = StartTime });
}
```

This is `ShoulderNote`'s side-derived members + `CardinalHoldNote`'s duration + nested head. It does
**not** implement `IHasMutableAngle` (the angle follows `Side`).

### Object: `ShoulderHoldNoteHead`

`Objects/ShoulderHoldNoteHead.cs` — mirrors `CardinalHoldNoteHead`: `: Note, IHasAngle`, holds a
`readonly ShoulderHoldNote Parent`, delegates `AngleDeg => Parent.AngleDeg` and
`ButtonInput => Parent.ButtonInput`.

### Gameplay drawable: `DrawableShoulderHoldNote`

`Objects/Drawables/DrawableShoulderHoldNote.cs` —
`DrawableNote<ShoulderHoldNote>, ISelfPosition`. It combines the shoulder-note head visual with the
cardinal-hold judgement.

**Judgement (copied from `DrawableCardinalHoldNote`, unchanged in substance):** nested
`DrawableShoulderHoldNoteHead` judged on press via `Head.UpdateResult()`; `holdPresses` count via
`OnPressed`/`OnReleased`; `catchRecords` accumulate held time over `[StartTime, EndTime]`;
`CheckForResult` defers the tail until the head is judged and folds the head grade into short holds;
`MissForcefully` is a no-op. This logic is duplicated (the two hold drawables do not share a base beyond
`DrawableNote<T>`), matching the existing mirror-not-inherit pattern; the only differences are the
visual pieces and the button-match (which comes from `HitObject.ButtonInput`, so identical code).

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
- Alpha ~0.35 (transparent), colour purple. Greys toward the dropped colour while the hold is active
  but not held, matching `DrawableCardinalHoldNote`'s trail behaviour.
- Hidden (`Progress`/size collapse or `InnerRadius → 1`) once the tail reaches the ring (fully
  consumed), and near spawn.

**Animations** mirror the shoulder note / cardinal hold: spawn pop (scale 0→1, 125 ms), hit (fade + scale
up, expire), miss (fade to red, expire), applied to head + trail as a unit.

**Nested-object plumbing** (`CreateNestedHitObject`/`AddNestedHitObject`/`ClearNestedHitObjects`) mirrors
`DrawableCardinalHoldNote`, creating `DrawableShoulderHoldNoteHead` from `ShoulderHoldNoteHead`.

### Gameplay drawable: `DrawableShoulderHoldNoteHead`

`Objects/Drawables/DrawableShoulderHoldNoteHead.cs` — a straight copy of
`DrawableCardinalHoldNoteHead` retyped to `ShoulderHoldNoteHead` (purely judgemental, draws nothing,
auto-miss on window elapse, public `UpdateResult`).

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
- No new base class factored between the two hold drawables — they mirror, matching the existing
  cardinal/shoulder drawable split. (`ShoulderNoteGeometry` and `HoldEndDragPiece` are the only shared
  helpers.)
- No warning-indicator / catcher interaction for shoulder holds.
