# Task 11 Report: SelectionHandler/SelectionBox/EditorSelectionHandler

## What Was Vendored / Trimmed

**New files (all in `Garbus.Game/Edit/Compose/`):**

| File | Source | Trims |
|---|---|---|
| `TernaryState.cs` | osu.Game.Graphics.UserInterface.TernaryState | Namespace only |
| `MenuItemType.cs` | osu.Game.Graphics.UserInterface.MenuItemType | Namespace only |
| `GarbusMenuItem.cs` | osu.Game.Graphics.UserInterface.OsuMenuItem | Replaces with framework `MenuItem` subclass; Hotkey/Icon props dropped |
| `TernaryStateToggleMenuItem.cs` | StatefulMenuItem + TernaryStateMenuItem + TernaryStateToggleMenuItem | Collapsed 3-class hierarchy into 1; GetIconForState dropped (no OsuMenu renderer); Hotkey dropped |
| `MoveSelectionEvent.cs` | MoveSelectionEvent.cs | Namespace only |
| `SelectionBoxControl.cs` | SelectionBoxControl.cs | OsuColour [Resolved] → inline static Color4 constants (YellowDark=#eeaa00, Red=#ed1121, GrayF=#fff); LoadComplete order fixed (InternalChildren before base.LoadComplete) |
| `SelectionBoxButton.cs` | SelectionBoxButton.cs | Same colour approach; load order fixed |
| `SelectionBox.cs` | SelectionBox.cs | **Scale/rotation handles entirely absent** (no SelectionBoxDragHandleContainer, no SelectionBoxScaleHandle, no SelectionBoxRotationHandle, no canRotate/canScaleX/Y bindables); OsuColour → inline Color4; OsuSpriteText/OsuFont → framework SpriteText/FrameworkFont; Ctrl+comma/period rotate shortcuts dropped; `recreateButtons()` called from LoadComplete not BDL |
| `SelectionHandler.cs` | SelectionHandler.cs | `IKeyBindingHandler<GlobalAction>` dropped (osu.Game-only); OsuContextMenuContainer removed (DeselectAll just clears list); SelectionRotationHandler/SelectionScaleHandler + CreateChildDependencies override dropped; OsuMenuItem → GarbusMenuItem; CommonStrings.ButtonsDelete → plain "Delete" |
| `EditorSelectionHandler.cs` | EditorSelectionHandler.cs | All sample/bank/new-combo ternary state stripped (HitSampleInfo, IHasRepeats, IHasComboInformation, Humanizer — none in Garbus); EditorBeatmap → EditorChart; `UpdateTernaryStates()` kept as protected virtual no-op; GetContextMenuItemsForSelection override dropped; DeleteItems wraps EditorChart.RemoveRange in ChangeHandler transaction |

**Modified:** `SelectionBlueprint.cs` — added `ContextMenuItems` property (was deferred from Task 10 per header comment; SelectionHandler needs it for single-blueprint context menus).

## BacSelectionHandler Mental Compile — Every Base Member

| Usage in BacSelectionHandler | Resolved in Garbus |
|---|---|
| `EditorSelectionHandler` base | `EditorSelectionHandler : SelectionHandler<GarbusHitObject>` ✓ |
| `SelectedItems` (BindableList<GarbusHitObject>) | `SelectionHandler<T>.SelectedItems` ✓ |
| `SelectedBlueprints` (IReadOnlyList<SelectionBlueprint<T>>) | `SelectionHandler<T>.SelectedBlueprints` ✓ |
| `SelectionBox.Alpha = 0` | `SelectionHandler<T>.SelectionBox` (public property), Alpha from Drawable ✓ |
| `EditorChart.PerformOnSelection(action)` | `EditorSelectionHandler.EditorChart` (Resolved) ✓ |
| `GetContextMenuItemsForSelection(selection)` override | `virtual protected` on `SelectionHandler<T>` ✓ |
| `base.GetContextMenuItemsForSelection(selection)` call | virtual protected method ✓ |
| `UpdateTernaryStates()` override | `virtual protected` on `EditorSelectionHandler` ✓ |
| `base.UpdateTernaryStates()` call | base no-op ✓ |
| `GetStateFromSelection(items, func)` | `public static` on `SelectionHandler<T>` ✓ |
| `TernaryState` enum | `Garbus.Game.Edit.Compose.TernaryState` ✓ |
| `TernaryStateToggleMenuItem("Anticlockwise")` | `Garbus.Game.Edit.Compose.TernaryStateToggleMenuItem` ✓ |
| `TernaryStateToggleMenuItem.State { BindTarget = ... }` | `Bindable<TernaryState> State` property ✓ |
| `HandleMovement(MoveSelectionEvent<HitObject>)` override | `virtual public` on `SelectionHandler<T>` ✓ |
| `moveEvent.Blueprint.ScreenSpaceSelectionPoint` | `SelectionBlueprint<T>.ScreenSpaceSelectionPoint` ✓ |
| `moveEvent.ScreenSpaceDelta` | `MoveSelectionEvent<T>.ScreenSpaceDelta` ✓ |

**One note for Task 16:** BacSelectionHandler's `GetContextMenuItemsForSelection` override uses `IEnumerable<SelectionBlueprint<HitObject>>` parameter but the base declares `IReadOnlyList<SelectionBlueprint<T>>`. C# requires exact parameter match for override — Task 16 must use `IReadOnlyList<SelectionBlueprint<GarbusHitObject>>` (BAC was overriding osu's same-named method with slightly different signature; the BAC source happens to compile because osu's base also uses IEnumerable but the port must match IReadOnlyList).

## Task 12 BlueprintContainer Internals

All internals BlueprintContainer calls are present:
- `SelectionHandler.HandleSelected(blueprint)` — internal virtual ✓
- `SelectionHandler.HandleDeselected(blueprint)` — internal virtual ✓
- `SelectionHandler.MouseDownSelectionRequested(blueprint, e)` — internal virtual ✓
- `SelectionHandler.MouseUpSelectionRequested(blueprint, e)` — internal ✓
- `SelectionHandler.SelectedItems.BindTo(...)` — BindableList<T> ✓
- `SelectionBox` public property for external Alpha manipulation ✓

## Rotation/Scale

Completely absent. No SelectionBoxRotationHandle, SelectionBoxScaleHandle, SelectionBoxDragHandle, SelectionBoxDragHandleContainer, SelectionRotationHandler, SelectionScaleHandler. SelectionBox contains only the border outline + info text + flip/reverse buttons. `CanFlipX`/`CanFlipY`/`CanReverse` properties are present as no-ops (false by default). `HandleFlip`/`HandleReverse` virtual methods remain on SelectionHandler for completeness.

## Build + Test Evidence

- `dotnet build Garbus.Desktop.slnf` → **0 errors, 7 pre-existing warnings** (no new warnings)
- `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --no-build` → **51/51 passed**

## Commit

`e4f3465` — "Vendor SelectionHandler/SelectionBox stack (Task 11)"

## Concerns

1. **Task 16 GetContextMenuItemsForSelection signature**: BAC source uses `IEnumerable` parameter; Garbus base uses `IReadOnlyList`. Task 16 must match `IReadOnlyList<SelectionBlueprint<GarbusHitObject>>`.
2. **SelectionBoxControl LoadComplete order**: osu's original used `[BackgroundDependencyLoader]` to init InternalChildren, then LoadComplete for post-load setup. Garbus uses a single LoadComplete with internal children first then `base.LoadComplete()` — functionally equivalent since `Circle` is added before transforms.
3. **EditorSelectionHandler double-wrap**: `DeleteItems` wraps `EditorChart.RemoveRange` in `ChangeHandler.BeginChange/EndChange`. `EditorChart.RemoveRange` already wraps in its own `BeginChange/EndChange` (via `TransactionalCommitComponent`). The outer ChangeHandler transaction is still correct because it signals the undo history (GarbusChartChangeHandler), while the inner is just the EditorChart batch — they operate on different interfaces.

## Fix: review findings

**Finding 1 — chose option (a): change to `IEnumerable<SelectionBlueprint<T>>`.**
Osu's real virtual uses `IEnumerable<SelectionBlueprint<T>>` (confirmed in reference source line 426). The old comment was doubly wrong: wrong about osu's type and wrong about C# allowing parameter-type widening in overrides. `IEnumerable` is the correct vendor-faithful signature. Nothing in the file indexing `SelectedBlueprints` (the `IReadOnlyList` property) is affected — the parameter `selection` was never indexed. The call site in `ContextMenuItems` passes `SelectedBlueprints` which satisfies `IEnumerable<T>` via covariance. Header comment updated to reflect the fix.

**Finding 2 — `OnOperationBegan`/`OnOperationEnded` bodies restored.**
`ChangeHandler` was moved from `EditorSelectionHandler` to the base `SelectionHandler<T>` with `[Resolved(CanBeNull = true)]` (matching osu's placement). `OnOperationBegan` now calls `ChangeHandler?.BeginChange()` and `OnOperationEnded` calls `ChangeHandler?.EndChange()`. The duplicate `[Resolved] IEditorChangeHandler? ChangeHandler` in `EditorSelectionHandler` was removed (inherited from base).

**Finding 3 — `HitObjectUpdated` subscription added.**
`EditorSelectionHandler.load()` now subscribes `EditorChart.HitObjectUpdated += _ => Scheduler.AddOnce(UpdateTernaryStates)` alongside the existing `SelectedItems.CollectionChanged` subscription, matching osu's `EditorBeatmap.HitObjectUpdated` pattern.

**Test evidence:** `dotnet build Garbus.Desktop.slnf` → 0 errors; `dotnet test` → 51/51 passed.
