# Slider horizontal lines (zero-time arcs) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a slider path contain a horizontal line — two consecutive nodes at the same
`TimeOffset` (including a child at offset 0 after the head) — which renders in gameplay as a
constant-radius arc.

**Architecture:** The gameplay body, editor polyline, chart format, and Verify already tolerate
zero-time links (polar clip/interp guard the zero delta). The only blockers are three editor
authoring paths that enforce *strictly* increasing node times. Replace those ad-hoc checks with a
single shared validator (`GarbusSliderPath`) encoding the invariant: non-decreasing times, at most
one zero-length link in a row, total duration > 0.

**Tech Stack:** C# / .NET, osu-framework, NUnit (headless visual-test scenes + plain unit tests).

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL fields use `= null!`.
- This is experimental: no backwards-compat layers, no version bumps, no historical notes in code/docs.
- Terminology: osu "beatmap" = "chart"; `Bac*` → `Garbus*`.
- Build: `dotnet build Garbus.Desktop.slnf`
- Test (all): `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
- Test (filtered): `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~<Name>"`

## File Structure

- **Create** `Garbus.Game/Objects/Path/GarbusSliderPath.cs` — the shared path-time validator
  (`AreTimesOrdered`, `AreTimesValid`). One responsibility: the node-time invariant.
- **Create** `Garbus.Game.Tests/GarbusSliderPathTest.cs` — plain-NUnit unit tests for the validator.
- **Modify** `Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs` — multi-click placement uses
  `AreTimesOrdered`; commit requires duration > 0; rubber-band preview gate loosened to `>=`.
- **Modify** `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs` — node-drag `timeShiftValid`
  and `T`-key `insertNodeAtCursor` use the shared validator.
- **Modify** `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs` — placement integration tests.
- **Modify** `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` — node-drag + T-insert tests.
- **Modify** `Garbus.Game.Tests/EditorSliderPolylineTest.cs` — horizontal-segment render test.

---

### Task 1: Shared path-time validator `GarbusSliderPath`

**Files:**
- Create: `Garbus.Game/Objects/Path/GarbusSliderPath.cs`
- Test: `Garbus.Game.Tests/GarbusSliderPathTest.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `static bool GarbusSliderPath.AreTimesOrdered(IReadOnlyList<double> offsets)` — non-decreasing
    (head implied at 0) and no two consecutive zero-length links.
  - `static bool GarbusSliderPath.AreTimesValid(IReadOnlyList<double> offsets)` — `AreTimesOrdered`
    plus `offsets.Count > 0 && offsets[^1] > 0` (total duration > 0).
  - Namespace `Garbus.Game.Objects`.

- [ ] **Step 1: Write the failing unit tests**

Create `Garbus.Game.Tests/GarbusSliderPathTest.cs`:

```csharp
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class GarbusSliderPathTest
    {
        [Test]
        public void StrictlyIncreasingIsValid()
        {
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 100, 200, 300 }), Is.True);
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 100, 200, 300 }), Is.True);
        }

        [Test]
        public void SingleZeroLengthLinkIsValid()
        {
            // two consecutive nodes at the same time (a mid horizontal arc), then a later node.
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 100, 100, 200 }), Is.True);
            // a trailing zero-length link (the last two nodes share a time).
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 100, 100 }), Is.True);
        }

        [Test]
        public void LeadingZeroLengthLinkNeedsALaterNode()
        {
            // child at offset 0 right after the head, followed by a real node: valid.
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 0, 200 }), Is.True);
            // a lone node at 0 is ordered but not valid (duration 0).
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 0 }), Is.True);
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 0 }), Is.False);
        }

        [Test]
        public void TwoConsecutiveZeroLengthLinksRejected()
        {
            // three nodes at one non-zero time (head→100 non-zero, then 100,100 = double zero link).
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 100, 100, 100 }), Is.False);
            // head + two children at 0 = a double zero link at the start.
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 0, 0 }), Is.False);
        }

        [Test]
        public void DecreasingRejected()
        {
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 200, 100 }), Is.False);
        }

        [Test]
        public void EmptyIsNotValid()
        {
            Assert.That(GarbusSliderPath.AreTimesValid(new double[0]), Is.False);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~GarbusSliderPathTest"`
Expected: FAIL to compile — `GarbusSliderPath` does not exist.

- [ ] **Step 3: Implement the validator**

Create `Garbus.Game/Objects/Path/GarbusSliderPath.cs`:

```csharp
// Validation of a slider path's node-time invariant, shared by every editor authoring path
// (placement, node drag, T-insert) so the rule for "horizontal line" arcs lives in one place.
//
// A slider path is the implicit head at time 0 followed by control points carrying a TimeOffset.
// A "zero-length link" (two consecutive nodes at the same time) renders as a constant-radius arc.
// The invariant: times are non-decreasing, at most one zero-length link may occur in a row (so an
// arc is a single collapsed segment, never a stack of three-plus nodes at one time), and — for a
// complete path — the total duration is > 0 (an all-zero path is invisible).

using System.Collections.Generic;

namespace Garbus.Game.Objects;

public static class GarbusSliderPath
{
    /// <summary>
    /// The ordering half of the invariant: control-point <paramref name="offsets"/> (the head at
    /// time 0 is implied) are non-decreasing and no two consecutive links are both zero-length.
    /// Used while a placement is still building up, where the duration is not yet &gt; 0.
    /// </summary>
    public static bool AreTimesOrdered(IReadOnlyList<double> offsets)
    {
        double previous = 0;           // the implicit head sits at time 0
        bool previousLinkZero = false; // there is no link leading into the head

        foreach (double offset in offsets)
        {
            if (offset < previous)
                return false; // times must not go backwards

            bool linkZero = offset == previous;

            if (linkZero && previousLinkZero)
                return false; // two zero-length links in a row = 3+ nodes at one time

            previousLinkZero = linkZero;
            previous = offset;
        }

        return true;
    }

    /// <summary>
    /// The full invariant for a complete path: <see cref="AreTimesOrdered"/> plus a total duration
    /// &gt; 0 (at least one node past the head). The list is non-decreasing when ordered, so the last
    /// element is the maximum (= the duration). Used by node drag, T-insert, and placement commit.
    /// </summary>
    public static bool AreTimesValid(IReadOnlyList<double> offsets)
        => AreTimesOrdered(offsets) && offsets.Count > 0 && offsets[^1] > 0;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~GarbusSliderPathTest"`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Objects/Path/GarbusSliderPath.cs Garbus.Game.Tests/GarbusSliderPathTest.cs
git commit -m "feat: add shared slider path-time validator for zero-time arcs"
```

---

### Task 2: Placement allows horizontal segments

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs`

**Interfaces:**
- Consumes: `GarbusSliderPath.AreTimesOrdered` (Task 1); `SliderBody.Duration` (existing,
  `= Max(TimeOffset)`).
- Produces: no new public surface — behavior change only.

- [ ] **Step 1: Write the failing integration tests**

Add to `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs` (inside the class, after
`TestSliderNodeAtEarlierTimeRejected`). These reuse the existing `waitForComposer`, `input`,
`positionAtAngle`, and `placedObject<T>` helpers already in the file. Note the scroll convention: a
*lower* `yFrac` is a *later* time.

```csharp
[Test]
public void TestPlaceSliderWithHorizontalSegment()
{
    waitForComposer();
    AddStep("select slider tool", () => input.Key(Key.Number8));
    AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.7f)));
    AddStep("click body", () => input.Click(MouseButton.Left));
    AddStep("move to node 1", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
    AddStep("click node 1", () => input.Click(MouseButton.Left));
    // node 2 at the SAME time as node 1 (same yFrac) but a different angle — a horizontal arc.
    AddStep("move to node 2 (same time, new angle)", () => input.MoveMouseTo(positionAtAngle(0, 0.5f)));
    AddStep("click node 2", () => input.Click(MouseButton.Left));
    AddStep("right click to commit", () => input.Click(MouseButton.Right));

    AddAssert("slider placed", () => placedObject<SliderBody>() != null);
    AddAssert("two control points", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(2));
    AddAssert("last two nodes share a time (horizontal arc)", () =>
    {
        var cps = placedObject<SliderBody>()!.Path.ControlPoints;
        return cps[0].TimeOffset == cps[1].TimeOffset && cps[0].TimeOffset > 0;
    });
}

[Test]
public void TestPlaceSliderRejectsThreeNodesAtSameTime()
{
    waitForComposer();
    AddStep("select slider tool", () => input.Key(Key.Number8));
    AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.7f)));
    AddStep("click body", () => input.Click(MouseButton.Left));
    AddStep("move to node 1", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
    AddStep("click node 1", () => input.Click(MouseButton.Left));
    AddStep("move to node 2 (same time)", () => input.MoveMouseTo(positionAtAngle(0, 0.5f)));
    AddStep("click node 2", () => input.Click(MouseButton.Left));
    // a THIRD node at the same time must be rejected (two zero-length links in a row).
    AddStep("move to node 3 (same time)", () => input.MoveMouseTo(positionAtAngle(45, 0.5f)));
    AddStep("click node 3 (rejected)", () => input.Click(MouseButton.Left));
    AddStep("right click to commit", () => input.Click(MouseButton.Right));

    AddAssert("only two control points (third rejected)",
        () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(2));
}

[Test]
public void TestPlaceSliderWithLeadingArcCommits()
{
    waitForComposer();
    AddStep("select slider tool", () => input.Key(Key.Number8));
    AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
    AddStep("click body", () => input.Click(MouseButton.Left));
    // node 1 at the head's time (offset 0) — a leading horizontal arc.
    AddStep("move to node 1 at head time", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
    AddStep("click node 1", () => input.Click(MouseButton.Left));
    // node 2 strictly later, so the path has a real duration and can commit.
    AddStep("move to node 2 (later)", () => input.MoveMouseTo(positionAtAngle(0, 0.3f)));
    AddStep("click node 2", () => input.Click(MouseButton.Left));
    AddStep("right click to commit", () => input.Click(MouseButton.Right));

    AddAssert("slider placed", () => placedObject<SliderBody>() != null);
    AddAssert("first node at head time (leading arc)",
        () => placedObject<SliderBody>()!.Path.ControlPoints[0].TimeOffset, () => Is.EqualTo(0.0));
    AddAssert("second node later",
        () => placedObject<SliderBody>()!.Path.ControlPoints[1].TimeOffset, () => Is.GreaterThan(0.0));
}

[Test]
public void TestPlaceSliderZeroDurationDoesNotCommit()
{
    waitForComposer();
    AddStep("select slider tool", () => input.Key(Key.Number8));
    AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
    AddStep("click body", () => input.Click(MouseButton.Left));
    // the only node sits at the head's time (offset 0): ordered, but total duration is 0.
    AddStep("move to node at head time", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
    AddStep("click node", () => input.Click(MouseButton.Left));
    AddStep("right click to commit", () => input.Click(MouseButton.Right));

    AddAssert("no slider placed (duration 0)", () => placedObject<SliderBody>() == null);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposePlacement"`
Expected: the four new tests FAIL — the current strict `timeOffset <= previousOffset` guard rejects
the equal-time node 2, so `TestPlaceSliderWithHorizontalSegment` gets 1 control point, and the
leading-arc / zero-duration cases behave wrongly.

- [ ] **Step 3: Relax the placement constraints**

In `Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs`:

Change `IsValidForPlacement` (currently line 43) to also require a real duration:

```csharp
    protected override bool IsValidForPlacement =>
        base.IsValidForPlacement && HitObject.Path.ControlPoints.Count > 0 && HitObject.Duration > 0;
```

Replace the strict guard in `tryAddNode` (currently lines 104-110). Replace:

```csharp
        double timeOffset = cursorTime - HitObject.StartTime;
        var previous = controlPoints.Count > 0 ? controlPoints[^1] : null;
        double previousOffset = previous?.TimeOffset ?? 0;

        // control points must always advance in time along the path.
        if (timeOffset <= previousOffset)
            return;
```

with:

```csharp
        double timeOffset = cursorTime - HitObject.StartTime;
        var previous = controlPoints.Count > 0 ? controlPoints[^1] : null;

        // Reject unless the prospective path stays ordered: non-decreasing, with at most one
        // zero-length link in a row (a single horizontal arc). The duration > 0 half is deferred to
        // IsValidForPlacement so a leading zero-arc can still be built up node by node.
        var prospective = new List<double>(controlPoints.Count + 1);
        foreach (var cp in controlPoints)
            prospective.Add(cp.TimeOffset);
        prospective.Add(timeOffset);

        if (!GarbusSliderPath.AreTimesOrdered(prospective))
            return;
```

(`System.Collections.Generic` and `Garbus.Game.Objects` are already imported in this file.)

Loosen the rubber-band preview gate in `Update` (currently line 159) from `>` to `>=` so the preview
line still shows when the next node would land at the same time as the last one. Change:

```csharp
            if (cursorTime - HitObject.StartTime > (HitObject.Path.ControlPoints.Count > 0 ? HitObject.Path.ControlPoints[^1].TimeOffset : 0))
```

to:

```csharp
            if (cursorTime - HitObject.StartTime >= (HitObject.Path.ControlPoints.Count > 0 ? HitObject.Path.ControlPoints[^1].TimeOffset : 0))
```

- [ ] **Step 4: Run the placement tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposePlacement"`
Expected: PASS — the four new tests plus the existing ones (`TestPlaceSliderMultiClick`,
`TestSliderNodeAtEarlierTimeRejected`, etc.) all green.

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs
git commit -m "feat: allow horizontal segments when placing sliders"
```

---

### Task 3: Node drag allows collapsing a link to an arc

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs:327-342` (`timeShiftValid`)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `GarbusSliderPath.AreTimesValid` (Task 1).
- Produces: no new public surface — behavior change only.

- [ ] **Step 1: Write the failing integration test**

Add to `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` (inside the class, near the other
slider-node tests). Reuses existing helpers `placeDiagonalSlider`, `addSecondNode`,
`selectSliderOnLine`, `nodeHandleScreen`, `sliderBlueprint`, `placedObject<T>`, `playfield`, `input`:

```csharp
[Test]
public void TestDraggingNodeOntoNeighbourTimeCreatesArc()
{
    waitForComposer();
    placeDiagonalSlider();   // one node at time > 0
    addSecondNode();         // second node at node0 + 250ms
    selectSliderOnLine();

    AddStep("select node 1", () => { input.MoveMouseTo(nodeHandleScreen(1)); input.Click(MouseButton.Left); });
    AddAssert("node 1 selected",
        () => sliderBlueprint().SelectedNodes.Single(), () => Is.SameAs(placedObject<SliderBody>()!.Path.ControlPoints[1]));

    AddStep("press node 1 handle", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(1));
        input.PressButton(MouseButton.Left);
    });
    // Drag node 1 up in time onto node 0's time (same beat) — collapsing the link to a horizontal arc.
    AddStep("drag onto node 0's time", () =>
    {
        var slider = placedObject<SliderBody>()!;
        var container = playfield.HitObjectContainer;
        var target = container.ScreenSpacePositionAtTime(slider.StartTime + slider.Path.ControlPoints[0].TimeOffset);
        target.X = nodeHandleScreen(1).X;
        input.MoveMouseTo(target);
    });
    AddStep("release", () => input.ReleaseButton(MouseButton.Left));

    AddAssert("nodes now share a time (horizontal arc)", () =>
    {
        var cps = placedObject<SliderBody>()!.Path.ControlPoints;
        return cps[0].TimeOffset == cps[1].TimeOffset && cps[0].TimeOffset > 0;
    });
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestDraggingNodeOntoNeighbourTimeCreatesArc"`
Expected: FAIL — the current `timeShiftValid` requires each offset strictly `> previous`, so the
drag onto node 0's time is rejected and the offsets stay unequal.

- [ ] **Step 3: Rewrite `timeShiftValid` to use the shared validator**

In `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`, replace the whole `timeShiftValid`
method (currently lines 323-342):

```csharp
    /// <summary>
    /// True if shifting every node in <paramref name="moved"/> by <paramref name="deltaTime"/> leaves the
    /// full control-point list strictly increasing in time and every offset above zero (nodes must follow the head).
    /// </summary>
    private static bool timeShiftValid(IReadOnlyList<GarbusPathControlPoint> controlPoints, ICollection<GarbusPathControlPoint> moved, double deltaTime)
    {
        double previous = 0; // the head sits at offset 0.

        foreach (var cp in controlPoints)
        {
            double offset = moved.Contains(cp) ? cp.TimeOffset + deltaTime : cp.TimeOffset;

            if (offset <= previous)
                return false;

            previous = offset;
        }

        return true;
    }
```

with:

```csharp
    /// <summary>
    /// True if shifting every node in <paramref name="moved"/> by <paramref name="deltaTime"/> leaves the
    /// full path valid: non-decreasing times, at most one zero-length link in a row (a single horizontal
    /// arc), and total duration &gt; 0 (the head sits at offset 0).
    /// </summary>
    private static bool timeShiftValid(IReadOnlyList<GarbusPathControlPoint> controlPoints, ICollection<GarbusPathControlPoint> moved, double deltaTime)
    {
        var offsets = new List<double>(controlPoints.Count);

        foreach (var cp in controlPoints)
            offsets.Add(moved.Contains(cp) ? cp.TimeOffset + deltaTime : cp.TimeOffset);

        return GarbusSliderPath.AreTimesValid(offsets);
    }
```

(`System.Collections.Generic` and `Garbus.Game.Objects` are already imported in this file.)

- [ ] **Step 4: Run the selection tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposeSelection"`
Expected: PASS — the new arc-drag test plus every existing selection/drag test (group drag,
single-node drag, seam clones, etc.) green.

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: allow dragging a slider node onto a neighbour to form an arc"
```

---

### Task 4: `T`-key insert allows an arc node

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs:421-460` (`insertNodeAtCursor`)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `GarbusSliderPath.AreTimesValid` (Task 1).
- Produces: no new public surface — behavior change only.

- [ ] **Step 1: Write the failing integration test**

Add to `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`. Reuses `placeDiagonalSlider`,
`selectSliderOnLine`, `positionAtAngle`, `playfield`, `input`, `placedObject<T>`:

```csharp
[Test]
public void TestTInsertAtExistingNodeTimeCreatesArc()
{
    waitForComposer();
    placeDiagonalSlider();   // one node at time > 0
    selectSliderOnLine();

    // Move the cursor to the existing node's exact time but a different angle column, then press T.
    AddStep("move cursor to node time, new angle", () =>
    {
        var slider = placedObject<SliderBody>()!;
        var container = playfield.HitObjectContainer;
        var screen = container.ScreenSpacePositionAtTime(slider.StartTime + slider.Path.ControlPoints[0].TimeOffset);
        screen.X = positionAtAngle(315).X;
        input.MoveMouseTo(screen);
    });
    AddStep("press T", () => input.Key(Key.T));

    AddAssert("second node inserted",
        () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(2));
    AddAssert("the two nodes share a time (horizontal arc)", () =>
    {
        var cps = placedObject<SliderBody>()!.Path.ControlPoints;
        return cps[0].TimeOffset == cps[1].TimeOffset;
    });
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestTInsertAtExistingNodeTimeCreatesArc"`
Expected: FAIL — the current `insertNodeAtCursor` rejects an insert whose time equals an existing
node's time (`controlPoints[insertIndex].TimeOffset == timeOffset`), so no node is added.

- [ ] **Step 3: Relax `insertNodeAtCursor` to validate the prospective path**

In `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`, inside `insertNodeAtCursor`, replace
this block (currently lines 431-445):

```csharp
        double timeOffset = time - HitObject.StartTime;

        // nodes must come strictly after the head.
        if (timeOffset <= 0)
            return;

        var controlPoints = HitObject.Path.ControlPoints;

        int insertIndex = 0;
        while (insertIndex < controlPoints.Count && controlPoints[insertIndex].TimeOffset < timeOffset)
            insertIndex++;

        // don't stack two nodes on the exact same time.
        if (insertIndex < controlPoints.Count && controlPoints[insertIndex].TimeOffset == timeOffset)
            return;
```

with:

```csharp
        double timeOffset = time - HitObject.StartTime;

        var controlPoints = HitObject.Path.ControlPoints;

        int insertIndex = 0;
        while (insertIndex < controlPoints.Count && controlPoints[insertIndex].TimeOffset < timeOffset)
            insertIndex++;

        // Validate the path the insertion would produce: non-decreasing, at most one zero-length link
        // in a row, duration > 0. This permits a single horizontal arc (inserting at an existing node's
        // time, or at time 0 after the head) while still rejecting a backwards time, a 3-node stack, or
        // a duration-0 path.
        var prospective = new List<double>(controlPoints.Count + 1);
        for (int i = 0; i < controlPoints.Count; i++)
            prospective.Add(controlPoints[i].TimeOffset);
        prospective.Insert(insertIndex, timeOffset);

        if (!GarbusSliderPath.AreTimesValid(prospective))
            return;
```

(The rest of the method — `previousRotation`, `changeHandler.BeginChange()`, the `Insert`, and
`editorChart.Update` — is unchanged. `System.Collections.Generic` and `Garbus.Game.Objects` are
already imported.)

- [ ] **Step 4: Run the selection tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposeSelection"`
Expected: PASS — the new arc-insert test and the existing `TestTInsertsTimeOrderedNode` (which
inserts at a strictly-between time) both green.

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: allow T-insert of a slider node forming an arc"
```

---

### Task 5: Regression test — editor polyline renders a horizontal segment

**Files:**
- Test: `Garbus.Game.Tests/EditorSliderPolylineTest.cs`

**Interfaces:**
- Consumes: `EditorSliderPolyline.Build`, `SliderSweep.SegmentsPerLink`, `GarbusPathControlPoint`
  (all existing). No production change — the builder already handles zero-time links; this pins it.

- [ ] **Step 1: Write the characterization test**

Add to `Garbus.Game.Tests/EditorSliderPolylineTest.cs` (inside the class; it already has `using
System.Collections.Generic;`, `osuTK`, and the `px_per_deg` / `centre_x` / `draw_height` / `duration`
constants):

```csharp
[Test]
public void ZeroTimeLinkRendersAsHorizontalSegment()
{
    // A leading zero-length link: the head (time 0) and a child also at time 0, then a later node so
    // the path has a real duration. The head→child link shares a time, so it must draw as a horizontal
    // segment (all its sub-vertices share y) that sweeps only in angle.
    var polyline = new List<Vector2>();
    var nodes = new List<Vector2>();
    var cps = new[]
    {
        new GarbusPathControlPoint { TimeOffset = 0, RotationOffset = 90 },
        new GarbusPathControlPoint { TimeOffset = duration, RotationOffset = 180 },
    };
    EditorSliderPolyline.Build(cps, px_per_deg, centre_x, draw_height, duration, polyline, nodes);

    // Head and the offset-0 child both sit at the bottom edge (time 0 → y = drawHeight).
    Assert.That(nodes[0].Y, Is.EqualTo(draw_height).Within(1e-3));
    Assert.That(nodes[1].Y, Is.EqualTo(draw_height).Within(1e-3));

    // The first link's sub-vertices (indices 0..SegmentsPerLink) all share that y — a horizontal line.
    for (int i = 0; i <= SliderSweep.SegmentsPerLink; i++)
        Assert.That(polyline[i].Y, Is.EqualTo(draw_height).Within(1e-3));

    // ...but it sweeps in x from the head (centre) toward the 90° column.
    Assert.That(polyline[SliderSweep.SegmentsPerLink].X, Is.EqualTo(centre_x + 90 * px_per_deg).Within(1e-3));
}
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~EditorSliderPolylineTest"`
Expected: PASS — the builder already emits a horizontal first link for a zero-time link; this is a
green-on-first-run characterization test guarding that behavior.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS — entire headless suite green.

- [ ] **Step 4: Commit**

```bash
git add Garbus.Game.Tests/EditorSliderPolylineTest.cs
git commit -m "test: pin horizontal-segment rendering of zero-time slider links"
```

---

## Self-Review

**Spec coverage:**
- Invariant (non-decreasing, one zero-link max, duration > 0) → Task 1 (`GarbusSliderPath` + unit tests). ✓
- Placement relaxation + duration-guard commit + rubber-band `>=` → Task 2. ✓
- Node-drag relaxation → Task 3. ✓
- `T`-insert relaxation → Task 4. ✓
- "Two consecutive equal-time nodes" example → Task 2 `TestPlaceSliderWithHorizontalSegment`, Task 3, Task 4. ✓
- "Child at offset 0 after the head" example → Task 2 `TestPlaceSliderWithLeadingArcCommits`, Task 5 render test. ✓
- Gameplay/format/Verify unchanged (already tolerant) → documented; no task needed. ✓

**Placeholder scan:** none — every step has concrete code and exact commands.

**Type consistency:** `AreTimesOrdered` / `AreTimesValid` signatures (`IReadOnlyList<double> → bool`,
namespace `Garbus.Game.Objects`) are defined in Task 1 and used identically in Tasks 2–4.
`SliderBody.Duration`, `GarbusPathControlPoint.TimeOffset`, `Path.ControlPoints`,
`EditorSliderPolyline.Build`, and `SliderSweep.SegmentsPerLink` match their existing declarations.
