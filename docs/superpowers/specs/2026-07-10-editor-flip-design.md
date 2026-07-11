# Editor: flip hit objects — design

## Goal
Give the compose editor two right-click actions on a selection that mirror the selected
hit objects:

1. **"Flip around angle…"** — interactive. Drops a vertical pivot bar on the cursor; on click,
   the selection is reflected about the angle under the cursor.
2. **"Flip selection"** — one-shot. Reflects the selection about the centre of its own angular
   bounding box.

Both are **true mirrors**: a reflection reverses handedness, so besides the angle we also negate
slider control-point rotation offsets and flip slam-edge rotational direction.

## Background / constraints
A Garbus hit object has **no free 2D position**. It is a *direction* `θ` (a ray from the playfield
centre) plus a *time* `t`; radius is `f(t)` (constant scroll). So a selection "bounding box" is an
**angular range**, not a spatial box, and every flip is an **angular reflection** — never a Cartesian
translate.

Angle convention (see `EditorAngleMapping`, `UI/GarbusScrollingHitObjectContainer.cs`): standard math
orientation, CCW positive, screen-y flipped. θ=0 East, 90 North, 180 West, 270 South. Reflection about
pivot φ is `θ → 2φ − θ`.

The editor unrolls the circle onto the timeline x-axis (`EditorAngleMapping`): grid left edge = 135°
(`ANGLE_ORIGIN`), the wrap seam falls on the 315° diagonal, with ±`GHOST_DEGREES` ghost bands. This is
why "flip x-positions around the bbox centre" must be computed in an angular frame that is robust to the
seam.

Object model (placeable types):

| Type | Angle | Handedness / other |
|---|---|---|
| `CardinalNote` | `IHasMutableAngle` (`int AngleDeg`) | — |
| `HoldNote` | `IHasMutableAngle` + `IHasDuration` | — |
| `GarbusSlamCentered` | `IHasMutableAngle` | `Side` (init-only, non-geometric) |
| `GarbusSlamEdge` | `IHasMutableAngle` | `Direction` CW/CCW (mutable field) — reverses under mirror |
| `SliderBody` | `IHasMutableAngle` (head) + `IHasDuration` | `Path.ControlPoints[].RotationOffset` (int, CCW) — negates under mirror; `Side` = stick assignment, non-geometric |
| `ShoulderNote` | **derived** angle: `AngleDeg => Side.ToAngleDeg()` (Right=0/East, Left=180/West) | angle is not mutable — only ever E or W |

`Side` (`HorizontalDirection { Left=-1, Right=1 }`) on sliders/slams selects which analog-stick catcher
tracks the object — an input assignment, not geometry. It sits on the E/W axis and is left unchanged by
a reflection.

## Core primitive — one modular reflection
Reflection on the circle is purely modular: `θ → NormalizeDeg(sumDeg − θ)` where `sumDeg = 2·φ (mod 360)`
fully defines the map. No seam-unwrapping is needed at apply-time — unwrapping only matters when
*computing* the bbox centre (below).

Add to `GarbusSelectionHandler`:

```csharp
private void Flip(int sumDeg)
{
    EditorChart.PerformOnSelection(h =>
    {
        switch (h)
        {
            case ShoulderNote shoulder:
                // No mutable angle: reflect its derived E/W angle and re-derive Side by hemisphere.
                int a = EditorAngleMapping.NormalizeDeg(sumDeg - shoulder.Side.ToAngleDeg());
                shoulder.Side = InEastHemisphere(a) ? HorizontalDirection.Right : HorizontalDirection.Left;
                break;

            case IHasMutableAngle mutable:
                mutable.AngleDeg = EditorAngleMapping.NormalizeDeg(sumDeg - mutable.AngleDeg);
                // Handedness reversal (a mirror flips chirality):
                if (h is SliderBody slider)
                    foreach (var cp in slider.Path.ControlPoints)
                        cp.RotationOffset = -cp.RotationOffset;
                if (h is GarbusSlamEdge slam)
                    slam.Direction = slam.Direction == RotationalDirection.Clockwise
                        ? RotationalDirection.Anticlockwise
                        : RotationalDirection.Clockwise;
                break;
        }
    });
}
```

`InEastHemisphere(a)`: East when `cos(a) ≥ 0`, i.e. `a ∈ [0,90] ∪ [270,360)`. The N/S ties (a = 90 or
270) resolve to East by the `≥ 0`. Note `ShoulderNote` is matched **before** the `IHasMutableAngle`
branch because it is not `IHasMutableAngle` anyway, but the explicit case documents the special handling.

One `PerformOnSelection` call ⇒ **single undo/redo step** (matches the existing Side/Direction toggles).

### Handedness rationale
- **Slider**: `RotationOffset` is a CCW rotation of each control point relative to the head; a mirror
  reverses CCW↔CW, so each offset negates. The head `AngleDeg` reflects like any angle.
- **SlamEdge**: `Direction` is the CW/CCW sweep sense; a mirror reverses it.
- **SlamCentered / `Side` fields**: `Side` lies on the E/W (mirror) axis and is non-geometric —
  untouched. No source-model change (no need to make `SlamCentered.Side` settable).

## "Flip selection" — pivot = selection centre (seam-robust)
```csharp
Flip(ComputeSelectionReflectionSum());
```

`ComputeSelectionReflectionSum()` finds the tightest angular arc covering the selection and returns
`(minUnwrapped + maxUnwrapped) mod 360`:

1. Collect each selected object's representative angle (slider = head `AngleDeg`; shoulder = derived
   angle; others = `AngleDeg`), normalised to `[0,360)`.
2. Sort ascending. Compute the gap between each consecutive pair and the wrap gap (`first + 360 − last`).
3. The **largest gap** is the empty region; the covering arc starts at the angle just after it.
4. Unwrap the angles starting there into a monotone run (add 360 on wrap). `min` = start, `max` = last.
5. `sum = NormalizeDeg(min + max)`.

Properties:
- A **134°/136°** pair: largest gap is the ~358° empty region, covering arc is [134,136], centre ≈135°,
  they **swap** — not reflected across the whole strip.
- A **single object**: `min = max = θ` ⇒ `sum = 2θ` ⇒ angle unchanged, handedness still reverses ⇒
  **mirror in place** (meaningful for a lone slider/slam-edge).

The largest-gap helper can live in `EditorAngleMapping` (e.g. `ReflectionSum(IEnumerable<int> angles)`)
to keep the handler small and unit-testable, or as a private method on the handler. Either is fine;
prefer `EditorAngleMapping` since angle math is its remit.

## "Flip around angle…" — interactive pivot bar
A new lightweight overlay drawable, `FlipPivotOverlay`, hosted **topmost** in the composer's content,
inactive by default:

- `ReceivePositionalInputAt(pos) => active` — only intercepts input while a flip is in progress, so it
  does not steal clicks from the blueprint stack the rest of the time.
- Activated by `composer.BeginFlipAroundAngle(Action<int> onCommit)`, which stores the callback, shows
  the overlay, and sets `active = true`.
- **OnMouseMove**: snap the cursor via
  `EditorAngleMapping.SnapX(localX / playfield.DrawWidth, composer.AngleSnap.Value)` → draw a vertical
  bar at the returned x-fraction; remember the returned pivot angle. (Optional nicety: also draw a ghost
  twin bar when the pivot is within a ghost band, mirroring how objects show ghost twins.)
- **OnClick (left)**: `onCommit(NormalizeDeg(2 * pivot))`, then deactivate.
- **OnClick (right) / OnKeyDown(Escape)**: cancel, deactivate (no mutation).

The pivot bar snaps to the existing angle-snap grid (`GarbusHitObjectComposer.AngleSnap`, the same
BindableInt the toolbox "angle snap" radio buttons drive). No live preview of the flipped objects —
bar only.

The menu action passes the reflection through the selection handler: the overlay stays geometry-agnostic
(it only knows "the user picked pivot φ → call `onCommit(2φ)`"); the handler's `Flip` closure reads the
*current* `SelectedHitObjects` at commit time.

## Menu items (universal, before base Delete)
In `GarbusSelectionHandler.GetContextMenuItemsForSelection`, yielded for **every** selection (before the
`base` items, which end with Delete):

```csharp
yield return new GarbusMenuItem("Flip around angle...", MenuItemType.Standard,
    () => composer.BeginFlipAroundAngle(sum => Flip(sum)));
yield return new GarbusMenuItem("Flip selection", MenuItemType.Standard,
    () => Flip(ComputeSelectionReflectionSum()));
```

(The existing Anticlockwise / Right-side ternary toggles remain above these for their respective
type-homogeneous selections.)

## Components & boundaries
- **`GarbusSelectionHandler`** — owns the menu items, `Flip(int)`, and pivot computation. Depends on
  `EditorChart` (mutation + undo) and the resolved `composer` (to start the interactive mode). No new
  fields beyond what's needed to wire the menu items.
- **`GarbusHitObjectComposer`** — gains `BeginFlipAroundAngle(Action<int>)` and hosts the
  `FlipPivotOverlay`. Depends on nothing new; reuses `AngleSnap` and `Playfield`.
- **`FlipPivotOverlay`** — pure input/visual: turns a cursor position into a snapped pivot angle and a
  drawn bar, and invokes a callback. Knows nothing about hit objects or reflection maths.
- **`EditorAngleMapping`** — may gain a `ReflectionSum(angles)` (largest-gap) helper; already owns
  `NormalizeDeg`, `SnapX`, `MinimalDiff`.

## Testing (`Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`)
Follow the existing place→select→invoke-menu-action→assert→undo/redo pattern
(`changeHandler.RestoreState(±1)`). `Flip(int)` and `ComputeSelectionReflectionSum()` are unit-testable
directly; the overlay is driven via `ManualInputManager`.

Cases:
- CardinalNote reflects N↔S under φ=0 (`sum=0`); E/W unchanged.
- SliderBody: head `AngleDeg` reflects **and** every `RotationOffset` negates.
- GarbusSlamEdge: `Direction` flips CW↔CCW (and angle reflects).
- ShoulderNote: `Side` flips under a vertical-ish pivot (φ=90) and is **unchanged** under φ=0.
- "Flip selection" on two objects: they swap about their midpoint.
- **Seam-straddling** selection (134° & 136°): they swap about ~135° — NOT reflected across the strip.
- Single slider "Flip selection": angle unchanged, `RotationOffset` negated (mirror in place).
- Mutation is a **single** undo step; undo restores, redo re-applies.
- Both menu items present for every selection type (Cardinal, Hold, Shoulder, SlamCentered, SlamEdge,
  Slider, and a mixed selection).
- Interactive: `BeginFlipAroundAngle` + move cursor to a known x + left-click applies the expected
  reflection; Escape / right-click cancels with no mutation.

## Out of scope
- **Time reversal** (`t → 2τ − t`, reversing the selection's order in time, internally reversing
  holds/sliders). A genuinely separate degree of freedom; deferred.
- Rotation / translation (not flips; no free XY exists).
- Keyboard shortcuts for the flip actions (context-menu only for now).
