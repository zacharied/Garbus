# Zero-duration sliders in the editor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the editor author and edit slider objects whose total duration is `0` (a constant-radius arc existing at a single instant), rendered as a horizontal line at the object's StartTime.

**Architecture:** Two mechanical relaxations plus one root-cause render fix. Relax the `Duration > 0` gate uniformly (one clause in `AreTimesValid`, one in the placement blueprint). Fix the `timeOffset / duration` vertical fraction at its source (`EditorSliderPolyline.Build`) to pin every node to the bottom line when duration is `0`, then remove the three `duration <= 0` early-returns that currently skip rendering such sliders.

**Tech Stack:** C# / .NET 8, osu-framework, NUnit (headless visual test scenes).

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL-initialised fields use `= null!`.
- This is the Garbus port repo; the integration branch is `master`. No backwards-compat layers, no version bumps.
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- The gameplay half is already landed (commit `ff47564`: `DrawableSliderChild` grants the catch on a zero-record tracker). The chart format needs no change. Do NOT touch gameplay or format code in this plan.
- The `≥1 control point` floor is retained deliberately (see the spec's *Forward-compatibility* section). Keep it expressed in exactly the two clauses this plan edits — do not scatter new `Count > 0` assumptions through render/edit paths.
- Commit via the Nimbalyst commit proposal tool (`mcp__nimbalyst__developer_git_commit_proposal`), not `git commit` on the command line — this is a shared worktree.

**Spec:** `docs/superpowers/specs/2026-07-16-zero-duration-sliders-design.md`

---

### Task 1: Relax the ordering/duration gate (`AreTimesValid`)

Dropping the `offsets[^1] > 0` clause relaxes both node-drag (`SliderSelectionBlueprint.timeShiftValid`) and T-insert (`insertNodeAtCursor`) at once, since both route through `AreTimesValid`. The ordering rule (non-decreasing, ≤1 zero-link in a row) and the `≥1 node` floor both stay.

**Files:**
- Modify: `Garbus.Game/Objects/Path/GarbusSliderPath.cs:43-49`
- Test: `Garbus.Game.Tests/GarbusSliderPathTest.cs:25-33`

**Interfaces:**
- Produces: `GarbusSliderPath.AreTimesValid(IReadOnlyList<double> offsets) => AreTimesOrdered(offsets) && offsets.Count > 0` — now `True` for `{0}`, still `False` for `{}` and for any non-ordered input.

- [ ] **Step 1: Update the unit test to expect the new behavior**

In `Garbus.Game.Tests/GarbusSliderPathTest.cs`, replace the `LeadingZeroLengthLinkNeedsALaterNode` method (lines 25-33) with:

```csharp
        [Test]
        public void LoneZeroNodeIsAValidZeroDurationArc()
        {
            // child at offset 0 right after the head, followed by a real node: valid.
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 0, 200 }), Is.True);
            // a lone node at 0 (head + one node at the same instant) is a zero-duration arc: now valid.
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 0 }), Is.True);
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 0 }), Is.True);
        }
```

(`TwoConsecutiveZeroLengthLinksRejected` already pins `{0,0}` → `False` and `EmptyIsNotValid` pins `{}` → `False`; leave both unchanged.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~GarbusSliderPathTest.LoneZeroNodeIsAValidZeroDurationArc"`
Expected: FAIL — `AreTimesValid({0})` currently returns `False` (asserted `True`).

- [ ] **Step 3: Relax `AreTimesValid`**

In `Garbus.Game/Objects/Path/GarbusSliderPath.cs`, replace the doc comment + method (lines 43-49):

```csharp
    /// <summary>
    /// The full invariant for a complete path: <see cref="AreTimesOrdered"/> plus at least one control
    /// point (the head alone is not a path). The total duration MAY be 0 — a path collapsed entirely to
    /// the head's time is a constant-radius arc at a single instant. Used by node drag, T-insert, and
    /// placement commit.
    /// </summary>
    public static bool AreTimesValid(IReadOnlyList<double> offsets)
        => AreTimesOrdered(offsets) && offsets.Count > 0;
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~GarbusSliderPathTest"`
Expected: PASS (all `GarbusSliderPathTest` cases).

- [ ] **Step 5: Commit**

Propose a commit via `mcp__nimbalyst__developer_git_commit_proposal` with:
- Files: `Garbus.Game/Objects/Path/GarbusSliderPath.cs`, `Garbus.Game.Tests/GarbusSliderPathTest.cs`
- Message: `feat: allow zero total duration in slider path validation`

---

### Task 2: Render the degenerate line in the shared polyline builder

`EditorSliderPolyline.Build` divides each node's `TimeOffset` by the slider `duration`. When duration is `0`, that is `0/0 = NaN`. Pin every node to the bottom line (`y = drawHeight`) in that case. This is the single root-cause fix that both editor render paths consume.

**Files:**
- Modify: `Garbus.Game/Edit/Drawables/EditorSliderPolyline.cs:16-53`
- Test: `Garbus.Game.Tests/EditorSliderPolylineTest.cs` (add one test)

**Interfaces:**
- Consumes: `EditorSliderPolyline.Build(IReadOnlyList<GarbusPathControlPoint> controlPoints, float pxPerDeg, float centreX, float drawHeight, double duration, List<Vector2> polyline, List<Vector2> nodes)` — now accepts `duration == 0` (all node y's = `drawHeight`, no NaN).

- [ ] **Step 1: Write the failing test**

In `Garbus.Game.Tests/EditorSliderPolylineTest.cs`, add after `ZeroTimeLinkRendersAsHorizontalSegment` (after line 103):

```csharp
        [Test]
        public void ZeroDurationPinsEveryNodeToTheBottomLine()
        {
            // A slider collapsed entirely to StartTime (duration 0): head + one node at time 0, different
            // angle. No vertical extent, so every vertex sits on the bottom line (y = drawHeight) with no
            // NaN, while still sweeping in x toward the node's angle column.
            var polyline = new List<Vector2>();
            var nodes = new List<Vector2>();
            var cp = new GarbusPathControlPoint { TimeOffset = 0, RotationOffset = 90 };
            EditorSliderPolyline.Build(new[] { cp }, px_per_deg, centre_x, draw_height, 0.0, polyline, nodes);

            Assert.That(nodes.Count, Is.EqualTo(2));
            Assert.That(nodes[0], Is.EqualTo(new Vector2(centre_x, draw_height)));
            Assert.That(nodes[1].X, Is.EqualTo(centre_x + 90 * px_per_deg).Within(1e-3));
            Assert.That(nodes[1].Y, Is.EqualTo(draw_height).Within(1e-3));

            // Every polyline vertex sits on the bottom line — and none is NaN.
            foreach (var v in polyline)
            {
                Assert.That(float.IsNaN(v.Y), Is.False);
                Assert.That(v.Y, Is.EqualTo(draw_height).Within(1e-3));
            }
        }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~EditorSliderPolylineTest.ZeroDurationPinsEveryNodeToTheBottomLine"`
Expected: FAIL — `nodes[1].Y` is `NaN` (from `1 - 0/0`), so the `Within` assertion fails.

- [ ] **Step 3: Guard the vertical fraction against zero duration**

In `Garbus.Game/Edit/Drawables/EditorSliderPolyline.cs`, update the doc line (line 21) from "and guarantees `duration` > 0" to:

```csharp
    /// and supplies fresh (or cleared) lists. <paramref name="duration"/> may be 0 — a path collapsed
    /// entirely to the head's time; every node then pins to the bottom line (y = drawHeight).
```

Then replace the local `toPoint` (lines 52-53):

```csharp
        // A zero-duration path (every node at time 0) has no vertical extent — pin every node to the
        // bottom line (the StartTime / judgement line) instead of dividing by zero.
        Vector2 toPoint(float angleOffset, double timeOffset)
        {
            float yFrac = duration > 0 ? (float)(timeOffset / duration) : 0f;
            return new Vector2(centreX + EditorAngleMapping.Direction * angleOffset * pxPerDeg, drawHeight * (1 - yFrac));
        }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~EditorSliderPolylineTest"`
Expected: PASS (all `EditorSliderPolylineTest` cases — the existing `duration > 0` cases still divide as before).

- [ ] **Step 5: Commit**

Propose a commit via `mcp__nimbalyst__developer_git_commit_proposal` with:
- Files: `Garbus.Game/Edit/Drawables/EditorSliderPolyline.cs`, `Garbus.Game.Tests/EditorSliderPolylineTest.cs`
- Message: `feat: pin zero-duration slider polyline to the bottom line`

---

### Task 3: Relax the placement commit gate

Drop the `Duration > 0` requirement from slider placement so a right-click commits a slider whose only node sits at the head's time. The `≥1 control point` floor stays (both in `IsValidForPlacement` and the `EndPlacement` argument).

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs:43-44` and the comment at `:108-110`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs:295-308`

**Interfaces:**
- Consumes: `SliderPlacementBlueprint.IsValidForPlacement` (now `base.IsValidForPlacement && HitObject.Path.ControlPoints.Count > 0`).

- [ ] **Step 1: Flip the placement test to expect a commit**

In `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs`, replace `TestPlaceSliderZeroDurationDoesNotCommit` (lines 295-308) with:

```csharp
        [Test]
        public void TestPlaceSliderZeroDurationCommits()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(Key.Number8));
            AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("click body", () => input.Click(MouseButton.Left));
            // the only node sits at the head's time (offset 0): a zero-duration constant-radius arc.
            AddStep("move to node at head time", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
            AddStep("click node", () => input.Click(MouseButton.Left));
            AddStep("right click to commit", () => input.Click(MouseButton.Right));

            AddAssert("slider placed", () => placedObject<SliderBody>() != null);
            AddAssert("one control point at head time",
                () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));
            AddAssert("zero duration", () => placedObject<SliderBody>()!.Duration, () => Is.EqualTo(0.0));
        }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposePlacement.TestPlaceSliderZeroDurationCommits"`
Expected: FAIL at "slider placed" — the commit is currently rejected (`Duration > 0` gate), so `placedObject<SliderBody>()` is `null`.

- [ ] **Step 3: Relax the placement gate**

In `Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs`, replace `IsValidForPlacement` (lines 43-44):

```csharp
    protected override bool IsValidForPlacement =>
        base.IsValidForPlacement && HitObject.Path.ControlPoints.Count > 0;
```

Then update the now-stale comment in `tryAddNode` (lines 108-110) to:

```csharp
        // Reject unless the prospective path stays ordered: non-decreasing, with at most one
        // zero-length link in a row (a single horizontal arc). A zero total duration is allowed (a
        // constant-radius arc at a single instant), so only the ordering rule is enforced here.
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposePlacement"`
Expected: PASS — including `TestPlaceSliderWithLeadingArcCommits` and `TestPlaceSliderRejectsThreeNodesAtSameTime` (ordering still enforced).

- [ ] **Step 5: Commit**

Propose a commit via `mcp__nimbalyst__developer_git_commit_proposal` with:
- Files: `Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs`, `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs`
- Message: `feat: commit zero-duration sliders from the slider tool`

---

### Task 4: Render + select the zero-duration slider (remove the bail-outs)

Remove the three `duration <= 0` early-returns so the committed slider draws its horizontal line and is selectable. The node-handle `y` computation must gain the same zero-duration guard as `Build` (it divides by `duration` directly). Hit-testing already delegates to the outline paths, the head marker, and node handles (`ReceivePositionalInputAt`, line 245) — none depend on the blueprint's zero-height quad — so a horizontal outline (`outline_radius = 8`) plus fixed-size handles are grabbable.

**Files:**
- Modify: `Garbus.Game/Edit/Drawables/SliderPolylineVisual.cs:152-159`
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs:117-125` (remove bail) and `:163` (guard y)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` (add one test + one helper)

**Interfaces:**
- Consumes: `EditorSliderPolyline.Build` (Task 2, zero-duration-safe); `SliderSelectionBlueprint.ReceivePositionalInputAt` (outline + head + handles); test helpers `positionAtAngle`, `settleWith`, `placedObject<T>`, `sliderEndsScreen` (all existing in `TestSceneComposeSelection`).

- [ ] **Step 1: Write the failing integration test**

In `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`, add a placement helper next to `placeDiagonalSlider` (after line 740):

```csharp
        /// <summary>Places a zero-duration slider: head and one node at the SAME time (yFrac), different angle.</summary>
        private void placeZeroDurationSlider()
        {
            AddStep("select slider tool", () => input.Key(Key.Number8));
            AddStep("move to body start", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("click body", () => input.Click(MouseButton.Left));
            // node at the SAME yFrac → same snapped time (offset 0), different angle → a horizontal arc.
            AddStep("move to node at head time", () => input.MoveMouseTo(positionAtAngle(315, 0.5f)));
            AddStep("click node", () => input.Click(MouseButton.Left));
            AddStep("right click to commit", () => input.Click(MouseButton.Right));
            AddAssert("zero-duration slider placed", () => placedObject<SliderBody>()?.Duration, () => Is.EqualTo(0.0));
            settleWith(() => placedObject<SliderBody>()!.StartTime);
            AddStep("switch to select tool", () => input.Key(Key.Number1));
        }
```

Then add the test (after `TestSliderSelectableOnlyOnPolylineAndNodes`, after line 1059):

```csharp
        [Test]
        public void TestZeroDurationSliderSelectableOnItsLine()
        {
            waitForComposer();
            placeZeroDurationSlider();

            // head and node share a time (the horizontal line), so both sliderEndsScreen points sit at the
            // same y; their midpoint lies on the rendered outline.
            AddStep("click the horizontal line", () =>
            {
                var (headScreen, nodeScreen) = sliderEndsScreen();
                input.MoveMouseTo((headScreen + nodeScreen) / 2);
                input.Click(MouseButton.Left);
            });
            AddAssert("zero-duration slider selected on its line",
                () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
        }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposeSelection.TestZeroDurationSliderSelectableOnItsLine"`
Expected: FAIL at "selected on its line" — `SliderSelectionBlueprint.Update` currently bails on `duration <= 0`, clearing the outline and handles, so there is nothing to click and selection stays empty. (Task 3 must be complete so placement commits; verify "zero-duration slider placed" passes.)

- [ ] **Step 3: Remove the render bail in `SliderPolylineVisual`**

In `Garbus.Game/Edit/Drawables/SliderPolylineVisual.cs`, replace `buildGeometry` (lines 152-159):

```csharp
    private void buildGeometry(float pxPerDeg, List<Vector2> polyline, List<Vector2> nodes)
    {
        // Duration may be 0 (a constant-radius arc at a single instant); EditorSliderPolyline.Build pins
        // every node to the bottom line in that case rather than dividing by zero.
        EditorSliderPolyline.Build(slider.Path.ControlPoints, pxPerDeg, DrawWidth / 2, DrawHeight, slider.Duration, polyline, nodes);
    }
```

- [ ] **Step 4: Remove the bail and guard the handle y in `SliderSelectionBlueprint`**

In `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`, replace the bail block (lines 117-125) with just the local:

```csharp
        double duration = HitObject.Duration;
```

Then replace the node-handle `y` line (originally line 163):

```csharp
            // duration 0 pins every node to the bottom line (the StartTime / judgement line).
            float y = duration > 0 ? DrawHeight * (float)(1 - cp.TimeOffset / duration) : DrawHeight;
```

- [ ] **Step 5: Run the new test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposeSelection.TestZeroDurationSliderSelectableOnItsLine"`
Expected: PASS.

- [ ] **Step 6: Run the full suite to check for regressions**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS — all tests green (baseline was 383 + the tests added by this plan). Pay attention to the other `TestSceneComposeSelection` slider tests (node drag, T-insert, path-precise selection) still passing.

- [ ] **Step 7: Commit**

Propose a commit via `mcp__nimbalyst__developer_git_commit_proposal` with:
- Files: `Garbus.Game/Edit/Drawables/SliderPolylineVisual.cs`, `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`, `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`
- Message: `feat: render and select zero-duration sliders in compose`

---

## Self-Review

**Spec coverage:**
- Gate relaxation, uniform across all three paths → Task 1 (`AreTimesValid`, covering node-drag + T-insert) + Task 3 (placement). ✓
- Render the degenerate line instead of bailing → Task 2 (`EditorSliderPolyline.Build`) + Task 4 (`SliderPolylineVisual`, `SliderSelectionBlueprint`). ✓
- `EditorDrawableSliderBody` needs no change (height 0 is fine) → confirmed, not touched. ✓
- Timeline strip already renders a dot → confirmed, not touched. ✓
- Tests: `GarbusSliderPathTest` → Task 1; `TestSceneComposePlacement` flip → Task 3; `TestSceneComposeSelection` new selection test → Task 4; `EditorSliderPolylineTest` zero-duration Build → Task 2. ✓ (all four spec-listed test changes covered)
- Forward-compatibility (≥1-node floor pinned to two clauses; render degrades at a single node) → Task 1 + Task 3 hold the floor; Task 2's `Build` already emits a single vertex (no link) for a lone head, so the render path degrades safely. ✓
- Out of scope (gameplay catch model, format, slams) → not touched by any task. ✓

**Placeholder scan:** No TBD/TODO/vague steps; every code step shows complete code and every run step shows an exact command + expected result. ✓

**Type consistency:** `AreTimesValid` / `AreTimesOrdered` signatures (`IReadOnlyList<double> → bool`) consistent across tasks; `EditorSliderPolyline.Build` signature unchanged (only internal `toPoint` logic changes); test helpers (`positionAtAngle`, `settleWith`, `sliderEndsScreen`, `placedObject<T>`) match their existing definitions in the referenced test files. ✓
