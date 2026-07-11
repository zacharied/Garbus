# Slider Node Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the editor select individual slider control-point nodes — click a node handle on an already-selected slider to pick it, multi-select with Ctrl, group-drag, and delete — while clicking the slider line still selects the whole slider as today.

**Architecture:** All state and behaviour live inside `SliderSelectionBlueprint` (osu's `PathControlPointVisualiser` pattern). The blueprint owns a `HashSet<GarbusPathControlPoint>` of selected nodes keyed on the stable control-point references. `NodeDragPiece` gains a selected visual state and a mouse-down selection callback. No changes to the global selection model (`EditorChart.SelectedHitObjects`), the undo JSON diff, or the clipboard.

**Tech Stack:** C# / .NET, osu-framework (drawables, input events, `IKeyBindingHandler<PlatformAction>`), NUnit headless visual tests via `ManualInputManager`.

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL fields use `= null!`.
- Vendored osu.Game files keep the ppy MIT header; these two files are BAC ports (not vendored) — keep their existing "Ported from BigAssCircle" header lines.
- Terminology: osu "beatmap" → "chart"; `Bac*` → `Garbus*`.
- Control points are stable reference instances in a `BindableList<GarbusPathControlPoint>`; node identity is by reference (never by index/value) — `SliderBody.GetSegmentStartTime` already relies on this.
- Node mutations go through `changeHandler.BeginChange()` / `EndChange()` and `editorChart.Update(HitObject)` (or `editorChart.Remove(HitObject)` for whole-slider removal) so undo/redo works via the existing snapshot diff. Fire `EditorChart.Update` only when something actually changed (avoids the drawable-refresh churn documented in CLAUDE.md).
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.

---

## File Structure

- `Garbus.Game/Edit/Blueprints/Components/NodeDragPiece.cs` — *modify*. Adds a selected fill visual and a left-mouse-down selection callback (`SelectRequested`). Still owns the drag callbacks.
- `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs` — *modify*. Owns `selectedNodes`, prunes/clears it, wires node handles' selection + visual state, group-drags selected nodes, and handles Delete / quick-delete with the empty-path→remove-slider rule.
- `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` — *modify*. Adds node-selection, group-drag, and deletion coverage using the existing harness and helpers (`placeDiagonalSlider`, `sliderEndsScreen`, `settleWith`).

No chart-format, serialization, or gameplay changes.

---

## Task 1: Node selection state + click-to-select (single, Ctrl toggle) + visual

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/Components/NodeDragPiece.cs`
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Produces (`NodeDragPiece`):
  - `public Action<bool>? SelectRequested { get; init; }` — invoked on left mouse-down with the Ctrl-pressed flag.
  - `public bool NodeSelected { get; set; }` — pushed by the blueprint each frame; drives the fill visual.
- Produces (`SliderSelectionBlueprint`):
  - `internal IReadOnlyCollection<GarbusPathControlPoint> SelectedNodes` — exposes `selectedNodes` for tests/assertions.

- [ ] **Step 1: Write the failing tests**

Add these tests to `TestSceneComposeSelection.cs` (they rely on the existing `placeDiagonalSlider`, `sliderEndsScreen`, `settleWith`, `waitForComposer`, `placedObject`, `composer`, `input`, `editorChart`). Add a small helper to grab a node handle. Put the helper near `sliderEndsScreen`:

```csharp
/// <summary>The single-node slider's one control point.</summary>
private GarbusPathControlPoint firstControlPoint() => placedObject<SliderBody>()!.Path.ControlPoints[0];

/// <summary>Adds a second control point directly (later in time, different angle) and refreshes.</summary>
private void addSecondNode()
{
    AddStep("add second node", () =>
    {
        var slider = placedObject<SliderBody>()!;
        slider.Path.ControlPoints.Add(new GarbusPathControlPoint { TimeOffset = slider.Path.ControlPoints[0].TimeOffset + 250, RotationOffset = 90 });
        editorChart.Update(slider);
    });
    settleWith(() => placedObject<SliderBody>()!.StartTime);
}

/// <summary>Screen centre of the node handle at control-point index <paramref name="i"/>.</summary>
private Vector2 nodeHandleScreen(int i)
{
    var handles = composer.ChildrenOfType<NodeDragPiece>().ToList();
    return handles[i].ScreenSpaceDrawQuad.Centre;
}

private SliderSelectionBlueprint sliderBlueprint() => composer.ChildrenOfType<SliderSelectionBlueprint>().Single();

/// <summary>Selects the slider by clicking the midpoint of its head→node line.</summary>
private void selectSliderOnLine()
{
    AddStep("select slider on its line", () =>
    {
        var (headScreen, nodeScreen) = sliderEndsScreen();
        input.MoveMouseTo((headScreen + nodeScreen) / 2);
        input.Click(MouseButton.Left);
    });
    AddAssert("slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
}
```

```csharp
[Test]
public void TestClickingNodeOnUnselectedSliderSelectsWholeSlider()
{
    waitForComposer();
    placeDiagonalSlider();

    // slider is not selected; clicking its node handle position must select the whole slider, no node.
    AddStep("click the node position", () =>
    {
        var (_, nodeScreen) = sliderEndsScreen();
        input.MoveMouseTo(nodeScreen);
        input.Click(MouseButton.Left);
    });
    AddAssert("whole slider selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
    AddAssert("no node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
}

[Test]
public void TestClickingHandleOnSelectedSliderSelectsNode()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    AddStep("click the node handle", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(0));
        input.Click(MouseButton.Left);
    });
    AddAssert("slider still selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
    AddAssert("that node selected", () => sliderBlueprint().SelectedNodes.Single(), () => Is.SameAs(firstControlPoint()));
}

[Test]
public void TestCtrlClickTogglesNodesInSelection()
{
    waitForComposer();
    placeDiagonalSlider();
    addSecondNode();
    selectSliderOnLine();

    AddStep("click node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
    AddStep("ctrl+click node 1", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(1));
        input.PressKey(Key.LControl);
        input.Click(MouseButton.Left);
        input.ReleaseKey(Key.LControl);
    });
    AddAssert("both nodes selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(2));

    AddStep("ctrl+click node 1 again", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(1));
        input.PressKey(Key.LControl);
        input.Click(MouseButton.Left);
        input.ReleaseKey(Key.LControl);
    });
    AddAssert("node 1 toggled off", () => sliderBlueprint().SelectedNodes.Single(), () => Is.SameAs(placedObject<SliderBody>()!.Path.ControlPoints[0]));
}

[Test]
public void TestClickingLineClearsNodeSelection()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    AddStep("click node handle", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
    AddAssert("node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

    AddStep("click the line", () =>
    {
        var (headScreen, nodeScreen) = sliderEndsScreen();
        input.MoveMouseTo((headScreen + nodeScreen) / 2);
        input.Click(MouseButton.Left);
    });
    AddAssert("slider still selected", () => editorChart.SelectedHitObjects.SingleOrDefault() == placedObject<SliderBody>());
    AddAssert("node selection cleared", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
}

[Test]
public void TestDeselectingSliderClearsNodeSelection()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();

    AddStep("click node handle", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
    AddAssert("node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

    AddStep("deselect all", () => editorChart.SelectedHitObjects.Clear());
    AddAssert("node selection cleared", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestClickingHandleOnSelectedSliderSelectsNode|TestCtrlClickTogglesNodesInSelection|TestClickingLineClearsNodeSelection|TestDeselectingSliderClearsNodeSelection|TestClickingNodeOnUnselectedSliderSelectsWholeSlider"`
Expected: FAIL — `SliderSelectionBlueprint` has no `SelectedNodes` member (compile error), or once added, the node-selection assertions fail.

- [ ] **Step 3: Add the selected visual + selection callback to `NodeDragPiece`**

Replace the body of `NodeDragPiece.cs` (keep the header comment) with:

```csharp
using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>A draggable circle handle over a slider control-point node. Fills solid when its node is selected.</summary>
internal partial class NodeDragPiece : CompositeDrawable
{
    public Action? DragStarted { get; init; }
    public Action<Vector2>? Dragging { get; init; }
    public Action? DragEnded { get; init; }

    /// <summary>Invoked on left mouse-down with the Ctrl-pressed flag so the blueprint can update node selection.</summary>
    public Action<bool>? SelectRequested { get; init; }

    private readonly Box fill;

    private bool nodeSelected;

    /// <summary>Whether this handle's node is currently selected; drives the solid fill.</summary>
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

    public NodeDragPiece()
    {
        Size = new Vector2(16);
        Origin = Anchor.Centre;
        InternalChild = new CircularContainer
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
        if (e.Button != MouseButton.Left)
            return base.OnMouseDown(e);

        // Select on the press so a plain click and a drag both start from this node being selected.
        // Returning true stops BlueprintContainer.performMouseDownActions from re-selecting / cycling the
        // whole slider; the slider is already selected (handles only receive input while it is).
        SelectRequested?.Invoke(e.ControlPressed);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e)
    {
        DragStarted?.Invoke();
        return true;
    }

    protected override void OnDrag(DragEvent e)
    {
        base.OnDrag(e);
        Dragging?.Invoke(e.ScreenSpaceMousePosition);
    }

    protected override void OnDragEnd(DragEndEvent e)
    {
        base.OnDragEnd(e);
        DragEnded?.Invoke();
    }
}
```

- [ ] **Step 4: Add node-selection state to `SliderSelectionBlueprint`**

In `SliderSelectionBlueprint.cs`, add the field near the other private fields (after `private SmoothPath? primaryOutline;`):

```csharp
// Node selection is local to this blueprint (osu's PathControlPointVisualiser pattern): a set of the
// stable control-point references. Not part of EditorChart.SelectedHitObjects / undo / clipboard.
private readonly HashSet<GarbusPathControlPoint> selectedNodes = new HashSet<GarbusPathControlPoint>();

internal IReadOnlyCollection<GarbusPathControlPoint> SelectedNodes => selectedNodes;
```

Wire the selection callback when handles are created. In `Update()`, replace the `nodeHandles.Add(...)` block with:

```csharp
while (nodeHandles.Count < controlPoints.Count)
{
    int index = nodeHandles.Count;
    nodeHandles.Add(new NodeDragPiece
    {
        SelectRequested = ctrl => selectNode(index, ctrl),
        DragStarted = () => changeHandler?.BeginChange(),
        Dragging = pos => dragNode(index, pos),
        DragEnded = () => changeHandler?.EndChange(),
    });
}
```

Push the selected flag + prune stale references each frame. In `Update()`, immediately after the loop that positions each handle (after the `for (int i = 0; i < controlPoints.Count; i++)` block that sets `nodeHandles[i].Position`), add the flag push inside that same loop and prune afterwards. Concretely, inside that `for` loop add as its last statement:

```csharp
    nodeHandles[i].NodeSelected = selectedNodes.Contains(cp);
```

And immediately after that loop closes, add:

```csharp
// Drop references orphaned by undo/redo restoring a fresh control-point list.
selectedNodes.RemoveWhere(n => !controlPoints.Contains(n));
```

Add the `selectNode` helper (place it just below `dragNode`):

```csharp
/// <summary>Left-click selection of a node: plain click selects only it; Ctrl toggles it in the set.</summary>
private void selectNode(int index, bool ctrl)
{
    var controlPoints = HitObject.Path.ControlPoints;
    if (index >= controlPoints.Count)
        return;

    var cp = controlPoints[index];

    if (ctrl)
    {
        if (!selectedNodes.Add(cp))
            selectedNodes.Remove(cp);
        return;
    }

    // plain click on a node already in a multi-selection keeps the group (so a drag moves it all);
    // otherwise reduce to just this node.
    if (selectedNodes.Contains(cp))
        return;

    selectedNodes.Clear();
    selectedNodes.Add(cp);
}
```

Clear node selection when the slider is deselected and when its line is clicked. Add these overrides (place after `insertNodeAtCursor`):

```csharp
protected override void OnDeselected()
{
    base.OnDeselected();
    selectedNodes.Clear();
}

protected override bool OnClick(ClickEvent e)
{
    // A click that reached the blueprint body (not consumed by a node handle) clears node selection but
    // leaves the whole-slider selection to BlueprintContainer's own click handling.
    if (e.Button == MouseButton.Left)
        selectedNodes.Clear();

    return base.OnClick(e);
}
```

Ensure the needed usings are present at the top of the file (add any missing): `System.Collections.Generic` (already present), `osu.Framework.Input.Events` (already present, for `ClickEvent`).

- [ ] **Step 5: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeds.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestClickingHandleOnSelectedSliderSelectsNode|TestCtrlClickTogglesNodesInSelection|TestClickingLineClearsNodeSelection|TestDeselectingSliderClearsNodeSelection|TestClickingNodeOnUnselectedSliderSelectsWholeSlider"`
Expected: PASS (5 tests).

- [ ] **Step 7: Run the full existing selection suite to catch regressions**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSceneComposeSelection"`
Expected: PASS — existing tests (path-precise selection, T-insert, node-drag no-recreate, side toggle, chip) still green.

- [ ] **Step 8: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/Components/NodeDragPiece.cs Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: select individual slider nodes by clicking their handles"
```

---

## Task 2: Group-drag selected nodes

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `selectedNodes` (Task 1), `EditorAngleMapping.MinimalDiff`/`NormalizeDeg`, `composer.FindSnappedAngleTimeAndPosition`.
- Produces: `dragNode` now applies the grabbed node's snapped delta to every node in `selectedNodes` (or just the grabbed node when it is not part of a selection), preserving strict time ordering and `TimeOffset > 0`.

- [ ] **Step 1: Write the failing tests**

Add to `TestSceneComposeSelection.cs`:

```csharp
[Test]
public void TestGroupDragMovesAllSelectedNodesByTheSameAngle()
{
    waitForComposer();
    placeDiagonalSlider();      // node 0 at rotation offset from head
    addSecondNode();            // node 1 at +250ms, rotation 90
    selectSliderOnLine();

    AddStep("select node 0", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
    AddStep("ctrl+select node 1", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(1));
        input.PressKey(Key.LControl);
        input.Click(MouseButton.Left);
        input.ReleaseKey(Key.LControl);
    });
    AddAssert("both selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(2));

    int rot0Before = 0, rot1Before = 0;
    AddStep("snapshot rotations", () =>
    {
        var cps = placedObject<SliderBody>()!.Path.ControlPoints;
        rot0Before = cps[0].RotationOffset;
        rot1Before = cps[1].RotationOffset;
    });

    AddStep("press mouse on node 0 handle", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(0));
        input.PressButton(MouseButton.Left);
    });
    AddRepeatStep("drag 3° right", () => dragStepRight(3), 15);   // +45°
    AddStep("release", () => input.ReleaseButton(MouseButton.Left));

    AddAssert("both nodes rotated by the same +45°", () =>
    {
        var cps = placedObject<SliderBody>()!.Path.ControlPoints;
        return cps[0].RotationOffset == rot0Before + 45 && cps[1].RotationOffset == rot1Before + 45;
    });
}

[Test]
public void TestDraggingUnselectedHandleMovesOnlyThatNode()
{
    waitForComposer();
    placeDiagonalSlider();
    addSecondNode();
    selectSliderOnLine();

    int rot1Before = 0;
    AddStep("snapshot node 1 rotation", () => rot1Before = placedObject<SliderBody>()!.Path.ControlPoints[1].RotationOffset);

    // No node explicitly selected yet; pressing node 0's handle selects it, then dragging moves only it.
    AddStep("press mouse on node 0 handle", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(0));
        input.PressButton(MouseButton.Left);
    });
    AddRepeatStep("drag 3° right", () => dragStepRight(3), 15);
    AddStep("release", () => input.ReleaseButton(MouseButton.Left));

    AddAssert("only node 0 selected", () => sliderBlueprint().SelectedNodes.Single(), () => Is.SameAs(placedObject<SliderBody>()!.Path.ControlPoints[0]));
    AddAssert("node 1 unchanged", () => placedObject<SliderBody>()!.Path.ControlPoints[1].RotationOffset, () => Is.EqualTo(rot1Before));
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestGroupDragMovesAllSelectedNodesByTheSameAngle|TestDraggingUnselectedHandleMovesOnlyThatNode"`
Expected: FAIL — `TestGroupDrag...` fails because only the grabbed node moves (node 1's rotation unchanged).

- [ ] **Step 3: Rewrite `dragNode` to move the whole selected group**

Replace the entire `dragNode` method with:

```csharp
private void dragNode(int index, Vector2 screenSpacePosition)
{
    if (composer == null || editorChart == null)
        return;

    var controlPoints = HitObject.Path.ControlPoints;
    if (index >= controlPoints.Count)
        return;

    var grabbed = controlPoints[index];
    var result = composer.FindSnappedAngleTimeAndPosition(screenSpacePosition);

    // The moved set: the whole node selection when the grabbed node is part of it, else just the grabbed node.
    var moved = selectedNodes.Contains(grabbed) && selectedNodes.Count > 0
        ? new List<GarbusPathControlPoint>(selectedNodes)
        : new List<GarbusPathControlPoint> { grabbed };

    bool changed = false;

    // Time: shift every moved node by the grabbed node's delta, but only if the whole path stays
    // strictly time-ordered and every offset stays > 0. All-or-nothing per event (no partial move).
    if (result.Time is double proposedTime)
    {
        double deltaTime = (proposedTime - HitObject.StartTime) - grabbed.TimeOffset;

        if (deltaTime != 0 && timeShiftValid(controlPoints, moved, deltaTime))
        {
            foreach (var cp in moved)
                cp.TimeOffset += deltaTime;
            changed = true;
        }
    }

    // Angle: rotation offsets are free integers (no ordering constraint), so apply the grabbed node's
    // minimal snap delta to every moved node unconditionally.
    if (result is GarbusSnapResult snap)
    {
        int currentAbsolute = EditorAngleMapping.NormalizeDeg(HitObject.AngleDeg + grabbed.RotationOffset);
        int diff = EditorAngleMapping.MinimalDiff(currentAbsolute, snap.AngleDeg);

        if (diff != 0)
        {
            foreach (var cp in moved)
                cp.RotationOffset += diff;
            changed = true;
        }
    }

    // Only run the (ApplyDefaults + state-save) update when something actually moved — mouse-move
    // events inside the same snap cell would otherwise re-apply the whole slider per event.
    if (changed)
        editorChart.Update(HitObject);
}

/// <summary>
/// True if shifting every node in <paramref name="moved"/> by <paramref name="deltaTime"/> keeps the full
/// control-point list strictly increasing in time and every offset above zero (nodes must follow the head).
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

Ensure `using System.Collections.Generic;` is present (it is).

- [ ] **Step 4: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeds.

- [ ] **Step 5: Run to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestGroupDragMovesAllSelectedNodesByTheSameAngle|TestDraggingUnselectedHandleMovesOnlyThatNode"`
Expected: PASS.

- [ ] **Step 6: Re-run the node-drag no-recreate regression guard**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestSliderNodeDragDoesNotRecreateDrawable"`
Expected: PASS — single-node drag still updates in place, no drawable recreation.

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: group-drag selected slider nodes together"
```

---

## Task 3: Delete selected node(s) via Delete key; empty path removes the slider

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `selectedNodes` (Task 1), `changeHandler`, `editorChart`.
- Produces: `SliderSelectionBlueprint` implements `IKeyBindingHandler<PlatformAction>`; `OnPressed(PlatformAction.Delete)` removes selected nodes (returns true) or defers to whole-slider deletion (returns false) when no node is selected. A private `removeNodes(...)` helper performs the removal with the empty-path→remove-slider rule.

- [ ] **Step 1: Write the failing tests**

Add to `TestSceneComposeSelection.cs`:

```csharp
[Test]
public void TestDeleteRemovesSelectedNodeNotWholeSlider()
{
    waitForComposer();
    placeDiagonalSlider();
    addSecondNode();            // now two control points
    selectSliderOnLine();

    var node0 = new System.Func<GarbusPathControlPoint>(() => placedObject<SliderBody>()!.Path.ControlPoints[0]);

    AddStep("select node 1", () => { input.MoveMouseTo(nodeHandleScreen(1)); input.Click(MouseButton.Left); });
    AddAssert("one node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

    var remaining = new GarbusPathControlPoint[1];
    AddStep("remember node 0", () => remaining[0] = node0());

    AddStep("press delete", () => input.Key(Key.Delete));
    AddAssert("slider survives", () => placedObject<SliderBody>() != null);
    AddAssert("one control point left", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));
    AddAssert("the other node remains", () => placedObject<SliderBody>()!.Path.ControlPoints[0], () => Is.SameAs(remaining[0]));
    AddAssert("node selection cleared", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
}

[Test]
public void TestDeleteUndoRestoresNode()
{
    waitForComposer();
    placeDiagonalSlider();
    addSecondNode();
    selectSliderOnLine();

    AddStep("select node 1", () => { input.MoveMouseTo(nodeHandleScreen(1)); input.Click(MouseButton.Left); });
    AddStep("press delete", () => input.Key(Key.Delete));
    AddAssert("one control point left", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));

    AddStep("undo", () => changeHandler.RestoreState(-1));
    AddAssert("both control points restored", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(2));
}

[Test]
public void TestDeletingLastNodeRemovesWholeSlider()
{
    waitForComposer();
    placeDiagonalSlider();      // single control point
    selectSliderOnLine();

    AddStep("select the only node", () => { input.MoveMouseTo(nodeHandleScreen(0)); input.Click(MouseButton.Left); });
    AddAssert("node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.EqualTo(1));

    AddStep("press delete", () => input.Key(Key.Delete));
    AddAssert("slider removed entirely", () => placedObject<SliderBody>() == null);
}

[Test]
public void TestDeleteWithNoNodeSelectedRemovesWholeSlider()
{
    waitForComposer();
    placeDiagonalSlider();
    selectSliderOnLine();       // slider selected, but no node picked

    AddAssert("no node selected", () => sliderBlueprint().SelectedNodes.Count, () => Is.Zero);
    AddStep("press delete", () => input.Key(Key.Delete));
    AddAssert("whole slider removed", () => placedObject<SliderBody>() == null);
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestDeleteRemovesSelectedNodeNotWholeSlider|TestDeleteUndoRestoresNode|TestDeletingLastNodeRemovesWholeSlider|TestDeleteWithNoNodeSelectedRemovesWholeSlider"`
Expected: FAIL — `TestDeleteRemovesSelectedNodeNotWholeSlider` and `TestDeleteUndoRestoresNode` fail because Delete removes the whole slider (SelectionHandler handles it today). `TestDeletingLastNodeRemovesWholeSlider` / `TestDeleteWithNoNodeSelectedRemovesWholeSlider` may already pass (whole-slider delete) — that's fine, they lock in the boundary once the new path exists.

- [ ] **Step 3: Implement Delete handling on the blueprint**

Add the interface to the class declaration. Change:

```csharp
internal partial class SliderSelectionBlueprint : GarbusSelectionBlueprint<SliderBody>
```

to:

```csharp
internal partial class SliderSelectionBlueprint : GarbusSelectionBlueprint<SliderBody>, IKeyBindingHandler<PlatformAction>
```

Add the using at the top of the file:

```csharp
using osu.Framework.Input.Bindings;
```

Add the handlers and the shared removal helper (place after the `OnClick` override from Task 1):

```csharp
public bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
{
    // Only intercept Delete when node(s) are picked; otherwise let SelectionHandler delete the whole
    // slider. The blueprint sits above SelectionHandler in the input queue, so it sees the action first.
    if (e.Action != PlatformAction.Delete || selectedNodes.Count == 0)
        return false;

    removeNodes(new List<GarbusPathControlPoint>(selectedNodes));
    return true;
}

public void OnReleased(KeyBindingReleaseEvent<PlatformAction> e)
{
}

/// <summary>
/// Removes the given control points (wrapped in one change transaction). If this empties the path,
/// the slider itself is removed from the chart instead — a path needs at least one node.
/// </summary>
private void removeNodes(IReadOnlyList<GarbusPathControlPoint> nodes)
{
    if (editorChart == null || nodes.Count == 0)
        return;

    var controlPoints = HitObject.Path.ControlPoints;

    changeHandler?.BeginChange();

    if (nodes.Count >= controlPoints.Count)
    {
        editorChart.Remove(HitObject);
    }
    else
    {
        foreach (var cp in nodes)
            controlPoints.Remove(cp);

        editorChart.Update(HitObject);
    }

    selectedNodes.Clear();
    changeHandler?.EndChange();
}
```

- [ ] **Step 4: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeds.

- [ ] **Step 5: Run to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestDeleteRemovesSelectedNodeNotWholeSlider|TestDeleteUndoRestoresNode|TestDeletingLastNodeRemovesWholeSlider|TestDeleteWithNoNodeSelectedRemovesWholeSlider"`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: delete selected slider nodes; empty path removes the slider"
```

---

## Task 4: Shift+RightClick quick-delete of a hovered node handle

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `nodeHandles`, `removeNodes` (Task 3).
- Produces: `SliderSelectionBlueprint.HandleQuickDeletion()` override — deletes the hovered node handle's control point and returns true; returns false (whole-slider delete) when no handle is hovered.

- [ ] **Step 1: Write the failing tests**

Add to `TestSceneComposeSelection.cs`:

```csharp
[Test]
public void TestQuickDeleteHoveredNodeRemovesOnlyThatNode()
{
    waitForComposer();
    placeDiagonalSlider();
    addSecondNode();
    selectSliderOnLine();

    AddStep("shift+right-click node 1 handle", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(1));
        input.PressKey(Key.LShift);
        input.Click(MouseButton.Right);
        input.ReleaseKey(Key.LShift);
    });
    AddAssert("slider survives", () => placedObject<SliderBody>() != null);
    AddAssert("one control point left", () => placedObject<SliderBody>()!.Path.ControlPoints.Count, () => Is.EqualTo(1));
}

[Test]
public void TestQuickDeleteOnLineRemovesWholeSlider()
{
    waitForComposer();
    placeDiagonalSlider();
    addSecondNode();
    selectSliderOnLine();

    AddStep("shift+right-click the line (not a handle)", () =>
    {
        var (headScreen, nodeScreen) = sliderEndsScreen();
        input.MoveMouseTo((headScreen + nodeScreen) / 2);
        input.PressKey(Key.LShift);
        input.Click(MouseButton.Right);
        input.ReleaseKey(Key.LShift);
    });
    AddAssert("whole slider removed", () => placedObject<SliderBody>() == null);
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestQuickDeleteHoveredNodeRemovesOnlyThatNode|TestQuickDeleteOnLineRemovesWholeSlider"`
Expected: FAIL — `TestQuickDeleteHoveredNodeRemovesOnlyThatNode` fails (whole slider removed instead of one node).

- [ ] **Step 3: Override `HandleQuickDeletion`**

Add to `SliderSelectionBlueprint.cs` (place after the `removeNodes` helper from Task 3):

```csharp
public override bool HandleQuickDeletion()
{
    // Shift+RightClick over a node handle deletes just that node; over the line, fall through (return
    // false) so SelectionHandler removes the whole slider.
    for (int i = 0; i < nodeHandles.Count && i < HitObject.Path.ControlPoints.Count; i++)
    {
        if (nodeHandles[i].IsHovered)
        {
            removeNodes(new List<GarbusPathControlPoint> { HitObject.Path.ControlPoints[i] });
            return true;
        }
    }

    return false;
}
```

- [ ] **Step 4: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeds.

- [ ] **Step 5: Run to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestQuickDeleteHoveredNodeRemovesOnlyThatNode|TestQuickDeleteOnLineRemovesWholeSlider"`
Expected: PASS.

- [ ] **Step 6: Run the whole editor test suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS — all headless editor tests green, including the CLAUDE.md-pinned lifecycle/drag guards.

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: quick-delete a hovered slider node with shift+right-click"
```

---

## Task 5: Manual verification + docs

**Files:**
- Modify: `CLAUDE.md` (add a one-line gotcha if any surfaced during implementation; otherwise a brief note that node selection lives in `SliderSelectionBlueprint`).

- [ ] **Step 1: Run the app and exercise the flow**

Run: `dotnet run --project Garbus.Desktop`, open the editor (Compose tab), place a multi-node slider, then verify by hand:
- Click the slider line → whole slider selected (yellow outline, count chip).
- With it selected, click a node handle → that handle fills solid; the slider stays selected.
- Ctrl+click other handles → multi-select; drag one → all move together.
- Delete → selected nodes vanish; deleting the last node removes the slider.
- Shift+right-click a handle → just that node deleted; on the line → whole slider deleted.
- Undo/redo restores each step.

Expected: All behaviours as described; no per-frame GC churn (optionally watch the `Garbus` "Slider polyline rebuilds" statistic via Ctrl+F2 — it must not climb while a slider merely sits selected).

- [ ] **Step 2: Update CLAUDE.md**

Add to the Compose section's notes a short line, e.g.:

```
- Slider **node selection** is local to `SliderSelectionBlueprint` (a `HashSet<GarbusPathControlPoint>`
  by reference) — not part of `EditorChart.SelectedHitObjects`/undo/clipboard. Handles only receive input
  while the slider is selected, so clicking a node on an unselected slider selects the whole slider.
  Delete/`HandleQuickDeletion` remove nodes; emptying the path removes the slider. Pinned by the node
  tests in `TestSceneComposeSelection`.
```

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: note slider node selection lives in SliderSelectionBlueprint"
```

---

## Self-Review Notes

**Spec coverage:**
- Local `HashSet<GarbusPathControlPoint>`, pruned each Update, cleared on deselect → Task 1.
- Selected visual on `NodeDragPiece` → Task 1.
- osu click routing (handles only interact while selected; unselected slider → whole selection) → Task 1 (`TestClickingNodeOnUnselectedSliderSelectsWholeSlider`).
- Ctrl multi-select + clearing on line click → Task 1.
- Group drag with time-ordering / `> 0` guard, angle unconditional, single BeginChange/EndChange, update-only-on-change → Task 2.
- Drag an unselected handle selects it and moves it alone → Task 2 (`TestDraggingUnselectedHandleMovesOnlyThatNode`).
- Delete key via `IKeyBindingHandler<PlatformAction>`, defer to whole-slider when no node picked, empty-path→remove-slider, undo → Task 3.
- Shift+RightClick quick-delete of hovered handle, line falls through → Task 4.
- Out-of-scope items (cross-slider node mixing, node context menu, drag-box node select, first-class global node selection) — not implemented, by design.

**Type consistency:** `selectedNodes` (`HashSet<GarbusPathControlPoint>`), `SelectedNodes` (`IReadOnlyCollection<...>`), `removeNodes(IReadOnlyList<...>)`, `timeShiftValid(IReadOnlyList<...>, ICollection<...>, double)`, `NodeDragPiece.SelectRequested(Action<bool>)` / `NodeDragPiece.NodeSelected(bool)` — names and signatures consistent across tasks.

**Placeholder scan:** none — every code and test step contains complete content.
