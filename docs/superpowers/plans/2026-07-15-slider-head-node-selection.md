# Slider Head-Node Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a slider's implicit head node individually selectable, draggable, deletable, and flippable in the compose editor, exactly like its control-point nodes.

**Architecture:** The head stays implicit in the data model (its time/angle *are* the slider's `StartTime`/`AngleDeg`). Selection state lives entirely inside `SliderSelectionBlueprint` as a `bool headSelected` flag beside the existing `selectedNodes` set (sentinel index `-1`). Head drag mutates `StartTime`/`AngleDeg` and compensates unselected nodes so they hold their absolute position; head delete promotes the first control point to become the new head. No chart-format, serialization, clipboard, or gameplay changes.

**Tech Stack:** C# / osu-framework, NUnit headless visual test scenes (`Garbus.Game.Tests`).

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL fields use `= null!`.
- This is an experimental project — **no** backwards-compatibility layers, no version bumps, no historical notes in docs.
- Terminology: osu "beatmap" → "chart"; British "Judgement".
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- `EditorChart.Update(hitObject)` refreshes drawables **in place** (never remove+recreate) — every mutation must go through it, and it must fire **only when something actually changed** (drag events in the same snap cell must not re-`Update`).
- Every chart mutation is wrapped in one `changeHandler.BeginChange()` / `EndChange()` per gesture so undo/redo works via the JSON-snapshot diff.
- Node/head selection is local to `SliderSelectionBlueprint` — NOT part of `EditorChart.SelectedHitObjects`, undo, or clipboard.
- The head has no incoming path segment, so it carries **no** `SweepEasing` — easing operations skip it.
- Placement/hit-zone rule: an object's `StartTime` may never be `< 0`.
- Slider path invariant (enforced by `GarbusSliderPath.AreTimesValid`): implicit head at offset 0, non-decreasing control-point `TimeOffset`s, at most one zero-length link in a row, total duration `> 0`.

---

## Reference: key existing code

**`SliderSelectionBlueprint`** (`Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`):
- `private readonly HashSet<GarbusPathControlPoint> selectedNodes` + `internal IReadOnlyCollection<GarbusPathControlPoint> SelectedNodes => selectedNodes` (line 69-71).
- `head` is an `EditSquarePiece` (passive, non-interactive) added in `load()` (line 89).
- `nodeHandles` is a `Container<NodeDragPiece>`; each frame `Update()` allocates `controlPoints.Count * wrapCopiesBuffer.Count` handles, setting `handle.CpIndex`, `handle.WrapK`, `handle.Position`, `handle.NodeSelected` (lines 143-176). Handle callbacks: `SelectRequested = (index, ctrl) => selectNode(index, ctrl)`, `DragStarted`, `Dragging = (index, pos) => dragNode(index, pos)`, `DragEnded` (lines 150-156).
- `dragNode(int index, Vector2 screenSpacePosition)` (line 267): grabs `controlPoints[index]`, computes moved set, applies Δtime (guarded by `timeShiftValid`) and Δangle, calls `editorChart.Update(HitObject)` iff `changed`.
- `timeShiftValid(controlPoints, moved, deltaTime)` (line 329): builds prospective offsets, returns `GarbusSliderPath.AreTimesValid(offsets)`.
- `selectNode(int index, bool ctrl)` (line 340).
- `removeNodes(IReadOnlyList<GarbusPathControlPoint> nodes)` (line 499): empties → `editorChart.Remove`; else removes cps + `editorChart.Update`.
- `OnPressed(PlatformAction)` Delete (line 480), `HandleQuickDeletion()` Shift+RightClick (line 524).
- `ReceivePositionalInputAt` (line 245) tests outline paths, then `head`, then `nodeHandles`.
- `ScreenSpaceSelectionPoint => head.ScreenSpaceDrawQuad.Centre` (line 547); `FinalNodeScreenPosition` (line 551) falls back to `head.ScreenSpaceDrawQuad.Centre`.
- `OnDeselected()` clears `selectedNodes` (line 464); `OnClick` clears `selectedNodes` on left-click (line 470).

**`NodeDragPiece`** (`Garbus.Game/Edit/Blueprints/Components/NodeDragPiece.cs`): circular handle, `CpIndex`/`WrapK` set each frame, `NodeSelected` drives fill alpha, `OnMouseDown` invokes `SelectRequested` and returns true, drag callbacks.

**`EditSquarePiece`** (`Garbus.Game/Edit/Blueprints/Components/EditSquarePiece.cs`): a *square* yellow outline box with a hidden `Box` fill; currently no interactivity, no selected state.

**`GarbusSelectionHandler`** (`Garbus.Game/Edit/GarbusSelectionHandler.cs`):
- `flip(int sumDeg, FlipMode mode, int pivotDeg)` (line 220): `case SliderBody slider when blueprint is SliderSelectionBlueprint sb && sb.SelectedNodes.Count > 0: reflectSelectedNodes(...)` (line 242).
- `reflectSelectedNodes(slider, sb, mode, pivotDeg)` (line 281): computes sum `s`, then `foreach cp in sb.SelectedNodes: cp.RotationOffset = s - cp.RotationOffset`.
- `handleAngles()` (line 319): same `SelectedNodes.Count > 0` guard yields per-node absolute angles.
- `EditorAngleMapping.NormalizeDeg(int)`, `.MinimalDiff(int,int)` are the helpers used throughout.

**`Inspector`** (`Garbus.Game/Edit/Inspector.cs`): `collectSelectedNodes()` (line 110) walks `composer.BlueprintContainer.SelectionHandler.SelectedBlueprints`, gathering `sliderBlueprint.SelectedNodes`. `Update()` (line 99) polls set-equality vs `lastNodeSelectionSnapshot` to trigger `rebuild()`. `writeSummary` shows "Selected Nodes" count (line 205).

**Tests** (`Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`): harness helpers `placeDiagonalSlider()` (head at 270°/0.7f, one node at 0°/0.4f), `addSecondNode()`, `selectSliderOnLine()`, `sliderEndsScreen()` → `(head, node)` screen points, `nodeHandleScreen(i)`, `primaryNodeHandleX(i)`, `sliderBlueprint()`, `firstControlPoint()`, `settleWith(...)`, `dragStepRight(deg)`. Node behaviours pinned in `TestClickingHandleOnSelectedSliderSelectsNode`, `TestCtrlClickTogglesNodesInSelection`, etc.

---

## File Structure

- **Modify** `Garbus.Game/Edit/Blueprints/Components/EditSquarePiece.cs` — make it an interactive drag handle with a selected fill state and the same callback surface as `NodeDragPiece` (Task 1).
- **Modify** `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs` — `headSelected` state + `HeadSelected` accessor, interactive head handles per wrap copy, unified drag transform with head compensation, delete-with-promotion, selection-point re-pointing (Tasks 2–6).
- **Modify** `Garbus.Game/Edit/Inspector.cs` — head-aware node-selection poll/snapshot and count (Task 7).
- **Modify** `Garbus.Game/Edit/GarbusSelectionHandler.cs` — flip guards, `handleAngles`, head reflection (Task 8).
- **Modify** `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` — all new coverage lives here alongside the existing node tests.

Task order: 1 (handle widget) → 2 (select) → 3 (drag) → 4 (delete) → 5 (re-point selection anchors) → 6 (wrap copies) → 7 (Inspector) → 8 (flip). Each task ends green.

---

### Task 1: Make `EditSquarePiece` a draggable, selectable head handle

Give the head square the same interaction surface as `NodeDragPiece` (mouse-down select, drag callbacks, selected fill) without changing its square appearance. The blueprint (Task 2+) will wire the callbacks.

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/Components/EditSquarePiece.cs`
- Test: none directly (exercised through blueprint tests in later tasks; this task is a pure widget change verified by build).

**Interfaces:**
- Produces: `EditSquarePiece` with `Action<int,bool>? SelectRequested`, `Action? DragStarted`, `Action<int,Vector2>? Dragging`, `Action? DragEnded`, `int CpIndex { get; set; }`, `int WrapK { get; set; }`, `bool NodeSelected { get; set; }`. `OnMouseDown` (left) invokes `SelectRequested(CpIndex, ctrl)` and returns true; drag events invoke the drag callbacks. Selected → solid fill (alpha 1), else outline only.

- [ ] **Step 1: Rewrite `EditSquarePiece` to be interactive**

Replace the whole file body with (keeping the ppy attribution header comment at top):

```csharp
using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>The yellow outline box used by note blueprints (the Garbus analogue of mania's EditNotePiece).
/// Doubles as the slider head handle: draggable, and fills solid when selected. Non-head uses leave the
/// callbacks null and never receive input, so behaviour there is unchanged.</summary>
internal partial class EditSquarePiece : CompositeDrawable
{
    /// <summary>Invoked on left mouse-down with (control-point index, Ctrl-pressed). The head uses the
    /// sentinel index -1; the blueprint routes it to head selection.</summary>
    public Action<int, bool>? SelectRequested { get; init; }

    public Action? DragStarted { get; init; }
    public Action<int, Vector2>? Dragging { get; init; }
    public Action? DragEnded { get; init; }

    /// <summary>Index this handle stands over (-1 = the head). Reassigned every frame by the blueprint.</summary>
    public int CpIndex { get; set; } = -1;

    /// <summary>The wrap-copy this handle stands on (0 = raw/primary copy, non-zero = a ghost-band clone).</summary>
    public int WrapK { get; set; }

    private readonly Box fill;

    private bool nodeSelected;

    /// <summary>Whether this handle's node is selected; drives the solid fill.</summary>
    public bool NodeSelected
    {
        get => nodeSelected;
        set
        {
            if (nodeSelected == value)
                return;

            nodeSelected = value;
            fill.Alpha = value ? 1 : 0;
        }
    }

    public EditSquarePiece()
    {
        InternalChild = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            BorderThickness = 3,
            BorderColour = new Colour4(255, 196, 40, 255),
            Child = fill = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                AlwaysPresent = true,
            },
        };
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left || SelectRequested == null)
            return base.OnMouseDown(e);

        SelectRequested.Invoke(CpIndex, e.ControlPressed);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e)
    {
        if (DragStarted == null)
            return base.OnDragStart(e);

        DragStarted.Invoke();
        return true;
    }

    protected override void OnDrag(DragEvent e)
    {
        base.OnDrag(e);
        Dragging?.Invoke(CpIndex, e.ScreenSpaceMousePosition);
    }

    protected override void OnDragEnd(DragEndEvent e)
    {
        base.OnDragEnd(e);
        DragEnded?.Invoke();
    }
}
```

Note: when `SelectRequested`/`DragStarted` are null (every non-head use — note blueprints, twin visuals), `OnMouseDown`/`OnDragStart` fall through to base and the widget behaves exactly as before.

- [ ] **Step 2: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: build succeeds (existing `EditSquarePiece` usages compile unchanged — new members are optional).

- [ ] **Step 3: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/Components/EditSquarePiece.cs
git commit -m "feat: make EditSquarePiece an interactive head handle"
```

---

### Task 2: Head selection state + click routing

Add `headSelected` and wire the head square's `SelectRequested` through a sentinel `CpIndex == -1` so plain-click/Ctrl-toggle/clear-on-body-click/clear-on-deselect all include the head.

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `EditSquarePiece` callbacks from Task 1.
- Produces: `internal bool HeadSelected` on `SliderSelectionBlueprint`; `selectNode(int index, bool ctrl)` extended to treat `index == -1` as the head.

- [ ] **Step 1: Write failing tests**

Add to `TestSceneComposeSelection.cs`. First a helper to click the head handle, then the tests:

```csharp
/// <summary>Screen centre of the head handle (EditSquarePiece) that currently sits on the main grid.</summary>
private Vector2 headHandleScreen()
{
    var playfieldQuad = playfield.ScreenSpaceDrawQuad;
    var candidates = sliderBlueprint().ChildrenOfType<Garbus.Game.Edit.Blueprints.Components.EditSquarePiece>()
                                      .Where(h => h.CpIndex == -1).ToList();
    var onGrid = candidates.FirstOrDefault(h => playfieldQuad.Contains(h.ScreenSpaceDrawQuad.Centre));
    return (onGrid ?? candidates[0]).ScreenSpaceDrawQuad.Centre;
}

[Test]
public void TestClickingHeadOnUnselectedSliderSelectsWholeSlider()
{
    waitForComposer();
    placeDiagonalSlider();

    AddStep("click the head position", () =>
    {
        var (headScreen, _) = sliderEndsScreen();
        input.MoveMouseTo(headScreen);
        input.Click(MouseButton.Left);
    });
    AddAssert("whole slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
    AddAssert("head not selected", () => sliderBlueprint().HeadSelected, () => Is.False);
}

[Test]
public void TestClickingHeadOnSelectedSliderSelectsHead()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    AddStep("click the head handle", () =>
    {
        input.MoveMouseTo(headHandleScreen());
        input.Click(MouseButton.Left);
    });
    AddAssert("slider still selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
    AddAssert("head selected", () => sliderBlueprint().HeadSelected, () => Is.True);
    AddAssert("no control-point node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
}

[Test]
public void TestCtrlClickTogglesHeadWithNode()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    AddStep("click node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
    AddStep("ctrl+click head", () =>
    {
        input.MoveMouseTo(headHandleScreen());
        input.PressKey(Key.LControl);
        input.Click(MouseButton.Left);
        input.ReleaseKey(Key.LControl);
    });
    AddAssert("head + node both selected", () =>
        sliderBlueprint().HeadSelected && sliderBlueprint().SelectedNodes.Count == 1);

    AddStep("ctrl+click head again", () =>
    {
        input.MoveMouseTo(headHandleScreen());
        input.PressKey(Key.LControl);
        input.Click(MouseButton.Left);
        input.ReleaseKey(Key.LControl);
    });
    AddAssert("head toggled off, node stays", () =>
        !sliderBlueprint().HeadSelected && sliderBlueprint().SelectedNodes.Count == 1);
}

[Test]
public void TestClickingLineClearsHeadSelection()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    AddStep("click head handle", () => { input.MoveMouseTo(headHandleScreen()); input.Click(MouseButton.Left); });
    AddAssert("head selected", () => sliderBlueprint().HeadSelected, () => Is.True);

    AddStep("click the line", () =>
    {
        var (headScreen, nodeScreen) = sliderEndsScreen();
        input.MoveMouseTo((headScreen + nodeScreen) / 2);
        input.Click(MouseButton.Left);
    });
    AddAssert("head selection cleared", () => sliderBlueprint().HeadSelected, () => Is.False);
}

[Test]
public void TestDeselectingSliderClearsHeadSelection()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    AddStep("click head handle", () => { input.MoveMouseTo(headHandleScreen()); input.Click(MouseButton.Left); });
    AddAssert("head selected", () => sliderBlueprint().HeadSelected, () => Is.True);

    AddStep("deselect all", () => editorChart.SelectedHitObjects.Clear());
    AddAssert("head selection cleared", () => sliderBlueprint().HeadSelected, () => Is.False);
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestClickingHeadOnSelectedSliderSelectsHead"`
Expected: FAIL — `HeadSelected` does not exist / head square not interactive (compile error until Step 3).

- [ ] **Step 3: Add state + accessor**

In `SliderSelectionBlueprint.cs`, after the `selectedNodes` field / `SelectedNodes` property (line 69-71) add:

```csharp
    // The implicit head node (offset 0 = slider StartTime/AngleDeg). Selectable alongside control points
    // but has no GarbusPathControlPoint, so it is a plain flag rather than a set member. Sentinel index -1.
    private const int head_index = -1;

    private bool headSelected;

    internal bool HeadSelected => headSelected;
```

- [ ] **Step 4: Wire the head square's callbacks and selected fill**

In `load()`, replace the passive `head = new EditSquarePiece { ... }` block (lines 89-95) with the interactive version:

```csharp
            head = new EditSquarePiece
            {
                RelativeSizeAxes = Axes.X,
                Height = EditorDrawableCardinalNote.NOTE_SIZE,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.Centre,
                CpIndex = head_index,
                SelectRequested = (index, ctrl) => selectNode(index, ctrl),
                DragStarted = () => changeHandler?.BeginChange(),
                Dragging = (index, pos) => dragNode(index, pos),
                DragEnded = () => changeHandler?.EndChange(),
            };
```

At the end of `Update()`, after the node-handle loop and the `selectedNodes.RemoveWhere(...)` prune (line 179), push the head's selected fill:

```csharp
        head.NodeSelected = headSelected;
```

- [ ] **Step 5: Extend `selectNode` to handle the head sentinel**

Replace `selectNode` (lines 340-362) with a version that treats `index == head_index` as the head. Model the head as a pseudo-member of the same selection so plain-click / Ctrl-toggle / keep-group semantics match nodes:

```csharp
    /// <summary>Left-click selection of a node (or the head, index -1): plain click selects only it; Ctrl
    /// toggles it in the combined head+nodes selection; plain-clicking something already in a multi-selection
    /// keeps the group so a drag moves it all.</summary>
    private void selectNode(int index, bool ctrl)
    {
        var controlPoints = HitObject.Path.ControlPoints;

        bool isHead = index == head_index;
        GarbusPathControlPoint? cp = null;

        if (!isHead)
        {
            if (index >= controlPoints.Count)
                return;

            cp = controlPoints[index];
        }

        bool alreadySelected = isHead ? headSelected : selectedNodes.Contains(cp!);

        if (ctrl)
        {
            if (isHead)
                headSelected = !headSelected;
            else if (!selectedNodes.Add(cp!))
                selectedNodes.Remove(cp!);
            return;
        }

        // plain click on something already in a multi-selection keeps the whole group (so a drag moves it all);
        // otherwise reduce to just this handle.
        if (alreadySelected)
            return;

        selectedNodes.Clear();
        headSelected = isHead;
        if (!isHead)
            selectedNodes.Add(cp!);
    }
```

- [ ] **Step 6: Clear head on deselect and on body click**

In `OnDeselected()` (line 464), after `selectedNodes.Clear();` add:

```csharp
        headSelected = false;
```

In `OnClick` (line 470-478), inside the `if (e.Button == MouseButton.Left)` block, after `selectedNodes.Clear();` add:

```csharp
            headSelected = false;
```

- [ ] **Step 7: Run the Task-2 tests**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestClickingHead|TestCtrlClickTogglesHeadWithNode|TestClickingLineClearsHeadSelection|TestDeselectingSliderClearsHeadSelection"`
Expected: PASS (5 tests).

- [ ] **Step 8: Run existing node tests for regressions**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneComposeSelection"`
Expected: PASS (all, including pre-existing node/select tests).

- [ ] **Step 9: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: select slider head node like a control point"
```

---

### Task 3: Head drag — move the start, compensate the tail

Generalise `dragNode` so a moved set containing the head mutates `StartTime`/`AngleDeg`, keeps in-set nodes riding along, and shifts out-of-set nodes' offsets to hold their absolute position. Time validity gains a `StartTime >= 0` check.

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `selectNode` head handling (Task 2), `composer.FindSnappedAngleTimeAndPosition`, `EditorAngleMapping.MinimalDiff/NormalizeDeg`, `GarbusSliderPath.AreTimesValid`.
- Produces: `dragNode(int index, Vector2)` handling `index == -1`; new `timeShiftValidWithHead(controlPoints, movedNodes, headInSet, deltaTime)` returning bool.

- [ ] **Step 1: Write failing tests**

The `placeDiagonalSlider()` slider has head at 270° and one node at 0° (absolute), the node later in time. Dragging the head must change `StartTime`/`AngleDeg` while the lone (unselected) node keeps its absolute time and angle. Add:

```csharp
[Test]
public void TestDraggingHeadMovesStartAndCompensatesNode()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    double origStart = 0, origNodeAbsTime = 0;
    int origAngle = 0, origNodeAbsAngle = 0;
    AddStep("capture originals", () =>
    {
        var s = placedObject<SliderBody>()!;
        var cp = s.Path.ControlPoints[0];
        origStart = s.StartTime;
        origAngle = s.AngleDeg;
        origNodeAbsTime = s.StartTime + cp.TimeOffset;
        origNodeAbsAngle = EditorAngleMapping.NormalizeDeg(s.AngleDeg + cp.RotationOffset);
    });

    // Select only the head, then drag it 45° to the right (one snap increment).
    AddStep("select head", () => { input.MoveMouseTo(headHandleScreen()); input.Click(MouseButton.Left); });
    AddAssert("head selected", () => sliderBlueprint().HeadSelected, () => Is.True);

    AddStep("press on head handle", () => { input.MoveMouseTo(headHandleScreen()); input.PressButton(MouseButton.Left); });
    AddStep("drag 45° right", () => dragStepRight(45));
    AddStep("release", () => input.ReleaseButton(MouseButton.Left));

    AddAssert("head angle changed by +45", () =>
        placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(EditorAngleMapping.NormalizeDeg(origAngle + 45)));
    AddAssert("node kept its absolute angle", () =>
    {
        var s = placedObject<SliderBody>()!;
        var cp = s.Path.ControlPoints[0];
        return EditorAngleMapping.NormalizeDeg(s.AngleDeg + cp.RotationOffset);
    }, () => Is.EqualTo(origNodeAbsAngle));
    AddAssert("start time unchanged (pure angle drag)", () =>
        placedObject<SliderBody>()!.StartTime, () => Is.EqualTo(origStart));
    AddAssert("node kept its absolute time", () =>
    {
        var s = placedObject<SliderBody>()!;
        return s.StartTime + s.Path.ControlPoints[0].TimeOffset;
    }, () => Is.EqualTo(origNodeAbsTime));
}

[Test]
public void TestDraggingHeadDoesNotRecreateDrawable()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    Gameplay.Objects.Drawables.DrawableHitObject drawable = null!;
    AddStep("capture drawable", () => drawable = composer.HitObjects.Single());

    AddStep("select head", () => { input.MoveMouseTo(headHandleScreen()); input.Click(MouseButton.Left); });
    AddStep("press on head handle", () => { input.MoveMouseTo(headHandleScreen()); input.PressButton(MouseButton.Left); });
    AddRepeatStep("wiggle right", () => dragStepRight(4), 8);
    AddRepeatStep("wiggle left", () => dragStepRight(-4), 8);
    AddStep("release", () => input.ReleaseButton(MouseButton.Left));

    AddAssert("drawable never recreated", () => composer.HitObjects.Single(), () => Is.SameAs(drawable));
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestDraggingHeadMovesStartAndCompensatesNode"`
Expected: FAIL — dragging the head currently does nothing to `AngleDeg` (the head square wasn't draggable before Task 1; `dragNode(-1, ...)` early-returns on `index >= controlPoints.Count` being false but `controlPoints[-1]` would throw — so the drag mutates nothing meaningful).

- [ ] **Step 3: Rewrite `dragNode` to handle the head sentinel**

Replace `dragNode` (lines 267-322) with:

```csharp
    private void dragNode(int index, Vector2 screenSpacePosition)
    {
        if (composer == null || editorChart == null)
            return;

        var controlPoints = HitObject.Path.ControlPoints;

        bool grabbedHead = index == head_index;
        if (!grabbedHead && index >= controlPoints.Count)
            return;

        // The grabbed handle's current absolute time/angle define the snap deltas.
        double grabbedTimeOffset = grabbedHead ? 0 : controlPoints[index].TimeOffset;
        int grabbedRotationOffset = grabbedHead ? 0 : controlPoints[index].RotationOffset;

        var result = composer.FindSnappedAngleTimeAndPosition(screenSpacePosition);

        // The moved set: the whole combined selection when the grabbed handle is part of it, else just the
        // grabbed handle. `movedHead` mirrors the head flag; `movedNodes` the control-point subset.
        bool grabbedSelected = grabbedHead ? headSelected : selectedNodes.Contains(controlPoints[index]);

        bool movedHead;
        ICollection<GarbusPathControlPoint> movedNodes;

        if (grabbedSelected)
        {
            movedHead = headSelected;
            movedNodes = selectedNodes;
        }
        else
        {
            movedHead = grabbedHead;
            movedNodes = grabbedHead ? System.Array.Empty<GarbusPathControlPoint>() : new[] { controlPoints[index] };
        }

        bool changed = false;

        // Time: shift the grabbed handle by its delta; apply to the moved set only if the whole path stays
        // valid (including StartTime >= 0 when the head moves). All-or-nothing per event.
        if (result.Time is double proposedTime)
        {
            double deltaTime = (proposedTime - HitObject.StartTime) - grabbedTimeOffset;

            if (deltaTime != 0 && timeShiftValidWithHead(controlPoints, movedNodes, movedHead, deltaTime))
            {
                if (movedHead)
                {
                    HitObject.StartTime += deltaTime;
                    // Nodes NOT in the moved set hold their absolute time — compensate their offsets.
                    foreach (var cp in controlPoints)
                    {
                        if (!movedNodes.Contains(cp))
                            cp.TimeOffset -= deltaTime;
                    }
                }
                else
                {
                    foreach (var cp in movedNodes)
                        cp.TimeOffset += deltaTime;
                }

                changed = true;
            }
        }

        // Angle: rotation offsets are free integers (no ordering constraint), so apply the grabbed handle's
        // minimal snap delta unconditionally.
        if (result is GarbusSnapResult snap)
        {
            int currentAbsolute = EditorAngleMapping.NormalizeDeg(HitObject.AngleDeg + grabbedRotationOffset);
            int diff = EditorAngleMapping.MinimalDiff(currentAbsolute, snap.AngleDeg);

            if (diff != 0)
            {
                if (movedHead)
                {
                    HitObject.AngleDeg = EditorAngleMapping.NormalizeDeg(HitObject.AngleDeg + diff);
                    foreach (var cp in controlPoints)
                    {
                        if (!movedNodes.Contains(cp))
                            cp.RotationOffset -= diff;
                    }
                }
                else
                {
                    foreach (var cp in movedNodes)
                        cp.RotationOffset += diff;
                }

                changed = true;
            }
        }

        if (changed)
            editorChart.Update(HitObject);
    }
```

Note the invariant this preserves: when the head moves by Δ and an out-of-set node's offset drops by Δ, its absolute position (`StartTime + TimeOffset`, `AngleDeg + RotationOffset`) is unchanged. In-set nodes keep their offsets, so they ride along with the head.

- [ ] **Step 4: Add `timeShiftValidWithHead`**

Replace the existing `timeShiftValid` (lines 329-337) with the head-aware version (keep the same name callers used, but the drag now calls the new one; the old signature is no longer referenced — delete it):

```csharp
    /// <summary>
    /// True if applying <paramref name="deltaTime"/> leaves the full path valid. When the head moves
    /// (<paramref name="headInSet"/>), StartTime shifts by Δ and every node NOT in <paramref name="movedNodes"/>
    /// has its offset reduced by Δ (so its absolute time is fixed) — plus StartTime + Δ must stay >= 0.
    /// When the head is fixed, only the moved nodes' offsets grow by Δ.
    /// </summary>
    private bool timeShiftValidWithHead(IReadOnlyList<GarbusPathControlPoint> controlPoints, ICollection<GarbusPathControlPoint> movedNodes, bool headInSet, double deltaTime)
    {
        if (headInSet && HitObject.StartTime + deltaTime < 0)
            return false;

        var offsets = new List<double>(controlPoints.Count);

        foreach (var cp in controlPoints)
        {
            bool inSet = movedNodes.Contains(cp);

            if (headInSet)
                offsets.Add(inSet ? cp.TimeOffset : cp.TimeOffset - deltaTime);
            else
                offsets.Add(inSet ? cp.TimeOffset + deltaTime : cp.TimeOffset);
        }

        return GarbusSliderPath.AreTimesValid(offsets);
    }
```

- [ ] **Step 5: Run the Task-3 tests**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestDraggingHead"`
Expected: PASS (2 tests).

- [ ] **Step 6: Run the full selection scene**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneComposeSelection"`
Expected: PASS (all — the existing `dragNode` node-only path is unchanged for `movedHead == false`).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: drag slider head to move start, compensating the tail"
```

---

### Task 4: Head deletion — promote the first control point

Extend the delete path so deleting the head promotes `ControlPoints[0]` into `StartTime`/`AngleDeg` and rebases the rest; head + all nodes removes the slider.

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `headSelected` (Task 2), `removeNodes` structure.
- Produces: `removeSelection()` — one entry point used by both the Delete key and quick-deletion, aware of `headSelected`.

- [ ] **Step 1: Write failing tests**

`placeDiagonalSlider()` gives head 270° + one node. `addSecondNode()` adds a second node (+250ms, 90° offset). Delete the head → node 0 promoted to head; remaining node rebased. Add:

```csharp
[Test]
public void TestDeletingHeadPromotesFirstNode()
{
    waitForComposer();
    placeDiagonalSlider();
    addSecondNode();
    selectSliderOnLine();

    double promotedAbsTime = 0;
    int promotedAbsAngle = 0, survivorAbsAngle = 0;
    double survivorAbsTime = 0;
    AddStep("capture node absolutes", () =>
    {
        var s = placedObject<SliderBody>()!;
        var cp0 = s.Path.ControlPoints[0];
        var cp1 = s.Path.ControlPoints[1];
        promotedAbsTime = s.StartTime + cp0.TimeOffset;
        promotedAbsAngle = EditorAngleMapping.NormalizeDeg(s.AngleDeg + cp0.RotationOffset);
        survivorAbsTime = s.StartTime + cp1.TimeOffset;
        survivorAbsAngle = EditorAngleMapping.NormalizeDeg(s.AngleDeg + cp1.RotationOffset);
    });

    AddStep("select head", () => { input.MoveMouseTo(headHandleScreen()); input.Click(MouseButton.Left); });
    AddAssert("head selected", () => sliderBlueprint().HeadSelected, () => Is.True);

    AddStep("press delete", () => input.Key(Key.Delete));

    AddAssert("one node left (was 2)", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));
    AddAssert("new head at promoted node's absolute time", () =>
        placedObject<SliderBody>()!.StartTime, () => Is.EqualTo(promotedAbsTime));
    AddAssert("new head at promoted node's absolute angle", () =>
        placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(promotedAbsAngle));
    AddAssert("survivor kept absolute time", () =>
    {
        var s = placedObject<SliderBody>()!;
        return s.StartTime + s.Path.ControlPoints[0].TimeOffset;
    }, () => Is.EqualTo(survivorAbsTime));
    AddAssert("survivor kept absolute angle", () =>
    {
        var s = placedObject<SliderBody>()!;
        return EditorAngleMapping.NormalizeDeg(s.AngleDeg + s.Path.ControlPoints[0].RotationOffset);
    }, () => Is.EqualTo(survivorAbsAngle));
    AddAssert("head deselected after delete", () => sliderBlueprint().HeadSelected, () => Is.False);
}

[Test]
public void TestDeletingHeadAndAllNodesRemovesSlider()
{
    waitForComposer();
    placeDiagonalSlider();  // head + 1 node
    selectSliderOnLine();

    AddStep("select head", () => { input.MoveMouseTo(headHandleScreen()); input.Click(MouseButton.Left); });
    AddStep("ctrl+click node 0", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(0));
        input.PressKey(Key.LControl);
        input.Click(MouseButton.Left);
        input.ReleaseKey(Key.LControl);
    });
    AddAssert("head + node selected", () =>
        sliderBlueprint().HeadSelected && sliderBlueprint().SelectedNodes.Count == 1);

    AddStep("press delete", () => input.Key(Key.Delete));
    AddAssert("slider removed", () => placedObject<SliderBody>(), () => Is.Null);
}

[Test]
public void TestUndoRestoresPromotedHead()
{
    waitForComposer();
    placeDiagonalSlider();
    addSecondNode();
    selectSliderOnLine();

    int origAngle = 0, origNodeCount = 0;
    double origStart = 0;
    AddStep("capture", () =>
    {
        var s = placedObject<SliderBody>()!;
        origAngle = s.AngleDeg;
        origStart = s.StartTime;
        origNodeCount = s.Path.ControlPoints.Count;
    });

    AddStep("select head", () => { input.MoveMouseTo(headHandleScreen()); input.Click(MouseButton.Left); });
    AddStep("press delete", () => input.Key(Key.Delete));
    AddAssert("node count dropped", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(origNodeCount - 1));

    AddStep("undo", () => changeHandler.RestoreState(-1));
    AddAssert("start restored", () => placedObject<SliderBody>()!.StartTime, () => Is.EqualTo(origStart));
    AddAssert("angle restored", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(origAngle));
    AddAssert("node count restored", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(origNodeCount));
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestDeletingHeadPromotesFirstNode"`
Expected: FAIL — Delete with only the head "selected" currently hits `selectedNodes.Count == 0` and falls through to whole-slider delete (or no-op), so the node count / promotion assertions fail.

- [ ] **Step 3: Add the head-aware removal core**

Add a new method and route both delete entry points through it. Insert after `removeNodes` (after line 522):

```csharp
    /// <summary>
    /// Removes the current head+node selection in one transaction. Head not selected → same as removeNodes.
    /// Head selected with survivors → the selected control points are dropped, then the new first control
    /// point is promoted into StartTime/AngleDeg and the remaining offsets rebased. Head + everything → the
    /// slider is removed.
    /// </summary>
    private void removeSelection()
    {
        if (editorChart == null)
            return;

        if (!headSelected)
        {
            if (selectedNodes.Count > 0)
                removeNodes(new List<GarbusPathControlPoint>(selectedNodes));
            return;
        }

        var controlPoints = HitObject.Path.ControlPoints;

        // Head selected → after dropping the selected nodes, promotion consumes one more survivor (the new
        // first control point). So the guard is `Count - 1`, not `Count`: at most one control point may
        // survive the node-drop and still be safely promoted away without leaving an empty path.
        if (selectedNodes.Count >= controlPoints.Count - 1)
        {
            changeHandler?.BeginChange();
            editorChart.Remove(HitObject);
            selectedNodes.Clear();
            headSelected = false;
            changeHandler?.EndChange();
            return;
        }

        changeHandler?.BeginChange();

        // Drop the selected control points first.
        foreach (var cp in selectedNodes)
            controlPoints.Remove(cp);

        // Promote the new first control point to be the head.
        var promoted = controlPoints[0];
        double deltaTime = promoted.TimeOffset;
        int deltaAngle = promoted.RotationOffset;

        HitObject.StartTime += deltaTime;
        HitObject.AngleDeg = EditorAngleMapping.NormalizeDeg(HitObject.AngleDeg + deltaAngle);

        controlPoints.RemoveAt(0);

        foreach (var cp in controlPoints)
        {
            cp.TimeOffset -= deltaTime;
            cp.RotationOffset -= deltaAngle;
        }

        selectedNodes.Clear();
        headSelected = false;

        editorChart.Update(HitObject);
        changeHandler?.EndChange();
    }
```

- [ ] **Step 4: Route the Delete key through `removeSelection`**

Replace `OnPressed(KeyBindingPressEvent<PlatformAction> e)` (lines 480-489) with:

```csharp
    public bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
    {
        // Only intercept Delete when the head or node(s) are picked; otherwise let SelectionHandler delete
        // the whole slider. The blueprint sits above SelectionHandler in the input queue, so it sees the
        // action first.
        if (e.Action != PlatformAction.Delete || (!headSelected && selectedNodes.Count == 0))
            return false;

        removeSelection();
        return true;
    }
```

- [ ] **Step 5: Run the Task-4 tests**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestDeletingHead|TestUndoRestoresPromotedHead"`
Expected: PASS (3 tests).

- [ ] **Step 6: Run the full selection scene**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneComposeSelection"`
Expected: PASS (all — node-only delete still routes through `removeNodes`).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: delete slider head by promoting the first node"
```

---

### Task 5: Quick-delete the head + re-point selection anchors

Shift+RightClick over the head handle deletes the head; `ReceivePositionalInputAt`, `ScreenSpaceSelectionPoint`, and `FinalNodeScreenPosition` keep working now that `head` is interactive (the head already consumes mouse-down, so verify selection-point still resolves).

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `removeSelection` (Task 4), `head.IsHovered`.
- Produces: `HandleQuickDeletion()` extended for a hovered head handle.

- [ ] **Step 1: Write failing test**

```csharp
[Test]
public void TestQuickDeleteHeadHandlePromotesFirstNode()
{
    waitForComposer();
    placeDiagonalSlider();
    addSecondNode();
    selectSliderOnLine();

    AddStep("select head", () => { input.MoveMouseTo(headHandleScreen()); input.Click(MouseButton.Left); });

    AddStep("shift+right-click the head handle", () =>
    {
        input.MoveMouseTo(headHandleScreen());
        input.PressKey(Key.LShift);
        input.Click(MouseButton.Right);
        input.ReleaseKey(Key.LShift);
    });

    AddAssert("head promoted (one node left)", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestQuickDeleteHeadHandlePromotesFirstNode"`
Expected: FAIL — `HandleQuickDeletion` only checks node handles, so the head hover falls through to whole-slider delete (slider removed → node count assertion fails / slider null).

- [ ] **Step 3: Extend `HandleQuickDeletion`**

Replace `HandleQuickDeletion()` (lines 524-540) with a version that checks the head square first:

```csharp
    public override bool HandleQuickDeletion()
    {
        // Shift+RightClick over the head handle deletes (promotes) the head; over a node handle deletes just
        // that node; over the line, fall through (return false) so SelectionHandler removes the whole slider.
        if (head.IsHovered)
        {
            headSelected = true;
            selectedNodes.Clear();
            removeSelection();
            return true;
        }

        var controlPoints = HitObject.Path.ControlPoints;

        foreach (var handle in nodeHandles)
        {
            if (handle.IsHovered && handle.CpIndex < controlPoints.Count)
            {
                removeNodes(new List<GarbusPathControlPoint> { controlPoints[handle.CpIndex] });
                return true;
            }
        }

        return false;
    }
```

Note: if the head is the only node (no control points), `removeSelection` with `selectedNodes.Count (0) >= controlPoints.Count - 1 (-1)` removes the slider — correct (a headless path is impossible).

- [ ] **Step 4: Add a selection-anchor regression test**

`ScreenSpaceSelectionPoint` reads `head.ScreenSpaceDrawQuad.Centre`; confirm the head being interactive didn't break whole-slider selection via the selection point:

```csharp
[Test]
public void TestSliderStillSelectableViaSelectionPointAfterHeadInteractive()
{
    waitForComposer();
    placeDiagonalSlider();

    AddStep("select slider via selection point", () =>
    {
        input.MoveMouseTo(sliderBlueprint().ScreenSpaceSelectionPoint);
        input.Click(MouseButton.Left);
    });
    AddAssert("whole slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
    AddAssert("head not selected (first click selects whole slider)", () => sliderBlueprint().HeadSelected, () => Is.False);
}
```

Rationale: on an unselected slider the blueprint's handles are not input-active (the base `ShouldBeConsideredForInput` gate), so a click on the head selects the whole slider first — head selection needs a second click. This matches the node behaviour pinned in `TestClickingNodeOnUnselectedSliderSelectsWholeSlider`.

- [ ] **Step 5: Run the Task-5 tests**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestQuickDeleteHeadHandlePromotesFirstNode|TestSliderStillSelectableViaSelectionPointAfterHeadInteractive"`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: shift+right-click deletes slider head"
```

---

### Task 6: Head handle per visible wrap copy

The head square is a single drawable pinned to the primary column; give it a ghost-band clone per visible wrap copy (like node handles) so a seam-adjacent head is clickable on every visible copy, and re-point `ReceivePositionalInputAt`/anchors at the primary copy.

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `wrapCopiesBuffer`, `EditorAngleMapping.GridOffset`, existing per-frame handle placement.
- Produces: a `Container<EditSquarePiece> headHandles` replacing the single `head`; `head` becomes the primary (`WrapK == 0`) copy for anchor purposes.

- [ ] **Step 1: Write failing test**

Use a seam-adjacent head so it produces more than one visible wrap copy, then click a non-primary copy:

```csharp
[Test]
public void TestClickingGhostBandCloneOfHeadSelectsIt()
{
    waitForComposer();

    // Head near the left seam (grid 20°, absolute 110°); the head's raw copy sits in the left ghost band,
    // its k=+1 clone lands in the right main grid — so more than one head handle is visible.
    AddStep("add seam-adjacent slider + park clock", () =>
    {
        var path = new osu.Framework.Bindables.BindableList<GarbusPathControlPoint>
        {
            new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 10 },
        };
        editorChart.Add(new SliderBody
        {
            StartTime = 2000,
            AngleDeg = 110, // grid 20
            Side = HorizontalDirection.Left,
            Path = new GarbusPath { ControlPoints = path },
        });
        editorClock.Stop();
        editorClock.Seek(2000);
    });
    AddUntilStep("drawable exists", () => composer.HitObjects.Any());
    settleWith(() => placedObject<SliderBody>()!.StartTime);
    AddStep("switch to select tool", () => input.Key(Key.Number1));
    AddStep("select slider via selection point", () =>
    {
        input.MoveMouseTo(sliderBlueprint().ScreenSpaceSelectionPoint);
        input.Click(MouseButton.Left);
    });
    AddAssert("slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());

    AddAssert("multiple head handles exist", () =>
        sliderBlueprint().ChildrenOfType<Garbus.Game.Edit.Blueprints.Components.EditSquarePiece>().Count(h => h.CpIndex == -1),
        () => Is.GreaterThan(1));

    AddStep("click a non-primary head copy", () =>
    {
        var clone = sliderBlueprint().ChildrenOfType<Garbus.Game.Edit.Blueprints.Components.EditSquarePiece>()
                                     .First(h => h.CpIndex == -1 && h.WrapK != 0);
        input.MoveMouseTo(clone.ScreenSpaceDrawQuad.Centre);
        input.Click(MouseButton.Left);
    });
    AddAssert("head selected via the clone", () => sliderBlueprint().HeadSelected, () => Is.True);
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestClickingGhostBandCloneOfHeadSelectsIt"`
Expected: FAIL — only one head `EditSquarePiece` exists ("multiple head handles" assertion fails).

- [ ] **Step 3: Replace single `head` with a `headHandles` container**

In the fields (line 57) replace:

```csharp
    private EditSquarePiece head = null!;
```

with:

```csharp
    private Container<EditSquarePiece> headHandles = null!;

    // The primary (WrapK == 0) head copy, used for the selection anchor points. Reassigned each frame.
    private EditSquarePiece head = null!;
```

In `load()` (lines 85-97) replace the `head = new EditSquarePiece { ... }` element with a container, keeping the same z-order (outline behind, head/nodes above):

```csharp
        InternalChildren = new Drawable[]
        {
            outlineContainer = new Container { RelativeSizeAxes = Axes.Both, Colour = yellow },
            headHandles = new Container<EditSquarePiece> { RelativeSizeAxes = Axes.Both },
            nodeHandles = new Container<NodeDragPiece> { RelativeSizeAxes = Axes.Both },
        };
```

- [ ] **Step 4: Populate head handles per wrap copy in `Update()`**

The head sits at grid offset 0 (rotation offset 0). It shares the same `wrapCopiesBuffer` the node handles use. After `wrapCopiesBuffer` is filled (line 141) and before/alongside the node-handle sizing, add head-handle management. Insert right after the node-handle placement loop closes (after line 176, before the `selectedNodes.RemoveWhere` prune at line 179):

```csharp
        // Head handles: one per visible wrap copy (offset 0), mirroring the node handles.
        while (headHandles.Count > wrapCopiesBuffer.Count)
            headHandles.Remove(headHandles[^1], true);

        while (headHandles.Count < wrapCopiesBuffer.Count)
        {
            headHandles.Add(new EditSquarePiece
            {
                RelativeSizeAxes = Axes.X,
                Height = EditorDrawableCardinalNote.NOTE_SIZE,
                Origin = Anchor.Centre,
                CpIndex = head_index,
                SelectRequested = (index, ctrl) => selectNode(index, ctrl),
                DragStarted = () => changeHandler?.BeginChange(),
                Dragging = (index, pos) => dragNode(index, pos),
                DragEnded = () => changeHandler?.EndChange(),
            });
        }

        for (int hi = 0; hi < wrapCopiesBuffer.Count; hi++)
        {
            int k = wrapCopiesBuffer[hi];
            var hh = headHandles[hi];
            hh.WrapK = k;
            hh.Position = new Vector2(
                DrawWidth / 2 + (EditorAngleMapping.GridOffset(0) - k * 360) * pxPerDeg,
                DrawHeight);
            hh.NodeSelected = headSelected;
            head = hh.WrapK == 0 ? hh : head;
        }

        // Fall back to the first head handle if no primary (WrapK 0) copy is visible.
        if (head == null || !headHandles.Contains(head))
            head = headHandles.Count > 0 ? headHandles[0] : null!;
```

Delete the now-obsolete `head.NodeSelected = headSelected;` line added in Task 2 Step 4 (the loop above sets each copy's fill).

Note: the head sits at the slider's start line, so its y is `DrawHeight` (the trailing/bottom edge, matching the old `Anchor.BottomCentre` placement). Node y is `DrawHeight * (1 - TimeOffset/duration)`; at offset 0 that is `DrawHeight`, consistent.

Also handle the `duration <= 0` early-return branch (lines 118-125): clear head handles there too. Replace that block's body with:

```csharp
        if (duration <= 0)
        {
            while (nodeHandles.Count > 0)
                nodeHandles.Remove(nodeHandles[^1], true);

            while (headHandles.Count > 0)
                headHandles.Remove(headHandles[^1], true);

            head = null!;
            clearOutline();
            return;
        }
```

- [ ] **Step 5: Guard the anchor accessors against a null primary head**

`ScreenSpaceSelectionPoint` and `FinalNodeScreenPosition` read `head.ScreenSpaceDrawQuad.Centre`. With `head` now possibly null for a frame before the first `Update()`, guard them. Replace `ScreenSpaceSelectionPoint` (line 547):

```csharp
    public override Vector2 ScreenSpaceSelectionPoint =>
        head?.ScreenSpaceDrawQuad.Centre ?? ScreenSpaceDrawQuad.Centre;
```

In `FinalNodeScreenPosition` (lines 551-575) replace the three `head.ScreenSpaceDrawQuad.Centre` fallbacks with `headScreen()` where:

```csharp
    private Vector2 headScreen() => head?.ScreenSpaceDrawQuad.Centre ?? ScreenSpaceDrawQuad.Centre;
```

i.e. `if (controlPoints.Count == 0) return headScreen();` and `return chosen?.ScreenSpaceDrawQuad.Centre ?? headScreen();`.

- [ ] **Step 6: Update `ReceivePositionalInputAt` to test all head handles**

Replace the single `head.ReceivePositionalInputAt` check (lines 255-256) with a loop:

```csharp
        foreach (var handle in headHandles)
        {
            if (handle.ReceivePositionalInputAt(screenSpacePos))
                return true;
        }
```

- [ ] **Step 7: Fix `HandleQuickDeletion`'s head-hover check**

The Task-5 code used `head.IsHovered`; now any head copy may be hovered. Replace that check:

```csharp
        if (headHandles.Any(h => h.IsHovered))
        {
            headSelected = true;
            selectedNodes.Clear();
            removeSelection();
            return true;
        }
```

Add `using System.Linq;` if not already present (it is — line 9).

- [ ] **Step 8: Run the Task-6 test + full scene**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestClickingGhostBandCloneOfHeadSelectsIt"`
Expected: PASS.

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneComposeSelection"`
Expected: PASS (all, including the Task 2–5 head tests, which now find the head via `headHandleScreen()`'s `CpIndex == -1` filter — still valid).

- [ ] **Step 9: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: render slider head handle per visible wrap copy"
```

---

### Task 7: Inspector — head-aware node poll and count

The Inspector polls `SelectedNodes` for changes but can't see `HeadSelected`; a head-only pick never triggers a rebuild, and the "Selected Nodes" count omits the head.

**Files:**
- Modify: `Garbus.Game/Edit/Inspector.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `SliderSelectionBlueprint.HeadSelected` (Task 2).
- Produces: Inspector rebuilds on head-selection change; count includes the head.

- [ ] **Step 1: Write failing test**

The Inspector lives in the right toolbox. Assert its rendered "Selected Nodes" value reflects a head-only selection. The simplest robust probe reads the Inspector's text; since the harness may not mount the full toolbox, instead assert the observable contract directly through a lightweight Inspector test in the compose scene. Add:

```csharp
[Test]
public void TestInspectorCountsHeadAsSelectedNode()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    AddStep("select head", () => { input.MoveMouseTo(headHandleScreen()); input.Click(MouseButton.Left); });

    AddUntilStep("inspector shows one selected node (the head)", () =>
    {
        var inspector = composer.ChildrenOfType<Inspector>().FirstOrDefault();
        if (inspector == null) return false;
        var text = inspector.ChildrenOfType<osu.Framework.Graphics.Sprites.SpriteText>()
                            .Select(t => t.Text.ToString()).ToList();
        // "Selected Nodes:" header followed by a "1" value paragraph.
        return text.Contains("Selected Nodes:") && text.Contains("1");
    });
}
```

If `Inspector` is not present in the compose harness, this test's `FirstOrDefault()` stays null and the `AddUntilStep` will time out — in that case (verify while running) fall back to asserting `sliderBlueprint().HeadSelected` and move the count assertion to a dedicated Inspector unit check. Confirm harness membership before finalising: search the harness for `Inspector`.

- [ ] **Step 2: Confirm the harness mounts the Inspector**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestInspectorCountsHeadAsSelectedNode"`
Expected: FAIL — either the count reads "0"/absent (head not counted) or the Inspector isn't mounted. If the Inspector isn't in the compose harness, the assertion times out; in that case adjust the test to construct the real `Inspector` bound to the same `editorChart`/`composer` (mirror how `TestSceneSetupTab` mounts its form) rather than pulling it from the composer tree.

- [ ] **Step 3: Make the poll head-aware**

In `Inspector.cs`, the per-frame `Update()` (line 99) compares `collectSelectedNodes()` to `lastNodeSelectionSnapshot`. Head state isn't in that set. Add a parallel snapshot of head-selected sliders.

Add a field beside `lastNodeSelectionSnapshot` (line 51):

```csharp
        // Tracked so a head-selection change (also not event-observable) triggers a rebuild.
        private readonly HashSet<SliderBody> lastHeadSelectionSnapshot = new HashSet<SliderBody>();
```

Add a collector beside `collectSelectedNodes()` (after line 124):

```csharp
        private HashSet<SliderBody> collectHeadSelectedSliders()
        {
            var set = new HashSet<SliderBody>();

            foreach (var blueprint in composer.BlueprintContainer.SelectionHandler.SelectedBlueprints)
            {
                if (blueprint is SliderSelectionBlueprint { HeadSelected: true } sliderBlueprint
                    && sliderBlueprint.Item is SliderBody body)
                {
                    set.Add(body);
                }
            }

            return set;
        }
```

Update `Update()` (lines 99-108):

```csharp
        protected override void Update()
        {
            base.Update();

            var currentNodes = collectSelectedNodes();
            var currentHeads = collectHeadSelectedSliders();
            if (!currentNodes.SetEquals(lastNodeSelectionSnapshot) || !currentHeads.SetEquals(lastHeadSelectionSnapshot))
                rebuild();
        }
```

In `rebuild()` (lines 126-147), after refreshing `lastNodeSelectionSnapshot` (line 137-138) add:

```csharp
            lastHeadSelectionSnapshot.Clear();
            foreach (var h in collectHeadSelectedSliders()) lastHeadSelectionSnapshot.Add(h);
```

- [ ] **Step 4: Count the head in the summary**

In `writeSummary` (line 205-209) the "Selected Nodes" block counts `selectedNodes.Count`. Add the head count. Change the signature to also receive the head-selected sliders, or recompute inline. Simplest: recompute in `writeSummary` via a passed count. Update `rebuild()` (line 140) to compute a combined node count and pass it. Replace the summary's node block:

```csharp
            int headCount = collectHeadSelectedSliders().Count;
            if (selectedNodes.Count + headCount > 0)
            {
                addHeader("Selected Nodes");
                addValue($"{selectedNodes.Count + headCount}");
            }
```

(`collectHeadSelectedSliders()` is cheap — a walk of the current selection — and `writeSummary` runs only on rebuild, not per frame.)

Also update the rebuild guard (line 145) so a head-only selection keeps the rolling refresh alive:

```csharp
            if (objects.Length > 0 || selectedNodes.Count > 0 || collectHeadSelectedSliders().Count > 0)
                rollingUpdate ??= Scheduler.AddDelayed(rebuild, 250);
```

- [ ] **Step 5: Run the Task-7 test**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestInspectorCountsHeadAsSelectedNode"`
Expected: PASS.

- [ ] **Step 6: Build + run editor test suites**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneComposeSelection|Inspector"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/Inspector.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: count and track the slider head in the inspector"
```

---

### Task 8: Flip / rotate the head

Extend `GarbusSelectionHandler`'s flip so a selection that includes the head reflects the head in absolute space and compensates unselected nodes — a winding-preserving involution.

**Files:**
- Modify: `Garbus.Game/Edit/GarbusSelectionHandler.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `SliderSelectionBlueprint.HeadSelected` (Task 2), `reflectSelectedNodes`, `handleAngles`.
- Produces: `flip`/`handleAngles`/`reflectSelectedNodes` head-aware.

- [ ] **Step 1: Write failing test**

Reflection about the selection's own centre is an involution: flipping twice restores every angle. With head + a node selected, unselected nodes must keep their absolute angle. Add:

```csharp
[Test]
public void TestFlipWithHeadReflectsAndIsInvolution()
{
    waitForComposer();
    placeDiagonalSlider();  // head 270°, node absolute 0°
    addSecondNode();        // second node absolute 90°+... (offset 90 from head)
    selectSliderOnLine();

    int origHeadAngle = 0, origCp0 = 0, origCp1 = 0;
    double origStart = 0, origCp0Time = 0, origCp1Time = 0;
    AddStep("capture originals", () =>
    {
        var s = placedObject<SliderBody>()!;
        origHeadAngle = s.AngleDeg;
        origCp0 = s.Path.ControlPoints[0].RotationOffset;
        origCp1 = s.Path.ControlPoints[1].RotationOffset;
        origStart = s.StartTime;
        origCp0Time = s.Path.ControlPoints[0].TimeOffset;
        origCp1Time = s.Path.ControlPoints[1].TimeOffset;
    });

    // Select head + node 0 (leave node 1 unselected).
    AddStep("select head", () => { input.MoveMouseTo(headHandleScreen()); input.Click(MouseButton.Left); });
    AddStep("ctrl+click node 0", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(0));
        input.PressKey(Key.LControl);
        input.Click(MouseButton.Left);
        input.ReleaseKey(Key.LControl);
    });

    int node1AbsBefore = 0;
    AddStep("capture node1 absolute", () =>
    {
        var s = placedObject<SliderBody>()!;
        node1AbsBefore = EditorAngleMapping.NormalizeDeg(s.AngleDeg + s.Path.ControlPoints[1].RotationOffset);
    });

    AddStep("flip selection", () =>
    {
        var handler = composer.ChildrenOfType<GarbusSelectionHandler>().Single();
        input.MoveMouseTo(headHandleScreen()); // ensure a blueprint is hovered for the key handler
        input.Key(Key.F);
    });

    AddAssert("unselected node1 kept absolute angle", () =>
    {
        var s = placedObject<SliderBody>()!;
        return EditorAngleMapping.NormalizeDeg(s.AngleDeg + s.Path.ControlPoints[1].RotationOffset);
    }, () => Is.EqualTo(node1AbsBefore));

    AddStep("flip again", () => { input.MoveMouseTo(headHandleScreen()); input.Key(Key.F); });

    AddAssert("head angle restored (involution)", () => placedObject<SliderBody>()!.AngleDeg, () => Is.EqualTo(origHeadAngle));
    AddAssert("cp0 offset restored", () => placedObject<SliderBody>()!.Path.ControlPoints[0].RotationOffset, () => Is.EqualTo(origCp0));
    AddAssert("cp1 offset restored", () => placedObject<SliderBody>()!.Path.ControlPoints[1].RotationOffset, () => Is.EqualTo(origCp1));
    AddAssert("times untouched by flip", () =>
    {
        var s = placedObject<SliderBody>()!;
        return s.StartTime == origStart
            && s.Path.ControlPoints[0].TimeOffset == origCp0Time
            && s.Path.ControlPoints[1].TimeOffset == origCp1Time;
    });
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestFlipWithHeadReflectsAndIsInvolution"`
Expected: FAIL — with head selected but `SelectedNodes.Count > 0` guard matching only nodes, the flip either reflects just the node subset (head angle unchanged → involution asserts still pass but the "unselected node1 kept absolute angle" fails because reflecting cp0 about the node-only centre moves node1's absolute angle) — confirm the exact failing assertion when running.

- [ ] **Step 3: Extend the flip guards to include the head**

In `flip` (line 242) change the slider-with-nodes case guard:

```csharp
                case SliderBody slider when blueprint is SliderSelectionBlueprint sb && (sb.SelectedNodes.Count > 0 || sb.HeadSelected):
                    reflectSelectedNodes(slider, sb, mode, pivotDeg);
                    break;
```

In `handleAngles` (line 325) change the matching case and yield the head angle when selected:

```csharp
                case SliderBody slider when blueprint is SliderSelectionBlueprint sb && (sb.SelectedNodes.Count > 0 || sb.HeadSelected):
                    if (sb.HeadSelected)
                        yield return EditorAngleMapping.NormalizeDeg(slider.AngleDeg);
                    foreach (var cp in sb.SelectedNodes)
                        yield return EditorAngleMapping.NormalizeDeg(slider.AngleDeg + cp.RotationOffset);
                    break;
```

- [ ] **Step 4: Make `reflectSelectedNodes` move the head**

Reflection maps head-relative offset `x → S − x`. The head is at offset 0 → it moves by `S`; expressed as absolute mutations that keep unselected nodes fixed:
- `AngleDeg += S`;
- selected nodes: `RotationOffset = S − RotationOffset`, then re-based to the new head by subtracting `S` → net `RotationOffset = −RotationOffset`;
- unselected nodes: absolute angle fixed while `AngleDeg` grew by `S` → `RotationOffset -= S`.

Replace `reflectSelectedNodes` (lines 281-310). Keep the `SelectionCentre`/`AroundPivot` sum computation; only the mutation tail changes to branch on `sb.HeadSelected`:

```csharp
    private static void reflectSelectedNodes(SliderBody slider, SliderSelectionBlueprint sb, FlipMode mode, int pivotDeg)
    {
        int s;

        if (mode == FlipMode.SelectionCentre)
        {
            int minOff = int.MaxValue, maxOff = int.MinValue;

            // The head participates in the centre when selected (its offset is 0).
            if (sb.HeadSelected)
            {
                minOff = Math.Min(minOff, 0);
                maxOff = Math.Max(maxOff, 0);
            }

            foreach (var cp in sb.SelectedNodes)
            {
                minOff = Math.Min(minOff, cp.RotationOffset);
                maxOff = Math.Max(maxOff, cp.RotationOffset);
            }

            s = minOff + maxOff;
        }
        else
        {
            int axisOff = EditorAngleMapping.MinimalDiff(slider.AngleDeg, pivotDeg);
            if (axisOff > 90)
                axisOff -= 180;
            else if (axisOff <= -90)
                axisOff += 180;

            s = 2 * axisOff;
        }

        if (!sb.HeadSelected)
        {
            // Head fixed: reflect only the selected nodes in offset space.
            foreach (var cp in sb.SelectedNodes)
                cp.RotationOffset = s - cp.RotationOffset;

            return;
        }

        // Head moves by S. Shift the head, re-base selected nodes (S - x, then - S = -x), and compensate the
        // unselected nodes (which must keep their absolute angle while AngleDeg grew by S).
        slider.AngleDeg = EditorAngleMapping.NormalizeDeg(slider.AngleDeg + s);

        foreach (var cp in slider.Path.ControlPoints)
        {
            if (sb.SelectedNodes.Contains(cp))
                cp.RotationOffset = -cp.RotationOffset;
            else
                cp.RotationOffset -= s;
        }
    }
```

Verify the involution: applying twice, the second pass recomputes `S' = min'+max'`. For `SelectionCentre`, selected offsets became `-x` (so their min/max negate and swap → `min' + max' = -(min+max) = -S`), and head stays at 0 → `S' = -S`. Then head angle `+= -S` returns to original; selected `-(-x) = x`; unselected `(x - S) - S'` = `x - S + S = x`. Restored. ✓

- [ ] **Step 5: Run the Task-8 test**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestFlipWithHeadReflectsAndIsInvolution"`
Expected: PASS.

- [ ] **Step 6: Run full selection + flip suites**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneComposeSelection"`
Expected: PASS (all, including the pre-existing node-only flip tests, which take the `!sb.HeadSelected` branch unchanged).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/GarbusSelectionHandler.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: flip slider head with the rest of the selection"
```

---

### Task 9: Full-suite verification + spec sync

**Files:**
- Modify: `docs/superpowers/specs/2026-07-15-slider-head-node-selection-design.md` (status line only)

- [ ] **Step 1: Build the whole desktop solution**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: build succeeds, no warnings introduced.

- [ ] **Step 2: Run the whole headless test suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all green (existing + ~15 new head tests).

- [ ] **Step 3: Manual smoke via the run skill (optional but recommended)**

Launch the editor, place a slider, select it, click the head handle, drag it, delete it, flip it. Confirm no GC storm / drawable churn during head drag (the same class of bug as the node-drag storm the plan's in-place-update constraint guards against).

Run: `dotnet run --project Garbus.Desktop`

- [ ] **Step 4: Update the spec status**

In `docs/superpowers/specs/2026-07-15-slider-head-node-selection-design.md`, change the `Status:` line to `implemented`.

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/2026-07-15-slider-head-node-selection-design.md
git commit -m "docs: mark slider head-node selection implemented"
```

---

## Self-Review

**Spec coverage:**
- Selection state / `HeadSelected` / click routing / clear-on-deselect / clear-on-body-click → Task 2. ✓
- Head handle interactive widget → Task 1; per-wrap-copy → Task 6. ✓
- Unified drag with head compensation + `StartTime >= 0` + `AreTimesValid` → Task 3. ✓
- Delete = promote first node; head+all = remove slider; undo → Task 4. ✓
- Quick-delete head; anchor re-pointing → Tasks 5 & 6. ✓
- Easing skips the head → **already satisfied by construction**: `setSelectedNodesEasing` and the Inspector Easing dropdown iterate `selectedNodes`, which never contains the head (it's a bool, not a set member). No code change needed; noted here so a reviewer doesn't look for a task. The Inspector Easing dropdown's visibility is gated on `selectedNodes.Count > 0` (unchanged), so a head-only selection shows no Easing control. ✓
- Inspector poll/snapshot/count → Task 7. ✓
- Flip guards / `handleAngles` / head reflection involution → Task 8. ✓
- No data-model/format/clipboard/gameplay change → confirmed: only blueprint, inspector, selection-handler, one component widget, and tests are touched. ✓

**Placeholder scan:** No TBD/TODO/"handle edge cases"/"similar to Task N" — every code step shows full code. ✓

**Type consistency:**
- `head_index = -1` sentinel used consistently in `EditSquarePiece.CpIndex` default (Task 1), `selectNode`/`dragNode` (Tasks 2–3), head-handle creation (Tasks 2 & 6).
- `HeadSelected` (property) / `headSelected` (field) consistent across blueprint, Inspector (Task 7), selection handler (Task 8).
- `timeShiftValidWithHead(controlPoints, movedNodes, headInSet, deltaTime)` defined in Task 3, called in Task 3's `dragNode` — the old `timeShiftValid` is deleted, no dangling callers.
- `removeSelection()` defined Task 4, called from `OnPressed` (Task 4) and `HandleQuickDeletion` (Tasks 5 & 6).
- `head` field repurposed from `EditSquarePiece` (Tasks 1–5) to the primary-copy pointer inside `headHandles` (Task 6); all readers (`ReceivePositionalInputAt`, anchors, quick-delete) updated in Task 6. A reviewer executing tasks in order sees `head` as a single drawable through Task 5, then the Task-6 diff converts it — the intermediate state compiles and passes at each task boundary.

**Note on task independence:** Tasks 2–5 treat `head` as a single `EditSquarePiece`; Task 6 converts it to a container + primary pointer. This is deliberate incremental delivery (each task green), not an inconsistency — Task 6 explicitly rewrites the anchor accessors and quick-delete check that Tasks 2/5 established.
