# Editor Flip Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two right-click flip actions to the compose editor — an interactive "Flip around angle…" pivot bar and a one-shot "Flip selection" about the selection's angular bounding-box centre — that mirror hit objects (and individually-selected slider nodes).

**Architecture:** One modular reflection primitive `Flip(int sumDeg)` on `GarbusSelectionHandler` reflects every selected "handle" (`θ → NormalizeDeg(sumDeg − θ)`, `sumDeg = 2·pivot`). Handles come from the global selection: point objects contribute their angle; a slider with selected nodes contributes only those nodes; a whole-selected slider contributes head + all nodes and mirrors rigidly. "Flip selection" derives `sumDeg` from the handle set's seam-robust angular centre (`EditorAngleMapping.ReflectionSum`); "Flip around angle…" derives it from a cursor-driven `FlipPivotOverlay`.

**Tech Stack:** C# / .NET, osu-framework, NUnit headless visual test scenes.

## Global Constraints

- Nullability enabled solution-wide; DI/BDL fields use `= null!`.
- No backwards-compat layers, no version bumps (experimental project).
- Vendored-file attribution headers unaffected (no vendored files change here).
- Terminology: "chart" not "beatmap"; `Garbus*` prefixes.
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- Reflection is angle-only — **time is never modified**.
- Angle convention: CCW positive, θ=0 East / 90 North / 180 West / 270 South. Reflection about pivot φ is `θ → 2φ − θ`, purely modular on the circle.

---

## File structure

- **Modify** `Garbus.Game/Edit/EditorAngleMapping.cs` — add `ReflectionSum(IEnumerable<int>)` (seam-robust bbox centre → reflection sum).
- **Modify** `Garbus.Game/Edit/GarbusSelectionHandler.cs` — node-aware `Flip(int)`, `handleAngles()`, `ComputeSelectionReflectionSum()`, and the two context-menu items.
- **Create** `Garbus.Game/Edit/FlipPivotOverlay.cs` — transient full-playfield overlay drawing the snapped pivot bar and committing/cancelling.
- **Modify** `Garbus.Game/Edit/GarbusHitObjectComposer.cs` — host the overlay in `PlayfieldContentContainer`; add `BeginFlipAroundAngle(Action<int>)`.
- **Modify** `Garbus.Game.Tests/Editor/TestEditorAngleMapping.cs` — `ReflectionSum` unit tests.
- **Modify** `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` — flip integration tests + helpers.

No changes to `SliderSelectionBlueprint` (its `internal SelectedNodes` is already exposed and the handler is in the same assembly).

---

## Task 1: `EditorAngleMapping.ReflectionSum`

Pure, seam-robust largest-gap centre. This is the mathematical core of "Flip selection" and is unit-tested in isolation.

**Files:**
- Modify: `Garbus.Game/Edit/EditorAngleMapping.cs`
- Test: `Garbus.Game.Tests/Editor/TestEditorAngleMapping.cs`

**Interfaces:**
- Produces: `public static int EditorAngleMapping.ReflectionSum(IEnumerable<int> angles)` — returns `2·φ` normalised to `[0,360)`, where `φ` is the centre of the tightest angular arc covering `angles`. Returns `0` for empty input.

- [ ] **Step 1: Write the failing tests**

Add to `TestEditorAngleMapping.cs` (inside the class):

```csharp
// --- ReflectionSum ---

[Test]
public void TestReflectionSumSingleAngleIsSelfMirror()
{
    // A lone handle mirrors about itself: sum = 2θ, so NormalizeDeg(sum − θ) == θ.
    Assert.That(EditorAngleMapping.ReflectionSum(new[] { 90 }), Is.EqualTo(180));
}

[Test]
public void TestReflectionSumSwapsAdjacentPair()
{
    // Centre of [60,120] is 90; reflecting swaps them.
    int sum = EditorAngleMapping.ReflectionSum(new[] { 60, 120 });
    Assert.That(sum, Is.EqualTo(180));
    Assert.That(EditorAngleMapping.NormalizeDeg(sum - 60), Is.EqualTo(120));
    Assert.That(EditorAngleMapping.NormalizeDeg(sum - 120), Is.EqualTo(60));
}

[Test]
public void TestReflectionSumUsesLargestGapNotNaiveMidpoint()
{
    // 0 and 90: the empty region is the 270° arc from 90 back to 0, so the covering arc is [0,90],
    // centre 45 — NOT the naive "average = 45" that only coincidentally matches here.
    Assert.That(EditorAngleMapping.ReflectionSum(new[] { 0, 90 }), Is.EqualTo(90));
}

[Test]
public void TestReflectionSumSeamStraddlingPairSwapsLocally()
{
    // 350 and 10 straddle the 0° seam: covering arc [350,370], centre 0. They swap about 0 — the
    // pivot is NOT the naive average (180) that would fling them across the circle.
    int sum = EditorAngleMapping.ReflectionSum(new[] { 350, 10 });
    Assert.That(sum, Is.EqualTo(0));
    Assert.That(EditorAngleMapping.NormalizeDeg(sum - 350), Is.EqualTo(10));
    Assert.That(EditorAngleMapping.NormalizeDeg(sum - 10), Is.EqualTo(350));
}

[Test]
public void TestReflectionSumEmptyIsZero()
{
    Assert.That(EditorAngleMapping.ReflectionSum(System.Array.Empty<int>()), Is.EqualTo(0));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestReflectionSum"`
Expected: FAIL — compile error, `ReflectionSum` does not exist.

- [ ] **Step 3: Implement `ReflectionSum`**

In `EditorAngleMapping.cs`, add `using System.Linq;` at the top (below `using System.Collections.Generic;`), then add this method inside the class (e.g. after `SnapX`):

```csharp
/// <summary>
/// The reflection "sum" (<c>2·φ</c>, normalised to [0,360)) whose pivot <c>φ</c> is the centre of the
/// tightest angular arc covering <paramref name="angles"/> — the seam-robust bounding-box centre.
/// Reflecting an angle <c>θ</c> by this sum is <c>NormalizeDeg(sum − θ)</c>. Returns 0 for empty input
/// (the identity reflection about East). The covering arc is found as the complement of the largest
/// circular gap between the sorted angles, so a selection straddling the wrap seam mirrors in place.
/// </summary>
public static int ReflectionSum(IEnumerable<int> angles)
{
    var sorted = angles.Select(NormalizeDeg).Distinct().OrderBy(a => a).ToList();

    if (sorted.Count == 0)
        return 0;
    if (sorted.Count == 1)
        return NormalizeDeg(2 * sorted[0]);

    // Largest circular gap → the covering arc is everything else, starting just after that gap.
    int gapStart = 0;
    int largestGap = 360 - sorted[^1] + sorted[0]; // wrap gap: last → first
    for (int i = 1; i < sorted.Count; i++)
    {
        int gap = sorted[i] - sorted[i - 1];
        if (gap > largestGap)
        {
            largestGap = gap;
            gapStart = i;
        }
    }

    // Unwrap the run starting after the gap into monotone values; min is the start, max the far end.
    int start = sorted[gapStart];
    int max = start;
    for (int i = 0; i < sorted.Count; i++)
    {
        int a = sorted[(gapStart + i) % sorted.Count];
        max = Math.Max(max, start + NormalizeDeg(a - start));
    }

    return NormalizeDeg(start + max);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestReflectionSum"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/EditorAngleMapping.cs Garbus.Game.Tests/Editor/TestEditorAngleMapping.cs
git commit -m "feat: add EditorAngleMapping.ReflectionSum for seam-robust flip pivot"
```

---

## Task 2: Node-aware `Flip` + "Flip selection" menu item

The reflection primitive and the automatic-pivot menu item. Reads `SelectedBlueprints` so slider node selection is visible; one `BeginChange`/`EndChange` transaction ⇒ single undo step.

**Files:**
- Modify: `Garbus.Game/Edit/GarbusSelectionHandler.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `EditorAngleMapping.ReflectionSum` (Task 1); `EditorAngleMapping.NormalizeDeg`, `MinimalDiff`; `EditorChart.BeginChange/EndChange/Update`; `SelectedBlueprints` (base); `SliderSelectionBlueprint.SelectedNodes`.
- Produces: `private void Flip(int sumDeg)` and `private int ComputeSelectionReflectionSum()` on `GarbusSelectionHandler`; a `GarbusMenuItem` titled exactly `"Flip selection"` yielded from `GetContextMenuItemsForSelection` for every selection.

- [ ] **Step 1: Write the failing tests**

Add these helpers to `TestSceneComposeSelection.cs` (near the other private helpers):

```csharp
/// <summary>A flip context-menu item by its exact label, or null (requires a selected blueprint hovered).</summary>
private GarbusMenuItem? flipMenuItem(string text)
{
    var handler = composer.ChildrenOfType<GarbusSelectionHandler>().Single();
    return handler.ContextMenuItems
                  .OfType<GarbusMenuItem>()
                  .FirstOrDefault(i => i.Text.Value.ToString() == text);
}

/// <summary>Places a slam-edge (instant placement) at an angle and returns to the select tool.</summary>
private void placeSlamEdgeAt(float angleDeg)
{
    AddStep("select slam-edge tool", () => input.Key(Key.Number6));
    AddStep("place slam edge", () =>
    {
        input.MoveMouseTo(positionAtAngle(angleDeg, 0.5f));
        input.Click(MouseButton.Left);
    });
    AddAssert("slam edge placed", () => placedObject<GarbusSlamEdge>() != null);
    settleWith(() => placedObject<GarbusSlamEdge>()!.StartTime);
    AddStep("switch to select tool", () => input.Key(Key.Number1));
}
```

Add these tests:

```csharp
[Test]
public void TestFlipSelectionMirrorsWholeSlider()
{
    waitForComposer();
    placeDiagonalSlider(); // head South (270), one node East (0) → RotationOffset 90.
    selectSliderOnLine();

    AddAssert("head 270 before", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
    AddAssert("offset 90 before", () => firstControlPoint().RotationOffset, () => Is.EqualTo(90));

    GarbusMenuItem flip = null!;
    AddUntilStep("Flip selection available", () =>
    {
        var (headScreen, nodeScreen) = sliderEndsScreen();
        input.MoveMouseTo((headScreen + nodeScreen) / 2); // keep a selected blueprint hovered
        flip = flipMenuItem("Flip selection")!;
        return flip != null;
    });

    AddStep("invoke Flip selection", () => flip.Action.Value?.Invoke());
    // Rigid mirror about the swept-extent centre: head and node swap absolute angles, offset negates.
    AddAssert("head now 0", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(0));
    AddAssert("offset now -90", () => firstControlPoint().RotationOffset, () => Is.EqualTo(-90));

    AddStep("undo", () => changeHandler.RestoreState(-1));
    AddAssert("head restored", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
    AddAssert("offset restored", () => firstControlPoint().RotationOffset, () => Is.EqualTo(90));

    AddStep("redo", () => changeHandler.RestoreState(1));
    AddAssert("head re-flipped", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(0));
}

[Test]
public void TestFlipSelectionSingleNodeIsNoOp()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    AddStep("select node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
    AddAssert("one node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

    GarbusMenuItem flip = null!;
    AddUntilStep("Flip selection available", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(0)); // node handle keeps the slider blueprint hovered
        flip = flipMenuItem("Flip selection")!;
        return flip != null;
    });

    // Mirroring a single node about itself changes nothing: head fixed, offset unchanged.
    AddStep("invoke Flip selection", () => flip.Action.Value?.Invoke());
    AddAssert("head unchanged", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
    AddAssert("offset unchanged", () => firstControlPoint().RotationOffset, () => Is.EqualTo(90));
}

[Test]
public void TestFlipSelectionFlipsSlamEdgeDirection()
{
    waitForComposer();
    placeSlamEdgeAt(45);

    hoverThenClick(() => screenPositionOf(placedObject<GarbusSlamEdge>()!));
    AddAssert("slam edge selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(1));

    int angleBefore = 0;
    AddStep("snapshot angle", () => angleBefore = placedObject<GarbusSlamEdge>()!.AngleDeg);
    AddAssert("starts clockwise", () => placedObject<GarbusSlamEdge>()!.Direction, () => Is.EqualTo(RotationalDirection.Clockwise));

    GarbusMenuItem flip = null!;
    AddUntilStep("Flip selection available", () =>
    {
        input.MoveMouseTo(screenPositionOf(placedObject<GarbusSlamEdge>()!));
        flip = flipMenuItem("Flip selection")!;
        return flip != null;
    });

    AddStep("invoke Flip selection", () => flip.Action.Value?.Invoke());
    // Single object: angle unchanged (mirror about itself), handedness reverses.
    AddAssert("angle unchanged", () => placedObject<GarbusSlamEdge>()!.AngleDeg, () => Is.EqualTo(angleBefore));
    AddAssert("now anticlockwise", () => placedObject<GarbusSlamEdge>()!.Direction, () => Is.EqualTo(RotationalDirection.Anticlockwise));

    AddStep("undo", () => changeHandler.RestoreState(-1));
    AddAssert("clockwise again", () => placedObject<GarbusSlamEdge>()!.Direction, () => Is.EqualTo(RotationalDirection.Clockwise));
}

[Test]
public void TestFlipSelectionSwapsTwoNotes()
{
    waitForComposer();
    placeNoteAt(60);
    placeNoteAt(120);

    // Capture the two note instances by their starting angle so identity (not list order) proves the swap.
    CardinalNote note60 = null!, note120 = null!;
    AddStep("capture notes", () =>
    {
        var notes = editorChart.HitObjects.OfType<CardinalNote>().ToList();
        note60 = notes.Single(n => n.AngleDeg == 60);
        note120 = notes.Single(n => n.AngleDeg == 120);
    });

    AddStep("select all", () =>
    {
        input.PressKey(Key.LControl);
        input.Key(Key.A);
        input.ReleaseKey(Key.LControl);
    });
    AddAssert("two selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(2));

    GarbusMenuItem flip = null!;
    AddUntilStep("Flip selection available", () =>
    {
        input.MoveMouseTo(screenPositionOf(note120));
        flip = flipMenuItem("Flip selection")!;
        return flip != null;
    });

    // Handle set {60,120}, centre 90, sum 180: the two notes exchange angles. A pure swap preserves the
    // angle *set*, so identity (these specific instances) is what proves the flip — not a set comparison.
    AddStep("invoke Flip selection", () => flip.Action.Value?.Invoke());
    AddAssert("the 60 note is now 120", () => note60.AngleDeg, () => Is.EqualTo(120));
    AddAssert("the 120 note is now 60", () => note120.AngleDeg, () => Is.EqualTo(60));
}

[Test]
public void TestFlipMenuItemsPresentForNote()
{
    waitForComposer();
    placeNoteAt(270);
    hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
    AddAssert("note selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(1));

    AddAssert("both flip items present", () =>
    {
        input.MoveMouseTo(screenPositionOf(placedObject<CardinalNote>()!));
        return flipMenuItem("Flip selection") != null && flipMenuItem("Flip around angle...") != null;
    });
}
```

> `hoverThenClick` already switches to the select tool internally; `placeNoteAt` leaves the select tool active (see its definition). `placeNoteAt(120)` parks the clock on the second note, so both notes are on-screen for select-all.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestFlip"`
Expected: FAIL — `flipMenuItem` returns null (`GarbusMenuItem` "Flip selection"/"Flip around angle..." not yielded); `TestFlipMenuItemsPresentForNote` also fails on the "Flip around angle..." lookup (added in Task 3).

- [ ] **Step 3: Implement `Flip`, pivot computation, and the menu item**

In `GarbusSelectionHandler.cs`:

(a) Add the two flip items at the **start** of `GetContextMenuItemsForSelection`, before the existing `if (selection.All(...))` blocks:

```csharp
protected override IEnumerable<MenuItem> GetContextMenuItemsForSelection(IEnumerable<SelectionBlueprint<GarbusHitObject>> selection)
{
    yield return new GarbusMenuItem("Flip around angle...", MenuItemType.Standard,
        () => composer.BeginFlipAroundAngle(Flip));
    yield return new GarbusMenuItem("Flip selection", MenuItemType.Standard,
        () => Flip(ComputeSelectionReflectionSum()));

    if (selection.All(s => s.Item is GarbusSlamEdge))
    {
        // ...existing Anticlockwise item unchanged...
```

(Leave the rest of the method body exactly as it is.)

(b) Add these three members to the class (e.g. after `HandleMovement`):

```csharp
/// <summary>
/// Reflects every selected handle about the pivot encoded by <paramref name="sumDeg"/> (= 2·φ):
/// <c>θ → NormalizeDeg(sumDeg − θ)</c>. A true mirror — slider handedness reverses. A slider with
/// selected nodes reflects only those nodes (head anchored); a whole-selected slider mirrors rigidly.
/// One change transaction ⇒ a single undo step.
/// </summary>
private void Flip(int sumDeg)
{
    if (EditorChart.SelectedHitObjects.Count == 0)
        return;

    EditorChart.BeginChange();

    foreach (var blueprint in SelectedBlueprints)
    {
        var h = blueprint.Item;
        bool changed = true;

        switch (h)
        {
            case ShoulderNote shoulder:
                // No mutable angle: reflect its derived E/W angle, re-derive Side by hemisphere.
                int a = EditorAngleMapping.NormalizeDeg(sumDeg - shoulder.Side.ToAngleDeg());
                shoulder.Side = inEastHemisphere(a) ? HorizontalDirection.Right : HorizontalDirection.Left;
                break;

            case SliderBody slider when blueprint is SliderSelectionBlueprint sb && sb.SelectedNodes.Count > 0:
                // Node subset: head fixed, reflect each selected node's absolute angle, store minimal offset.
                foreach (var cp in sb.SelectedNodes)
                {
                    int abs = EditorAngleMapping.NormalizeDeg(slider.AngleDeg + cp.RotationOffset);
                    int newAbs = EditorAngleMapping.NormalizeDeg(sumDeg - abs);
                    cp.RotationOffset = EditorAngleMapping.MinimalDiff(slider.AngleDeg, newAbs);
                }
                break;

            case SliderBody slider:
                // Rigid whole-slider mirror: reflect the head, negate every offset (preserves winding).
                slider.AngleDeg = EditorAngleMapping.NormalizeDeg(sumDeg - slider.AngleDeg);
                foreach (var cp in slider.Path.ControlPoints)
                    cp.RotationOffset = -cp.RotationOffset;
                break;

            case IHasMutableAngle mutable:
                mutable.AngleDeg = EditorAngleMapping.NormalizeDeg(sumDeg - mutable.AngleDeg);
                if (h is GarbusSlamEdge slam)
                    slam.Direction = slam.Direction == RotationalDirection.Clockwise
                        ? RotationalDirection.Anticlockwise
                        : RotationalDirection.Clockwise;
                break;

            default:
                changed = false;
                break;
        }

        if (changed)
            EditorChart.Update(h);
    }

    EditorChart.EndChange();
}

/// <summary>The reflection sum whose pivot is the centre of the selection's handle-set angular bbox.</summary>
private int ComputeSelectionReflectionSum() => EditorAngleMapping.ReflectionSum(handleAngles());

/// <summary>
/// The angular "handles" the flip acts on: one per point object; per selected node for a slider with a
/// node selection; head + every node for a whole-selected slider.
/// </summary>
private IEnumerable<int> handleAngles()
{
    foreach (var blueprint in SelectedBlueprints)
    {
        switch (blueprint.Item)
        {
            case SliderBody slider when blueprint is SliderSelectionBlueprint sb && sb.SelectedNodes.Count > 0:
                foreach (var cp in sb.SelectedNodes)
                    yield return EditorAngleMapping.NormalizeDeg(slider.AngleDeg + cp.RotationOffset);
                break;

            case SliderBody slider:
                yield return EditorAngleMapping.NormalizeDeg(slider.AngleDeg);
                foreach (var cp in slider.Path.ControlPoints)
                    yield return EditorAngleMapping.NormalizeDeg(slider.AngleDeg + cp.RotationOffset);
                break;

            case IHasAngle angled:
                yield return EditorAngleMapping.NormalizeDeg(angled.AngleDeg);
                break;
        }
    }
}

/// <summary>East hemisphere (cos θ ≥ 0): the N/S ties (90°, 270°) resolve to East.</summary>
private static bool inEastHemisphere(int angleDeg)
{
    int a = EditorAngleMapping.NormalizeDeg(angleDeg);
    return a <= 90 || a >= 270;
}
```

> `BeginFlipAroundAngle` is added to the composer in Task 3. Until then the `"Flip around angle..."` action references a method that does not exist yet — **add a temporary stub** so this task compiles: in `GarbusHitObjectComposer.cs` add `public void BeginFlipAroundAngle(System.Action<int> onCommit) { }` (empty). Task 3 replaces the body. (This keeps Task 2 independently buildable/testable.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestFlipSelection"`
Expected: PASS (`TestFlipSelectionMirrorsWholeSlider`, `TestFlipSelectionSingleNodeIsNoOp`, `TestFlipSelectionFlipsSlamEdgeDirection`, `TestFlipSelectionSwapsTwoNotes`).

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestFlipMenuItemsPresentForNote"`
Expected: PASS (both items present — the stub yields "Flip around angle..." successfully).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/GarbusSelectionHandler.cs Garbus.Game/Edit/GarbusHitObjectComposer.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: node-aware Flip primitive and Flip selection menu item"
```

---

## Task 3: `FlipPivotOverlay` + interactive "Flip around angle…"

The cursor-driven pivot bar and the composer hook that drives it. Replaces the Task 2 stub.

**Files:**
- Create: `Garbus.Game/Edit/FlipPivotOverlay.cs`
- Modify: `Garbus.Game/Edit/GarbusHitObjectComposer.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `GarbusSelectionHandler.Flip` (via the callback passed to `BeginFlipAroundAngle`); `EditorAngleMapping.SnapX`, `NormalizeDeg`; `GarbusHitObjectComposer.AngleSnap`, `PlayfieldContentContainer`.
- Produces: `public void GarbusHitObjectComposer.BeginFlipAroundAngle(Action<int> onCommit)`; `public partial class FlipPivotOverlay` with `Begin(Action<int> commit)`.

- [ ] **Step 1: Write the failing tests**

Add to `TestSceneComposeSelection.cs`:

```csharp
[Test]
public void TestFlipAroundAngleReflectsNote()
{
    waitForComposer();
    placeNoteAt(90); // North
    hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));
    AddAssert("note selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(1));

    GarbusMenuItem flip = null!;
    AddUntilStep("Flip around angle available", () =>
    {
        input.MoveMouseTo(screenPositionOf(placedObject<CardinalNote>()!));
        flip = flipMenuItem("Flip around angle...")!;
        return flip != null;
    });

    AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
    AddStep("move pivot bar to East (0°)", () => input.MoveMouseTo(positionAtAngle(0, 0.5f)));
    AddStep("click to commit", () => input.Click(MouseButton.Left));

    // Reflect 90 about pivot 0 (sum 0) → 270 (South).
    AddAssert("note now South (270)", () => placedObject<CardinalNote>()!.AngleDeg, () => Is.EqualTo(270));
}

[Test]
public void TestFlipAroundAngleEscapeCancels()
{
    waitForComposer();
    placeNoteAt(90);
    hoverThenClick(() => screenPositionOf(placedObject<CardinalNote>()!));

    GarbusMenuItem flip = null!;
    AddUntilStep("Flip around angle available", () =>
    {
        input.MoveMouseTo(screenPositionOf(placedObject<CardinalNote>()!));
        flip = flipMenuItem("Flip around angle...")!;
        return flip != null;
    });

    AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
    AddStep("move pivot bar to East", () => input.MoveMouseTo(positionAtAngle(0, 0.5f)));
    AddStep("press Escape", () => input.Key(Key.Escape));
    AddStep("click where the bar was", () => input.Click(MouseButton.Left));

    AddAssert("note unchanged (still 90)", () => placedObject<CardinalNote>()!.AngleDeg, () => Is.EqualTo(90));
}

[Test]
public void TestFlipAroundAngleReflectsSelectedNode()
{
    waitForComposer();
    placeDiagonalSlider(); // head 270, node 0 at absolute 0 (offset 90)
    selectSliderOnLine();
    AddStep("select node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
    AddAssert("one node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

    GarbusMenuItem flip = null!;
    AddUntilStep("Flip around angle available", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(0));
        flip = flipMenuItem("Flip around angle...")!;
        return flip != null;
    });

    AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
    AddStep("move pivot bar to North (90°)", () => input.MoveMouseTo(positionAtAngle(90, 0.5f)));
    AddStep("click to commit", () => input.Click(MouseButton.Left));

    // Node abs 0 reflected about pivot 90 (sum 180) → abs 180; offset from head 270 = MinimalDiff(270,180) = -90.
    AddAssert("head unchanged", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(270));
    AddAssert("node offset now -90", () => firstControlPoint().RotationOffset, () => Is.EqualTo(-90));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestFlipAroundAngle"`
Expected: FAIL — the stub `BeginFlipAroundAngle` does nothing, so the note/node never changes (and Escape test may pass vacuously; the reflect tests fail).

- [ ] **Step 3: Create `FlipPivotOverlay`**

Create `Garbus.Game/Edit/FlipPivotOverlay.cs`:

```csharp
using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit;

/// <summary>
/// A transient full-playfield overlay for "Flip around angle…". While active it draws a vertical pivot
/// bar snapped to the angle grid under the cursor; a left-click commits the reflection about that angle
/// via the supplied callback, and right-click or Escape cancels. Inactive (and input-transparent) at all
/// other times so it never steals clicks from the blueprint stack it sits above.
/// </summary>
public partial class FlipPivotOverlay : CompositeDrawable
{
    private readonly Func<float, (float xFrac, int angleDeg)> snap;
    private readonly Box bar;

    private Action<int>? onCommit;
    private bool active;
    private int pivotAngle;

    public FlipPivotOverlay(Func<float, (float xFrac, int angleDeg)> snap)
    {
        this.snap = snap;
        RelativeSizeAxes = Axes.Both;
        Alpha = 0;

        InternalChild = bar = new Box
        {
            RelativeSizeAxes = Axes.Y,
            Width = 2,
            Colour = new Colour4(255, 204, 34, 255), // osu Yellow, matching selection accents.
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopCentre,
        };
    }

    public void Begin(Action<int> commit)
    {
        onCommit = commit;
        active = true;
        Alpha = 1;
    }

    private void end()
    {
        active = false;
        Alpha = 0;
        onCommit = null;
    }

    public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => active;

    protected override bool OnMouseMove(MouseMoveEvent e)
    {
        if (!active)
            return false;

        (float xFrac, int angleDeg) = snap(e.MousePosition.X / DrawWidth);
        pivotAngle = angleDeg;
        bar.X = xFrac * DrawWidth;
        return true;
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (active && e.Button == MouseButton.Right)
        {
            end();
            return true;
        }

        return false; // let a left press flow through to OnClick.
    }

    protected override bool OnClick(ClickEvent e)
    {
        if (!active)
            return false;

        if (e.Button == MouseButton.Left)
            onCommit?.Invoke(EditorAngleMapping.NormalizeDeg(2 * pivotAngle));

        end();
        return true;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (active && e.Key == Key.Escape)
        {
            end();
            return true;
        }

        return base.OnKeyDown(e);
    }
}
```

- [ ] **Step 4: Wire the overlay into the composer**

In `GarbusHitObjectComposer.cs`:

(a) Add a field near the top of the class:

```csharp
private FlipPivotOverlay flipPivotOverlay = null!;
```

(b) At the end of the composer's `load()` (the `[BackgroundDependencyLoader] private void load()`), add the overlay as the topmost child of the playfield content (it runs after the base BDL, so `PlayfieldContentContainer` exists):

```csharp
PlayfieldContentContainer.Add(flipPivotOverlay = new FlipPivotOverlay(x => EditorAngleMapping.SnapX(x, AngleSnap.Value)));
```

(c) Replace the Task 2 stub `public void BeginFlipAroundAngle(System.Action<int> onCommit) { }` with:

```csharp
/// <summary>Enters interactive "flip around angle" mode: the overlay picks a pivot; <paramref name="onCommit"/> receives 2·pivot.</summary>
public void BeginFlipAroundAngle(Action<int> onCommit) => flipPivotOverlay.Begin(onCommit);
```

(Ensure `using System;` is present for `Action`.)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestFlipAroundAngle"`
Expected: PASS (3 tests).

- [ ] **Step 6: Run the full editor test suite for regressions**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposeSelection|FullyQualifiedName~TestEditorAngleMapping"`
Expected: PASS (all, including the pre-existing selection/mapping tests).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/FlipPivotOverlay.cs Garbus.Game/Edit/GarbusHitObjectComposer.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: interactive Flip around angle pivot overlay"
```

---

## Task 4: ShoulderNote hemisphere coverage + full build

Locks the ShoulderNote rule (deferred to its own test because it needs a placed shoulder) and confirms the whole solution builds.

**Files:**
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–3.

- [ ] **Step 1: Write the failing test**

Add to `TestSceneComposeSelection.cs`:

```csharp
[Test]
public void TestFlipAroundAngleFlipsShoulderSideAcrossVerticalAxis()
{
    waitForComposer();

    // Place a shoulder on the West side; read back whatever Side placement assigned.
    AddStep("select shoulder tool", () => input.Key(Key.Number4));
    AddStep("place shoulder (West)", () =>
    {
        input.MoveMouseTo(positionAtAngle(180, 0.5f));
        input.Click(MouseButton.Left);
    });
    AddAssert("shoulder placed", () => placedObject<ShoulderNote>() != null);
    settleWith(() => placedObject<ShoulderNote>()!.StartTime);
    AddStep("switch to select tool", () => input.Key(Key.Number1));

    hoverThenClick(() => screenPositionOf(placedObject<ShoulderNote>()!));
    AddAssert("shoulder selected", () => editorChart.SelectedHitObjects.Count, () => Is.EqualTo(1));

    HorizontalDirection sideBefore = default;
    AddStep("snapshot side", () => sideBefore = placedObject<ShoulderNote>()!.Side);

    // Flip about North (90°): a vertical-ish axis swaps East<->West, so Side must flip.
    GarbusMenuItem flip = null!;
    AddUntilStep("Flip around angle available", () =>
    {
        input.MoveMouseTo(screenPositionOf(placedObject<ShoulderNote>()!));
        flip = flipMenuItem("Flip around angle...")!;
        return flip != null;
    });
    AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
    AddStep("move pivot to North (90°)", () => input.MoveMouseTo(positionAtAngle(90, 0.5f)));
    AddStep("commit", () => input.Click(MouseButton.Left));
    AddAssert("side flipped", () => placedObject<ShoulderNote>()!.Side, () => Is.Not.EqualTo(sideBefore));

    // Flip again about East (0°): a horizontal axis (N<->S) leaves E/W untouched, so Side is unchanged.
    HorizontalDirection sideMid = default;
    AddStep("snapshot side", () => sideMid = placedObject<ShoulderNote>()!.Side);
    AddUntilStep("Flip around angle available again", () =>
    {
        input.MoveMouseTo(screenPositionOf(placedObject<ShoulderNote>()!));
        flip = flipMenuItem("Flip around angle...")!;
        return flip != null;
    });
    AddStep("begin flip-around", () => flip.Action.Value?.Invoke());
    AddStep("move pivot to East (0°)", () => input.MoveMouseTo(positionAtAngle(0, 0.5f)));
    AddStep("commit", () => input.Click(MouseButton.Left));
    AddAssert("side unchanged", () => placedObject<ShoulderNote>()!.Side, () => Is.EqualTo(sideMid));
}
```

- [ ] **Step 2: Run test to verify it passes (behavior already implemented in Task 2)**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestFlipAroundAngleFlipsShoulderSide"`
Expected: PASS — the ShoulderNote branch in `Flip` (Task 2) already handles this; this test pins it. If it fails, the defect is in the Task 2 `inEastHemisphere` / `Side` logic — fix there.

- [ ] **Step 3: Full solution build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Full test suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (all headless tests green).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "test: pin ShoulderNote hemisphere flip rule"
```

---

## Self-review notes (for the implementer)

- **Switch order in `Flip` matters:** `ShoulderNote` → `SliderBody`-with-nodes → `SliderBody` → `IHasMutableAngle` → default. `SliderBody` is `IHasMutableAngle`, so its cases must precede the generic one.
- **Node-subset uses `MinimalDiff` for the stored offset** (minimal winding) while the **whole-slider** case uses `-rot` (preserves winding). This is intentional; don't unify them.
- **`sumDeg` is normalised to [0,360)** — safe for point-object and node-subset maths (both re-derive absolute angles before storing). The whole-slider `-rot` path is independent of `sumDeg`'s normalisation.
- If `hoverThenClick` or `placeNoteAt` behaves differently than assumed, re-read their definitions at the top of `TestSceneComposeSelection.cs` before adjusting tests — do not weaken assertions.
