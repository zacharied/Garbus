# Head-only (zero-child) sliders — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the editor author a slider with zero control points (a slider that is just its head) via `Ctrl`+left-click, keep its gameplay judgement unchanged, and render its head as a display circle so it is visible during play.

**Architecture:** Three isolated changes. (1) `SliderPlacementBlueprint` gains a one-shot `committingHeadOnly` flag: a `Ctrl`+left-click begins and immediately commits a node-less placement, while right-click and tool-switch commits still require ≥1 node (so head-only sliders can never be created by accident). (2) `DrawableSliderBody` renders its single head node as a filled circle (radius = the body line's half-thickness) when the path has no links. (3) The remaining editor behaviours (selection, delete, T-insert promotion) already work against a zero-node path and are pinned by tests. `GarbusSliderPath.AreTimesValid` is deliberately **not** touched — it is never validated against an empty offset list.

**Tech Stack:** C# / .NET 8, osu-framework, NUnit (headless visual test scenes).

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL-initialised fields use `= null!`.
- Garbus port repo; integration branch is `master`. No backwards-compat layers, no version bumps, no format changes.
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- Gameplay **judgement** is unchanged: `DrawableSliderHead` keeps its auto-`ApplyMaxResult` stub; the head-only work is display-only in gameplay.
- Commit via the Nimbalyst commit proposal tool (`mcp__nimbalyst__developer_git_commit_proposal`), not `git commit` on the command line — this is a shared worktree. Stage exactly the files listed in each task's Commit step.

**Spec:** `docs/superpowers/specs/2026-07-17-head-only-sliders-design.md`

---

### Task 1: `Ctrl`+left-click placement of a head-only slider

A `Ctrl`+left-click with the slider tool (in the `Waiting` state) commits a zero-control-point slider in one click. A plain left-click still starts a normal multi-click slider; a node-less right-click still cancels; and the tool-switch auto-commit (`ComposeBlueprintContainer` calls `EndPlacement(PlacementActive == Active)`) still discards a node-less placement. The head-only path is the only one that treats a node-less path as valid, gated by a one-shot flag.

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs` (class doc `:24-29`; `IsValidForPlacement` `:43-44`; `OnMouseDown` `:81-99`)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs` (add three tests after `TestPlaceSliderZeroDurationCommits`, line 311)

**Interfaces:**
- Produces: `SliderPlacementBlueprint` — after this task, a `Ctrl`+left-click while `PlacementActive == Waiting` commits a `SliderBody` with `Path.ControlPoints.Count == 0` and `Duration == 0`.

- [ ] **Step 1: Write the failing test**

In `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs`, add after line 311 (the close of `TestPlaceSliderZeroDurationCommits`):

```csharp
        [Test]
        public void TestCtrlClickPlacesHeadOnlySlider()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(Key.Number8));
            AddStep("move to head", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("ctrl+left-click", () =>
            {
                input.PressKey(Key.LControl);
                input.Click(MouseButton.Left);
                input.ReleaseKey(Key.LControl);
            });

            AddAssert("slider placed", () => placedObject<SliderBody>() != null);
            AddAssert("zero control points",
                () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(0));
            AddAssert("zero duration", () => placedObject<SliderBody>()!.Duration, () => Is.EqualTo(0.0));
            AddAssert("head at 270", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
        }

        [Test]
        public void TestPlainRightClickDoesNotPlaceHeadOnly()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(Key.Number8));
            AddStep("move to head", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("plain left-click (start)", () => input.Click(MouseButton.Left));
            AddStep("right-click with no nodes", () => input.Click(MouseButton.Right));

            AddAssert("no slider placed", () => placedObject<SliderBody>() == null);
        }

        [Test]
        public void TestToolSwitchDoesNotCommitHeadOnly()
        {
            waitForComposer();
            AddStep("select slider tool", () => input.Key(Key.Number8));
            AddStep("move to head", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("plain left-click (start)", () => input.Click(MouseButton.Left));
            AddStep("switch to select tool (auto-commit path)", () => input.Key(Key.Number1));

            AddAssert("no slider placed", () => placedObject<SliderBody>() == null);
        }
```

- [ ] **Step 2: Run the tests to verify the head-only one fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposePlacement.TestCtrlClickPlacesHeadOnlySlider"`
Expected: FAIL at "slider placed" — a `Ctrl`+left-click currently just runs the plain-left-click path (`BeginPlacement`), leaving an uncommitted 0-node placement, so `placedObject<SliderBody>()` is `null`.

(The two guard tests already pass — they pin behaviour that must not regress. Run them too to confirm the starting state:
`dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposePlacement.TestPlainRightClickDoesNotPlaceHeadOnly|FullyQualifiedName~TestSceneComposePlacement.TestToolSwitchDoesNotCommitHeadOnly"` → PASS.)

- [ ] **Step 3: Add the one-shot flag and relax the gate**

In `Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs`, replace `IsValidForPlacement` (lines 43-44):

```csharp
    // Set only by the Ctrl+left-click head-only path (below), so that path is the ONLY one which treats
    // a node-less path as committable. Right-click and the tool-switch auto-commit still require ≥1 node.
    private bool committingHeadOnly;

    protected override bool IsValidForPlacement =>
        base.IsValidForPlacement && (HitObject.Path.ControlPoints.Count > 0 || committingHeadOnly);
```

- [ ] **Step 4: Commit the head-only slider on `Ctrl`+left-click**

In the same file, replace `OnMouseDown` (lines 81-99):

```csharp
    protected override bool OnMouseDown(MouseDownEvent e)
    {
        switch (e.Button)
        {
            case MouseButton.Left:
                if (PlacementActive == PlacementState.Waiting)
                {
                    BeginPlacement(true);

                    // Ctrl+left-click drops a head-only slider (zero control points) in one click. The
                    // head's angle/time were already snapped to the cursor while Waiting, so commit
                    // immediately. The one-shot flag makes this the only path that accepts a node-less
                    // path, so a plain right-click or a tool-switch auto-commit cannot create one.
                    if (e.ControlPressed)
                    {
                        committingHeadOnly = true;
                        EndPlacement(true);
                    }
                }
                else
                    tryAddNode();
                return true;

            case MouseButton.Right:
                if (PlacementActive == PlacementState.Active)
                    EndPlacement(HitObject.Path.ControlPoints.Count > 0);
                return true;
        }

        return false;
    }
```

- [ ] **Step 5: Update the class doc**

In the same file, replace the class-doc summary (lines 24-29):

```csharp
/// <summary>
/// Multi-click slider placement: the first left click sets the body (start time + angle), each further
/// left click appends a control-point node at the snapped cursor (which must be later in time than the
/// previous node), and a right click commits — requiring at least one node, per the format's contract.
/// A rubber-band segment previews the next node at the cursor.
///
/// A <c>Ctrl</c>+left-click on the first click instead commits a head-only slider (zero control points)
/// immediately, without entering the multi-click flow.
/// </summary>
```

- [ ] **Step 6: Run the placement suite to verify pass + no regressions**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposePlacement"`
Expected: PASS — the three new tests plus every existing placement test, notably `TestSliderNodeAtEarlierTimeRejected` (node-less right-click still discards) and `TestPlaceSliderZeroDurationCommits` (a 1-node zero-duration slider still commits via right-click).

- [ ] **Step 7: Commit**

Propose a commit via `mcp__nimbalyst__developer_git_commit_proposal`:
- Files: `Garbus.Game/Edit/Blueprints/SliderPlacementBlueprint.cs`, `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs`
- Message: `feat: place head-only sliders with Ctrl+left-click`

---

### Task 2: Gameplay display circle for a head-only slider

`DrawableSliderBody.updatePath` draws nothing when the path has no links (`nodeTimes.Length < 2`). Render the single head node as a filled circle of the body's own line radius (`Thickness / 2`), travelling centre→ring like a body head, hosted in a fade-managed container so it fades/tints with the object. Judgement is untouched.

**Files:**
- Modify: `Garbus.Game/Objects/Drawables/DrawableSliderBody.cs` (fields near `:126`; ctor `:149-158`; `load` `:160-181`; `PrepareForUse` `:189-194`; `updatePath` `:244-301`; `UpdateHitStateTransforms` `:485-505`)
- Test: `Garbus.Game.Tests/Visual/TestSceneGameplay.cs` (add one test after `TestUnsampledCatchWindowGrantsHit`, line 302)

**Interfaces:**
- Consumes: `SliderPlacementBlueprint` head-only sliders are not needed here — the test constructs a `SliderBody` with an empty `ControlPoints` list directly.
- Produces: a `DrawableSliderBody` for a zero-control-point slider now contains a visible `osu.Framework.Graphics.Shapes.Circle` (the `headCircle`) with `Alpha > 0` and non-zero draw size while the head is in the visible radial band.

- [ ] **Step 1: Write the failing test**

In `Garbus.Game.Tests/Visual/TestSceneGameplay.cs`, add after line 302 (the close of `TestUnsampledCatchWindowGrantsHit`):

```csharp
        [Test]
        public void TestHeadOnlySliderDisplaysCircle()
        {
            Objects.Drawables.DrawableSliderBody body = null!;

            // A slider with ZERO control points — just its head. It renders no path line, so it must show
            // a circle (body-line radius) to stay visible. StartTime 5050 sits off the playThrough grid.
            AddStep("add head-only slider", () =>
            {
                var slider = new SliderBody
                {
                    StartTime = 5050,
                    AngleDeg = 0,
                    Side = HorizontalDirection.Left,
                    Path = new GarbusPath
                    {
                        ControlPoints = new osu.Framework.Bindables.BindableList<GarbusPathControlPoint>(),
                    },
                };
                slider.ApplyDefaults();
                playfield.Add(PlayScreen.CreateDrawableRepresentation(slider));
            });

            AddUntilStep("head-only slider body present", () =>
            {
                body = playfield.AllHitObjects
                                .OfType<Objects.Drawables.DrawableSliderBody>()
                                .FirstOrDefault(b => b.HitObject.StartTime == 5050)!;
                return body != null;
            });

            // Walk the clock up to just before StartTime; the head must have emerged and be visible as a
            // circle (Alpha > 0, non-zero size) BEFORE it reaches the ring and auto-hits.
            AddUntilStep("head circle visible before judgement", () =>
            {
                manualClock.CurrentTime = Math.Min(5000, manualClock.CurrentTime + 50);
                var circle = body.ChildrenOfType<osu.Framework.Graphics.Shapes.Circle>().FirstOrDefault();
                return circle != null && circle.Alpha > 0 && circle.DrawWidth > 0 && manualClock.CurrentTime >= 5000;
            });

            // Judgement is unchanged: the head still auto-passes once its time arrives.
            playThrough(6000);
            AddUntilStep("head judged", () => body.NestedHitObjects
                                                  .OfType<Objects.Drawables.DrawableSliderHead>()
                                                  .All(h => h.Judged));
            AddAssert("head hit (max result)", () => body.NestedHitObjects
                                                         .OfType<Objects.Drawables.DrawableSliderHead>()
                                                         .All(h => h.IsHit));
        }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneGameplay.TestHeadOnlySliderDisplaysCircle"`
Expected: FAIL at "head circle visible before judgement" (times out) — `updatePath` currently draws nothing for a single-node path, so there is no `Circle` child.

- [ ] **Step 3: Add the head-circle fields**

In `Garbus.Game/Objects/Drawables/DrawableSliderBody.cs`, add these fields immediately after the `nestedContainer` field (after line 129):

```csharp
    // A head-only slider (no control points) has no line to draw; render its single node as a filled
    // circle of the body's own line radius so it stays visible. Wrapped in a fade-managed container
    // (like pathContainer) so it fades/tints as a unit, while the circle carries per-frame band alpha.
    private readonly Container headContainer = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    private readonly Circle headCircle = new()
    {
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        Alpha = 0,
    };
```

- [ ] **Step 4: Wire the container in the constructor**

In the same file, in the constructor, add after `tipBox.Colour = sideColour;` (line 157):

```csharp
        headContainer.Colour = sideColour;
        headContainer.Add(headCircle);
```

- [ ] **Step 5: Add the container to the tree and size the circle**

In `load()`, add after `AddInternal(tipBox);` (line 178):

```csharp
        headCircle.Size = new Vector2(Thickness);
        AddInternal(headContainer);
```

- [ ] **Step 6: Fade the container in with the rest**

In `PrepareForUse()`, add after `escapeContainer.FadeInFromZero(100, Easing.In);` (line 193):

```csharp
        headContainer.FadeInFromZero(100, Easing.In);
```

- [ ] **Step 7: Render the head circle each frame**

In the same file, add a call to the new helper as the first line of `updatePath()` (before `int bodyIndex = 0;`, line 246):

```csharp
        updateHeadCircle();
```

Then add this method immediately after `updatePath()` (after its closing brace, line 301):

```csharp
    /// <summary>
    /// A head-only slider (no control points) has no path to render; show its single node as a filled
    /// circle of the body's own line radius, travelling centre→ring like a body head would. Hidden for
    /// any slider that has a real path (its line already draws the head).
    /// </summary>
    private void updateHeadCircle()
    {
        if (nodeTimes.Length >= 2)
        {
            headCircle.Alpha = 0;
            return;
        }

        float ringRadius = scrollingContainer.ScrollLength;
        float r = scrollingContainer.DistanceFromCentreAtTime(nodeTimes[0]);

        if (r >= 0 && r <= ringRadius)
        {
            headCircle.Position = polarToCartesian(nodeRadians[0], r);
            headCircle.Alpha = 1;
        }
        else
            headCircle.Alpha = 0;
    }
```

(`rebuildNodes` always produces at least the head node, so `nodeTimes[0]` / `nodeRadians[0]` are always valid.)

- [ ] **Step 8: Fade the head circle on hit/miss**

In `UpdateHitStateTransforms(ArmedState state)`, add `headContainer` to both cases. Replace the two `case` blocks (lines 491-504):

```csharp
            case ArmedState.Hit:
                escapeContainer.FadeOut(350, Easing.OutQuint);
                tipBox.FadeOut(350, Easing.OutQuint);
                headContainer.FadeOut(350, Easing.OutQuint);
                pathContainer.FadeOut(350, Easing.OutQuint).OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                pathContainer.FadeColour(Colour4.Red, duration);
                escapeContainer.FadeColour(Colour4.Red, duration);
                escapeContainer.FadeOut(duration, Easing.InQuint);
                tipBox.FadeOut(duration, Easing.InQuint);
                headContainer.FadeColour(Colour4.Red, duration);
                headContainer.FadeOut(duration, Easing.InQuint);
                pathContainer.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
```

(`Expire()` stays chained off `pathContainer`'s fade, which runs for a head-only slider even though it has no visible paths — so the body still expires.)

- [ ] **Step 9: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneGameplay.TestHeadOnlySliderDisplaysCircle"`
Expected: PASS.

- [ ] **Step 10: Run the gameplay suite to check for regressions**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneGameplay"`
Expected: PASS — every existing gameplay test still green (normal sliders keep `headCircle.Alpha == 0`, so their visuals are unchanged).

- [ ] **Step 11: Commit**

Propose a commit via `mcp__nimbalyst__developer_git_commit_proposal`:
- Files: `Garbus.Game/Objects/Drawables/DrawableSliderBody.cs`, `Garbus.Game.Tests/Visual/TestSceneGameplay.cs`
- Message: `feat: render a head-only slider as a display circle`

---

### Task 3: Editor selection, delete, and T-insert promotion

A head-only slider must be selectable, deletable, and promotable to a one-node slider via `T`. These should already work: selection reduces to the `head` `EditSquarePiece` (already in `ReceivePositionalInputAt`), delete routes through `SelectionHandler`, and T-insert produces a 1-node path that passes the *unchanged* `AreTimesValid` (count 1 > 0). This task **verifies** that with tests. The selection test is the one that pins the zero-height-parent risk; the plan includes a concrete fix to apply only if it fails.

**Files:**
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` (add two helpers after `sliderEndsScreen`, line 771; add three tests after `TestZeroDurationSliderSelectableOnItsLine`, line 1092)
- Modify **only if the selection test fails**: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`

**Interfaces:**
- Consumes: `SliderPlacementBlueprint`'s `Ctrl`+left-click head-only placement (Task 1); existing helpers `positionAtAngle`, `settleWith`, `placedObject<T>`, `sliderBlueprint()`, `editorChart`, `editorClock`, `playfield`, `input`.

- [ ] **Step 1: Add the head-only placement + head-position helpers**

In `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`, add after `sliderEndsScreen()` (after line 771):

```csharp
        /// <summary>Places a head-only slider (zero control points) via Ctrl+left-click, then selects the select tool.</summary>
        private void placeHeadOnlySlider()
        {
            AddStep("select slider tool", () => input.Key(Key.Number8));
            AddStep("move to head", () => input.MoveMouseTo(positionAtAngle(270, 0.5f)));
            AddStep("ctrl+left-click to place head-only", () =>
            {
                input.PressKey(Key.LControl);
                input.Click(MouseButton.Left);
                input.ReleaseKey(Key.LControl);
            });
            AddAssert("head-only slider placed",
                () => placedObject<SliderBody>()?.Path.ControlPoints.Count, () => Is.EqualTo(0));
            settleWith(() => placedObject<SliderBody>()!.StartTime);
            AddStep("switch to select tool", () => input.Key(Key.Number1));
        }

        /// <summary>Screen position of a head-only slider's head (its angle column, at StartTime).</summary>
        private Vector2 headScreen()
        {
            var slider = placedObject<SliderBody>()!;
            var container = playfield.HitObjectContainer;

            Vector2 p = container.ScreenSpacePositionAtTime(slider.StartTime);
            p.X = container.ToScreenSpace(new Vector2(EditorAngleMapping.ToX(slider.AngleDeg) * container.DrawWidth, 0)).X;
            return p;
        }
```

- [ ] **Step 2: Write the selection / delete / promote tests**

In the same file, add after `TestZeroDurationSliderSelectableOnItsLine` (after line 1092):

```csharp
        [Test]
        public void TestHeadOnlySliderSelectableOnHead()
        {
            waitForComposer();
            placeHeadOnlySlider();

            AddStep("click the head", () =>
            {
                input.MoveMouseTo(headScreen());
                input.Click(MouseButton.Left);
            });
            AddAssert("head-only slider selected",
                () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
        }

        [Test]
        public void TestHeadOnlySliderDeletable()
        {
            waitForComposer();
            placeHeadOnlySlider();

            AddStep("click the head", () =>
            {
                input.MoveMouseTo(headScreen());
                input.Click(MouseButton.Left);
            });
            AddAssert("selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());

            AddStep("press Delete", () => input.Key(Key.Delete));
            AddAssert("slider removed", () => placedObject<SliderBody>() == null);
        }

        [Test]
        public void TestTPromotesHeadOnlyToOneNode()
        {
            waitForComposer();
            placeHeadOnlySlider();

            AddStep("click the head", () =>
            {
                input.MoveMouseTo(headScreen());
                input.Click(MouseButton.Left);
            });
            AddAssert("selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());

            // Move the cursor to a LATER time and a different angle, then press T to insert the first node.
            AddStep("move cursor later + new angle", () =>
            {
                var slider = placedObject<SliderBody>()!;
                var container = playfield.HitObjectContainer;
                var screen = container.ScreenSpacePositionAtTime(slider.StartTime + 250);
                screen.X = positionAtAngle(315).X;
                input.MoveMouseTo(screen);
            });
            AddStep("press T", () => input.Key(Key.T));
            AddAssert("promoted to one node",
                () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));
            AddAssert("node later than head",
                () => placedObject<SliderBody>()!.Path.ControlPoints[0].TimeOffset, () => Is.GreaterThan(0.0));
        }
```

- [ ] **Step 3: Run the three tests**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposeSelection.TestHeadOnlySlider|FullyQualifiedName~TestSceneComposeSelection.TestTPromotesHeadOnly"`
Expected: PASS — the head piece already backs selection (`SliderSelectionBlueprint.ReceivePositionalInputAt` delegates to `head.ReceivePositionalInputAt`, which has a real `NOTE_SIZE`-tall quad independent of the blueprint's zero `DrawHeight`), delete routes through `SelectionHandler`, and T-insert yields a valid 1-node path.

- [ ] **Step 4 (contingency — ONLY if `TestHeadOnlySliderSelectableOnHead` failed in Step 3): make the head piece hit-test independent of the collapsed box**

If (and only if) the selection assert failed, the zero-height parent is suppressing the head piece's positional input. Fix it by giving the head piece an explicit, `DrawHeight`-independent hit area. In `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`, in `ReceivePositionalInputAt` (lines 231-251), replace the head check (lines 241-242):

```csharp
        // The head marker backs head-only selection. Its own quad (a fixed NOTE_SIZE square around the
        // StartTime line) is what we hit-test, so a zero-height blueprint box does not suppress it.
        if (head.ReceivePositionalInputAt(screenSpacePos))
            return true;
```

If the head child itself is being culled, additionally set `head.AlwaysPresent = true;` in `load()` where `head` is constructed (lines 89-95). Re-run Step 3 until green. **Do not apply this step if Step 3 already passed** — it is dead code in that case.

- [ ] **Step 5: Run the full `TestSceneComposeSelection` suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposeSelection"`
Expected: PASS — the new tests plus every existing selection test (node drag, T-insert, path-precise selection, zero-duration selection) still green.

- [ ] **Step 6: Run the whole test suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS — all tests green.

- [ ] **Step 7: Commit**

Propose a commit via `mcp__nimbalyst__developer_git_commit_proposal`:
- Files: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` (and `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs` **only if** Step 4 was applied)
- Message: `test: cover head-only slider selection, delete, and T-insert`
  (If Step 4 was applied, use `feat: select head-only sliders by their head marker` instead.)

---

## Self-Review

**Spec coverage:**
- `Ctrl`+left-click placement, right-click stays a cancel, tool-switch guard → Task 1. ✓
- `IsValidForPlacement` relaxed only via one-shot flag → Task 1, Step 3. ✓
- `AreTimesValid` not touched → confirmed; no task modifies `GarbusSliderPath.cs`, and `GarbusSliderPathTest` is untouched. ✓
- Gameplay display circle (radius `Thickness/2`, centre→ring, side-tinted, fades on hit/miss, head-only case only, judgement unchanged) → Task 2. ✓
- Editor selection/delete/T-insert promotion (verify; fix only if needed) → Task 3. ✓
- "What already degrades gracefully" (model, serializer, inspector, timeline) → relied upon, not modified; the placement + gameplay + selection tests exercise the end-to-end path. ✓
- Out of scope (genuine head judgement, head cap on normal sliders, format change, right-click behaviour change) → no task touches these. ✓

**Placeholder scan:** Every code step shows complete code; every run step shows an exact command + expected result. The one conditional step (Task 3, Step 4) is explicitly gated on an observed test failure and carries complete fix code, not a TODO. ✓

**Type consistency:** `placedObject<SliderBody>()`, `Path.ControlPoints.Count`, `Duration`, `AngleDeg`, `positionAtAngle`, `settleWith`, `headScreen`, `placeHeadOnlySlider` all match the existing test-scene definitions and the `SliderBody` model. `headContainer`/`headCircle`/`updateHeadCircle` are introduced in Task 2 and referenced only within that task. `committingHeadOnly`/`IsValidForPlacement`/`OnMouseDown` signatures match `SliderPlacementBlueprint`. ✓
