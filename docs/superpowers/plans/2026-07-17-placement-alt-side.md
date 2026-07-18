# Alt-Modifier Side Selection During Placement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Holding Alt while placing an `IHasSide` object (`GarbusSlamEdge`, `GarbusSlamCentered`,
`SliderBody`) places it with `Side = Right` instead of the current always-`Left` default.

**Architecture:** One shared placement-blueprint base class (`GarbusPlacementBlueprint<T>`) already
live-writes `AngleDeg` from the snap result every frame while a placement is waiting for its first click.
Add a parallel per-frame write of `Side` from the current Alt keyboard state, gated behind the same
`IHasSide` type check, in the same method. Because all three placement blueprints for `IHasSide` types
derive from this base, no other files need to change.

**Tech Stack:** C# / osu-framework (`ManualInputManager`-driven NUnit headless tests via
`Garbus.Game.Tests`).

## Global Constraints

- No new abstractions, no per-type opt-out — the check is `HitObject is IHasSide`, uniform across all
  three implementers (spec: `docs/superpowers/specs/2026-07-17-placement-alt-side-design.md`).
- `Side` updates live only while `PlacementActive == PlacementState.Waiting`; once a placement transitions
  to `Active` (first click committed), `Side` must not change for the rest of that placement, regardless
  of later Alt presses/releases.
- No changes to `SlamEdgePlacementBlueprint`, `SlamCenteredPlacementBlueprint`, or
  `SliderPlacementBlueprint` — the fix lives entirely in the shared base class.

---

### Task 1: Alt-driven Side during placement

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/GarbusPlacementBlueprint.cs:46-56` (add `using
  Garbus.Game.Core;` to the usings block at the top, and the new conditional in
  `UpdateTimeAndPosition`)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` (two new `[Test]` methods, appended
  after `TestSKeyDoesNotToggleShoulderSide`)

**Interfaces:**
- Consumes: `IHasSide.Side` (`Garbus.Game/Objects/IHasSide.cs`), `HorizontalDirection`
  (`Garbus.Game/Core/HorizontalDirection.cs`, values `Left`/`Right`), `PlacementBlueprint.PlacementActive`
  / `PlacementState` (`Garbus.Game/Edit/Compose/PlacementBlueprint.cs`), `Drawable.GetContainingInputManager()`
  (osu-framework), `InputManager.CurrentState.Keyboard.AltPressed` (osu-framework `KeyboardState`).
- Produces: nothing consumed by later tasks — this is the only task in the plan.

- [ ] **Step 1: Write the two failing tests**

Open `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` and add these two methods immediately
after `TestSKeyDoesNotToggleShoulderSide` (which currently ends at line 1405 — insert after its
closing `}`, still inside the class body):

```csharp
        [Test]
        public void TestAltDuringPlacementSetsSlamSideRight()
        {
            waitForComposer();

            AddStep("select slam-centered tool", () => input.Key(Key.Number6));
            AddStep("hold alt", () => input.PressKey(Key.LAlt));
            AddStep("move to angle", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("click to place", () => input.Click(MouseButton.Left));
            AddStep("release alt", () => input.ReleaseKey(Key.LAlt));
            AddAssert("slam centered placed Right", () => placedObject<GarbusSlamCentered>()!.Side, () => Is.EqualTo(HorizontalDirection.Right));
            settleWith(() => placedObject<GarbusSlamCentered>()!.StartTime);
            AddStep("switch to select tool", () => input.Key(Key.Number1));

            AddStep("select slam-edge tool", () => input.Key(Key.Number7));
            AddStep("move to angle without alt", () => input.MoveMouseTo(positionAtAngle(90, 0.5f)));
            AddStep("click to place", () => input.Click(MouseButton.Left));
            AddAssert("slam edge placed Left (no alt)", () => placedObject<GarbusSlamEdge>()!.Side, () => Is.EqualTo(HorizontalDirection.Left));
        }

        [Test]
        public void TestAltLockedInAtSliderHeadPlacement()
        {
            waitForComposer();

            AddStep("select slider tool", () => input.Key(Key.Number8));
            AddStep("hold alt", () => input.PressKey(Key.LAlt));
            AddStep("move to head", () => input.MoveMouseTo(positionAtAngle(270, 0.7f)));
            AddStep("click body (head)", () => input.Click(MouseButton.Left));
            AddStep("release alt", () => input.ReleaseKey(Key.LAlt));
            AddStep("move to node without alt", () => input.MoveMouseTo(positionAtAngle(0, 0.4f)));
            AddStep("click node", () => input.Click(MouseButton.Left));
            AddStep("right click to commit", () => input.Click(MouseButton.Right));
            AddAssert("slider placed", () => placedObject<SliderBody>() != null);
            settleWith(() => placedObject<SliderBody>()!.StartTime);
            AddStep("switch to select tool", () => input.Key(Key.Number1));
            AddAssert("slider Side locked Right despite alt release mid-drag", () => placedObject<SliderBody>()!.Side, () => Is.EqualTo(HorizontalDirection.Right));
        }
```

(Discovered during execution: without `settleWith` + switching back to the select tool, the freshly
spawned next-placement blueprint spins on stale mouse state during test teardown and throws a `NaN`
position exception — every other slider-placement helper in this file already does this cleanup for
that reason.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestAltDuringPlacementSetsSlamSideRight|FullyQualifiedName~TestAltLockedInAtSliderHeadPlacement"`

Expected: both FAIL — `TestAltDuringPlacementSetsSlamSideRight` fails on the "slam centered placed
Right" assertion (actual `Left`, since nothing reads Alt state yet); `TestAltLockedInAtSliderHeadPlacement`
fails on "slider Side locked Right despite alt release mid-drag" (actual `Left`).

- [ ] **Step 3: Implement the Alt-driven Side write**

In `Garbus.Game/Edit/Blueprints/GarbusPlacementBlueprint.cs`, add `Garbus.Game.Core` to the usings
(alphabetical order with the existing `Garbus.Game.*` usings):

```csharp
using Garbus.Game.Core;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Objects;
```

Then change `UpdateTimeAndPosition` from:

```csharp
    public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
    {
        var result = Composer?.FindSnappedAngleTimeAndPosition(screenSpacePosition) ?? new SnapResult(screenSpacePosition, fallbackTime);

        base.UpdateTimeAndPosition(result.ScreenSpacePosition, result.Time ?? fallbackTime);

        if (PlacementActive == PlacementState.Waiting && result is GarbusSnapResult garbus)
            HitObject.AngleDeg = garbus.AngleDeg;

        return result;
    }
```

to:

```csharp
    public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
    {
        var result = Composer?.FindSnappedAngleTimeAndPosition(screenSpacePosition) ?? new SnapResult(screenSpacePosition, fallbackTime);

        base.UpdateTimeAndPosition(result.ScreenSpacePosition, result.Time ?? fallbackTime);

        if (PlacementActive == PlacementState.Waiting && result is GarbusSnapResult garbus)
            HitObject.AngleDeg = garbus.AngleDeg;

        // Alt during placement picks the Right side; locked in once the first click moves
        // PlacementActive past Waiting, so it can't be nudged mid-drag by releasing/re-pressing Alt.
        if (PlacementActive == PlacementState.Waiting && HitObject is IHasSide hasSide)
        {
            hasSide.Side = GetContainingInputManager()?.CurrentState.Keyboard.AltPressed == true
                ? HorizontalDirection.Right
                : HorizontalDirection.Left;
        }

        return result;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestAltDuringPlacementSetsSlamSideRight|FullyQualifiedName~TestAltLockedInAtSliderHeadPlacement"`

Expected: both PASS.

- [ ] **Step 5: Run the full editor test suite to check for regressions**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`

Expected: all tests PASS (in particular, the existing `TestSKeyTogglesSlamSide` and the slider
Side-toggle context-menu test, which exercise the same `Side` property, must be unaffected since they
place objects without touching Alt at all — the new code only overrides `Side` while `Waiting`, and
placement finishes long before either of those tests reads `Side`).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/GarbusPlacementBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: alt during placement sets IHasSide objects to the Right side"
```

## Self-Review

**Spec coverage:**
- Live preview / Alt read every frame while `Waiting` — covered by Step 3's implementation and asserted
  indirectly by Step 1's tests (Alt state at the moment of the click, which is the last `Waiting`-phase
  frame, determines the committed `Side`).
- Slider lock-in at head placement — covered by `TestAltLockedInAtSliderHeadPlacement` (Alt released
  before the node click/commit, `Side` still ends up `Right`).
- Uniform across all three `IHasSide` types, no per-type opt-out — covered by the single shared-base-class
  change; `TestAltDuringPlacementSetsSlamSideRight` exercises both slam subtypes, and
  `TestAltLockedInAtSliderHeadPlacement` exercises the slider.
- No changes to the three placement-blueprint subclasses — confirmed; only the shared base class and the
  test file are touched.

**Placeholder scan:** none — all steps contain literal code/commands.

**Type consistency:** `HorizontalDirection.Left`/`Right`, `IHasSide.Side`, `PlacementState.Waiting` all
match their existing declarations found during research; no new types introduced.
