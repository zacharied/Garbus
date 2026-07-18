# Alt-modifier side selection during placement

## Goal

Every `IHasSide` object (`GarbusSlamEdge`, `GarbusSlamCentered`, `SliderBody`) currently places as
`Side = Left` and must be flipped afterward via the S key, the "Right side" context-menu toggle, or the
Inspector dropdown. Holding **Alt** during placement should place the object with `Side = Right`
directly, without the extra step.

## Locked decisions

- **Live preview:** while the placement is still waiting for its first click (`PlacementActive ==
  Waiting`), `Side` updates every frame from the current Alt state — mirroring how `AngleDeg` already
  live-updates from the snap result during the same window. The preview visibly flips Left/Right as Alt
  is held/released before the click.
- **Slider timing:** for `SliderPlacementBlueprint`'s multi-click flow (head click → node clicks → right
  click / Ctrl+click commit), `Side` is decided by the Alt state at the moment the head is placed (the
  first click, which transitions `PlacementActive` to `Active`) and is **locked** for the rest of that
  placement. Holding or releasing Alt during node-dragging has no further effect.
- **Scope:** applies uniformly to all three `IHasSide` implementers; no per-type opt-out.

## Implementation

One hook point covers all three types: `GarbusPlacementBlueprint<T>.UpdateTimeAndPosition`
(`Garbus.Game/Edit/Blueprints/GarbusPlacementBlueprint.cs`) is the shared base for
`SlamEdgePlacementBlueprint` and `SlamCenteredPlacementBlueprint` (via `InstantPlacementBlueprint<T>`) and
for `SliderPlacementBlueprint` directly, and already runs every frame, writing `HitObject.AngleDeg` while
`PlacementActive == Waiting`. Add a parallel conditional there:

```csharp
if (PlacementActive == PlacementState.Waiting && HitObject is IHasSide hasSide)
    hasSide.Side = GetContainingInputManager()?.CurrentState.Keyboard.AltPressed == true
        ? HorizontalDirection.Right
        : HorizontalDirection.Left;
```

`SliderPlacementBlueprint`'s constructor keeps its explicit `Side = HorizontalDirection.Left` (the
property is `required` on `SliderBody`); it's immediately overwritten by the first
`UpdateTimeAndPosition` call once the blueprint is in the scene graph.

No changes needed to `SlamEdgePlacementBlueprint` / `SlamCenteredPlacementBlueprint` /
`SliderPlacementBlueprint` themselves — the base class change is sufficient.

## Testing

Added to `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`, following the existing
`input.PressKey(Key.LControl)` / `ReleaseKey` pattern already used for the head-only-slider Ctrl test:

1. Hold Alt, place a slam (edge or centered) → assert `Side == Right`. Release Alt, place another →
   assert `Side == Left`.
2. Slider: hold Alt through the head click only, release Alt before adding a node and committing →
   assert final `Side == Right` (proves the lock-in at head placement, not reverted by releasing Alt
   mid-drag).
