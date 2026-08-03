using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints;

/// <summary>
/// Slider selection: a yellow outline tracing the actual polyline (head → each node, with wrap copies
/// across the seam) plus a draggable circle handle per control-point node (dragging retimes/re-angles
/// that node, clamped between its neighbours). With the slider selected, pressing <c>T</c> inserts a new
/// node at the cursor, kept time-ordered.
///
/// Hit-testing is **path-precise**: <see cref="ReceivePositionalInputAt"/> only reports the outline paths
/// and node handles, so clicking a line segment or node selects the slider while clicks in the empty
/// space of its bounding box fall through to whatever is underneath. <see cref="SelectionQuad"/> only
/// sizes the framework's rectangular handle box and never drives selection, so bounding the whole
/// polyline there is safe.
/// </summary>
internal partial class SliderSelectionBlueprint : GarbusSelectionBlueprint<SliderBody>, IKeyBindingHandler<PlatformAction>
{
    /// <summary>Thickness of the outline; doubles as the click tolerance for path-precise selection.</summary>
    private const float outline_radius = 8;

    // osu's OsuColour.Yellow (#FFCC22).
    private static readonly Colour4 yellow = new Colour4(255, 204, 34, 255);

    [Resolved]
    private IEditorChangeHandler? changeHandler { get; set; }

    [Resolved]
    private EditorChart? editorChart { get; set; }

    [Resolved]
    private GarbusHitObjectComposer? composer { get; set; }

    private Container outlineContainer = null!;
    private Container<NodeDragPiece> nodeHandles = null!;
    private Container<EditSquarePiece> headHandles = null!;

    // The primary (WrapK == 0) head copy, used for the selection anchor points. Reassigned each frame.
    private EditSquarePiece head = null!;

    // Outline paths are buffered drawables — pooled/reused (never new'd per frame), one per visible wrap copy.
    private readonly List<SmoothPath> outlinePool = new List<SmoothPath>();
    private readonly List<Vector2> outlineVertices = new List<Vector2>();
    // EditorSliderPolyline.Build also emits node-dot positions; the outline doesn't use them (node handles
    // are drawn separately), so it writes them into this throwaway buffer.
    private readonly List<Vector2> outlineNodesScratch = new List<Vector2>();
    private SmoothPath? primaryOutline;

    // Node selection is local to this blueprint (osu's PathControlPointVisualiser pattern): a set of the
    // stable control-point references. Not part of EditorChart.SelectedHitObjects / undo / clipboard.
    private readonly HashSet<GarbusPathControlPoint> selectedNodes = new HashSet<GarbusPathControlPoint>();

    // The control point currently being dragged, captured by stable reference at drag start. NodeDragPiece
    // identifies its node by CpIndex — a slot-order value reassigned every frame — so as the wrap-copy count
    // shifts mid-drag (e.g. the node crossing the seam), the dragged handle's CpIndex can jump to a different
    // control point. Binding the drag to a fixed reference keeps it on the grabbed node the whole time.
    private GarbusPathControlPoint? draggedNode;

    // The implicit head has no GarbusPathControlPoint to capture in draggedNode, so a head drag is flagged
    // separately. Set in beginNodeDrag(head_index), cleared in endNodeDrag.
    private bool draggingHead;

    internal IReadOnlyCollection<GarbusPathControlPoint> SelectedNodes => selectedNodes;

    // The implicit head node (offset 0 = slider StartTime/AngleDeg). Selectable alongside control points
    // but has no GarbusPathControlPoint, so it is a plain flag rather than a set member. Sentinel index -1.
    private const int head_index = -1;

    private bool headSelected;

    internal bool HeadSelected => headSelected;

    // Node drag-box (Shift+drag) selection state. The snapshot holds the node selection captured at drag
    // start when combining (Shift+Ctrl); in replace mode it stays empty so the box fully defines selection.
    private readonly HashSet<GarbusPathControlPoint> nodeDragBoxSnapshot = new HashSet<GarbusPathControlPoint>();
    private bool nodeDragBoxSnapshotHead;

    /// <summary>Whether this slider has control-point nodes the box can target (a head-only slider has none).</summary>
    internal bool HasNodeTargets => HitObject.Path.ControlPoints.Count > 0;

    /// <summary>Whether the head or any control-point node is currently selected.</summary>
    internal bool HasNodeSelection => headSelected || selectedNodes.Count > 0;

    /// <summary>Starts a node drag-box. Replace clears the current node selection so the box fully defines
    /// it; combine snapshots the current selection so boxed nodes are added on top of it.</summary>
    internal void BeginNodeDragBox(bool combine)
    {
        nodeDragBoxSnapshot.Clear();

        if (combine)
        {
            nodeDragBoxSnapshot.UnionWith(selectedNodes);
            nodeDragBoxSnapshotHead = headSelected;
        }
        else
        {
            nodeDragBoxSnapshotHead = false;
            selectedNodes.Clear();
            headSelected = false;
        }
    }

    /// <summary>Rebuilds the node selection for the current box: every node whose handle centre lies in the
    /// box, unioned with the drag-start snapshot (empty in replace mode).</summary>
    internal void UpdateNodeDragBox(Quad screenQuad)
    {
        var controlPoints = HitObject.Path.ControlPoints;

        boxedNodesBuffer.Clear();

        foreach (var handle in nodeHandles)
        {
            if (handle.CpIndex >= 0 && handle.CpIndex < controlPoints.Count
                && screenQuad.Contains(handle.ScreenSpaceDrawQuad.Centre))
                boxedNodesBuffer.Add(controlPoints[handle.CpIndex]);
        }

        selectedNodes.Clear();

        foreach (var cp in controlPoints)
        {
            if (boxedNodesBuffer.Contains(cp) || nodeDragBoxSnapshot.Contains(cp))
                selectedNodes.Add(cp);
        }

        // The head is a node target only on a slider that has control points; its handle disables interaction
        // otherwise, so it never boxes.
        bool boxedHead = HasNodeTargets && headHandles.Any(h => h.InteractionEnabled && screenQuad.Contains(h.ScreenSpaceDrawQuad.Centre));
        headSelected = boxedHead || nodeDragBoxSnapshotHead;
    }

    private readonly HashSet<GarbusPathControlPoint> boxedNodesBuffer = new HashSet<GarbusPathControlPoint>();

    /// <summary>Ends a node drag-box, dropping the snapshot; the live node selection stands.</summary>
    internal void EndNodeDragBox()
    {
        nodeDragBoxSnapshot.Clear();
        nodeDragBoxSnapshotHead = false;
    }

    private InputManager inputManager = null!;

    public SliderSelectionBlueprint(SliderBody slider)
        : base(slider)
    {
        Width = EditorDrawableCardinalNote.NOTE_SIZE;
        Origin = Anchor.BottomCentre;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        InternalChildren = new Drawable[]
        {
            // behind the head marker and node handles so the dots/handles stay clickable on top.
            outlineContainer = new Container { RelativeSizeAxes = Axes.Both, Colour = yellow },
            headHandles = new Container<EditSquarePiece> { RelativeSizeAxes = Axes.Both },
            nodeHandles = new Container<NodeDragPiece> { RelativeSizeAxes = Axes.Both },
        };
    }

    // wrap copies trace the seam themselves; suppress the base rectangular ghost twin.
    protected override float? TwinXFraction() => null;

    protected override void LoadComplete()
    {
        base.LoadComplete();
        inputManager = GetContainingInputManager()!;
    }

    protected override void Update()
    {
        base.Update();

        Height = HitObjectContainer.LengthAtTime(HitObject.StartTime, HitObject.EndTime);

        var controlPoints = HitObject.Path.ControlPoints;

        double duration = HitObject.Duration;

        float pxPerDeg = HitObjectContainer.DrawWidth / EditorAngleMapping.TOTAL_DEGREES;
        float bodyGridDeg = EditorAngleMapping.ToGridDegrees(HitObject.AngleDeg);

        int minOffset = 0, maxOffset = 0;
        foreach (var cp in controlPoints)
        {
            minOffset = Math.Min(minOffset, cp.RotationOffset);
            maxOffset = Math.Max(maxOffset, cp.RotationOffset);
        }

        // Wrap copies match the outline's — one handle per (node × visible wrap copy) so every ghost band
        // clone of a node is independently clickable, not just whichever copy lands in the main grid.
        wrapCopiesBuffer.Clear();
        foreach (int k in EditorAngleMapping.VisibleWrapCopiesForOffsets(bodyGridDeg, minOffset, maxOffset))
            wrapCopiesBuffer.Add(k);

        int handlesNeeded = controlPoints.Count * wrapCopiesBuffer.Count;

        while (nodeHandles.Count > handlesNeeded)
            nodeHandles.Remove(nodeHandles[^1], true);

        while (nodeHandles.Count < handlesNeeded)
        {
            nodeHandles.Add(new NodeDragPiece
            {
                SelectRequested = (index, ctrl) => selectNode(index, ctrl),
                DragStarted = index => beginNodeDrag(index),
                Dragging = pos => dragNode(pos),
                DragEnded = () => endNodeDrag(),
            });
        }

        int slot = 0;
        for (int i = 0; i < controlPoints.Count; i++)
        {
            var cp = controlPoints[i];
            // duration 0 pins every node to the bottom line (the StartTime / judgement line).
            float y = duration > 0 ? DrawHeight * (float)(1 - cp.TimeOffset / duration) : DrawHeight;
            bool selected = selectedNodes.Contains(cp);

            foreach (int k in wrapCopiesBuffer)
            {
                var handle = nodeHandles[slot++];
                handle.CpIndex = i;
                handle.WrapK = k;
                handle.Position = new Vector2(
                    DrawWidth / 2 + (EditorAngleMapping.GridOffset(cp.RotationOffset) - k * 360) * pxPerDeg,
                    y);
                handle.NodeSelected = selected;
                handle.ShapeOnly = cp.ShapeOnly;
            }
        }

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
                DragStarted = () => beginNodeDrag(head_index),
                Dragging = (_, pos) => dragNode(pos),
                DragEnded = () => endNodeDrag(),
            });
        }

        // A head-only slider (no control points) IS its head — the head is not an independent node target,
        // so its handle declines input and the click/drag flows through to the whole-object select + group
        // move (like dragging a body slider by its outline). It still hit-tests, keeping the slider selectable.
        bool headIsNodeTarget = controlPoints.Count > 0;
        if (!headIsNodeTarget)
            headSelected = false;

        for (int hi = 0; hi < wrapCopiesBuffer.Count; hi++)
        {
            int k = wrapCopiesBuffer[hi];
            var hh = headHandles[hi];
            hh.WrapK = k;
            hh.InteractionEnabled = headIsNodeTarget;
            hh.Position = new Vector2(
                DrawWidth / 2 + (EditorAngleMapping.GridOffset(0) - k * 360) * pxPerDeg,
                DrawHeight);
            hh.NodeSelected = headSelected;
            head = hh.WrapK == 0 ? hh : head;
        }

        // Fall back to the first head handle if no primary (WrapK 0) copy is visible.
        if (head == null || !headHandles.Contains(head))
            head = headHandles.Count > 0 ? headHandles[0] : null!;

        // Drop references orphaned by undo/redo restoring a fresh control-point list.
        selectedNodes.RemoveWhere(n => !controlPoints.Contains(n));

        updateOutline(pxPerDeg, bodyGridDeg, duration);
    }

    private readonly List<int> wrapCopiesBuffer = new List<int>();

    /// <summary>
    /// Rebuilds the outline polyline to match the drawn slider exactly: the same subdivided, eased/smoothed
    /// sweep as <see cref="SliderPolylineVisual"/> (via <see cref="EditorSliderPolyline"/>), drawn once per
    /// visible wrap copy. Kept current every frame (even while deselected) because the paths back
    /// path-precise hit-testing — so clicking the curved body selects it, not just the straight chord.
    /// </summary>
    private void updateOutline(float pxPerDeg, float bodyGridDeg, double duration)
    {
        outlineVertices.Clear();
        outlineNodesScratch.Clear();
        EditorSliderPolyline.Build(HitObject.Path.ControlPoints, pxPerDeg, DrawWidth / 2, DrawHeight, duration, outlineVertices, outlineNodesScratch);

        int minOffset = 0, maxOffset = 0;

        foreach (var cp in HitObject.Path.ControlPoints)
        {
            minOffset = Math.Min(minOffset, cp.RotationOffset);
            maxOffset = Math.Max(maxOffset, cp.RotationOffset);
        }

        primaryOutline = null;
        int used = 0;

        if (outlineVertices.Count >= 2)
        {
            foreach (int k in EditorAngleMapping.VisibleWrapCopiesForOffsets(bodyGridDeg, minOffset, maxOffset))
            {
                var path = poolOutline(used++);
                path.Vertices = outlineVertices;
                path.Position = -path.PositionInBoundingBox(Vector2.Zero) + new Vector2(-k * 360 * pxPerDeg, 0);

                if (k == 0)
                    primaryOutline = path;
            }
        }

        for (int i = used; i < outlinePool.Count; i++)
            outlinePool[i].ClearVertices();
    }

    private SmoothPath poolOutline(int index)
    {
        while (outlinePool.Count <= index)
        {
            var path = new HollowOutlinePath { PathRadius = outline_radius };
            outlinePool.Add(path);
            outlineContainer.Add(path);
        }

        return outlinePool[index];
    }

    public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
    {
        // path-precise: only the traced polyline and the node handles select the slider — never the empty
        // space of its bounding box.
        foreach (var path in outlinePool)
        {
            if (path.ReceivePositionalInputAt(screenSpacePos))
                return true;
        }

        foreach (var handle in headHandles)
        {
            if (handle.ReceivePositionalInputAt(screenSpacePos))
                return true;
        }

        foreach (var handle in nodeHandles)
        {
            if (handle.ReceivePositionalInputAt(screenSpacePos))
                return true;
        }

        return false;
    }

    /// <summary>Captures the grabbed control point by stable reference (the index is still trustworthy at
    /// drag start — no wrap-copy reshuffle has happened yet) and opens the change transaction.</summary>
    private void beginNodeDrag(int index)
    {
        var controlPoints = HitObject.Path.ControlPoints;
        draggingHead = index == head_index;
        draggedNode = !draggingHead && index < controlPoints.Count ? controlPoints[index] : null;
        changeHandler?.BeginChange();
    }

    private void endNodeDrag()
    {
        draggedNode = null;
        draggingHead = false;
        changeHandler?.EndChange();
    }

    private void dragNode(Vector2 screenSpacePosition)
    {
        if (composer == null || editorChart == null)
            return;

        var controlPoints = HitObject.Path.ControlPoints;

        // The grabbed handle is either the implicit head (draggingHead) or a control point captured by stable
        // reference at drag start (draggedNode) — the index is untrustworthy mid-drag as wrap copies reshuffle.
        // A non-head grab whose node has since left the path aborts the event.
        GarbusPathControlPoint? grabbed = null;

        if (!draggingHead)
        {
            if (draggedNode is not { } node || !controlPoints.Contains(node))
                return;

            grabbed = node;
        }

        var result = composer.FindSnappedAngleTimeAndPosition(screenSpacePosition);

        // The grabbed handle's current absolute time/angle define the snap deltas (the head sits at offset 0).
        double grabbedTimeOffset = grabbed?.TimeOffset ?? 0;
        int grabbedRotationOffset = grabbed?.RotationOffset ?? 0;

        // The moved set: the whole combined selection when the grabbed handle is part of it, else just the
        // grabbed handle. `movedHead` mirrors the head flag; `movedNodes` the control-point subset.
        bool grabbedSelected = draggingHead ? headSelected : selectedNodes.Contains(grabbed!);

        bool movedHead;
        ICollection<GarbusPathControlPoint> movedNodes;

        if (grabbedSelected)
        {
            movedHead = headSelected;
            movedNodes = selectedNodes;
        }
        else
        {
            movedHead = draggingHead;
            movedNodes = draggingHead ? System.Array.Empty<GarbusPathControlPoint>() : new[] { grabbed! };
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

    /// <summary>
    /// True if applying <paramref name="deltaTime"/> leaves the full path valid. When the head moves
    /// (<paramref name="headInSet"/>), StartTime shifts by Δ and every node NOT in <paramref name="movedNodes"/>
    /// has its offset reduced by Δ (so its absolute time is fixed) — plus StartTime + Δ must stay >= 0.
    /// When the head is fixed, only the moved nodes' offsets grow by Δ. Validity is delegated to
    /// <see cref="GarbusSliderPath.AreTimesValid"/>: non-decreasing times, at most one zero-length link in a
    /// row, and at least one control point — a zero total duration is allowed (the path may collapse onto the
    /// head's time as a constant-radius arc at a single instant).
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

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Repeat || !IsSelected)
            return base.OnKeyDown(e);

        if (e is { ControlPressed: true, ShiftPressed: false, AltPressed: false })
        {
            switch (e.Key)
            {
                case Key.Q:
                    setSelectedNodesEasing(Easing.In);
                    return true;

                case Key.W:
                    setSelectedNodesEasing(Easing.Out);
                    return true;

                case Key.E:
                    setSelectedNodesEasing(Easing.InOutQuad);
                    return true;

                case Key.R:
                    setSelectedNodesEasing(Easing.None);
                    return true;
            }

            return base.OnKeyDown(e);
        }

        if (e.Key != Key.T)
            return base.OnKeyDown(e);

        insertNodeAtCursor();
        return true;
    }

    /// <summary>Sets every selected node's <see cref="GarbusPathControlPoint.SweepEasing"/> — same transaction pattern as the Inspector's Easing dropdown.</summary>
    private void setSelectedNodesEasing(Easing easing)
    {
        if (selectedNodes.Count == 0 || editorChart == null)
            return;

        if (selectedNodes.All(n => n.SweepEasing == easing))
            return;

        changeHandler?.BeginChange();
        foreach (var n in selectedNodes)
            n.SweepEasing = easing;
        editorChart.Update(HitObject);
        changeHandler?.EndChange();
    }

    private void insertNodeAtCursor()
    {
        if (composer == null || editorChart == null)
            return;

        var result = composer.FindSnappedAngleTimeAndPosition(inputManager.CurrentState.Mouse.Position);

        if (result.Time is not double time || result is not GarbusSnapResult snap)
            return;

        double timeOffset = time - HitObject.StartTime;

        var controlPoints = HitObject.Path.ControlPoints;

        int insertIndex = 0;
        // `<=` means an equal-time insert lands just AFTER an existing node at that time — matching
        // placement's tryAddNode, which always APPENDS an equal-time node after the previous last node.
        // Both produce a valid single zero-link arc.
        while (insertIndex < controlPoints.Count && controlPoints[insertIndex].TimeOffset <= timeOffset)
            insertIndex++;

        // Validate the path the insertion would produce: non-decreasing, at most one zero-length link
        // in a row, at least one node. This permits a single horizontal arc (inserting at an existing
        // node's time, or at time 0 after the head) — including one that collapses the path to zero total
        // duration — while still rejecting a backwards time or a 3-node stack.
        var prospective = new List<double>(controlPoints.Count + 1);
        for (int i = 0; i < controlPoints.Count; i++)
            prospective.Add(controlPoints[i].TimeOffset);
        prospective.Insert(insertIndex, timeOffset);

        if (!GarbusSliderPath.AreTimesValid(prospective))
            return;

        int previousRotation = insertIndex > 0 ? controlPoints[insertIndex - 1].RotationOffset : 0;
        int previousAbsolute = EditorAngleMapping.NormalizeDeg(HitObject.AngleDeg + previousRotation);

        changeHandler?.BeginChange();

        controlPoints.Insert(insertIndex, new GarbusPathControlPoint
        {
            TimeOffset = timeOffset,
            RotationOffset = previousRotation + EditorAngleMapping.MinimalDiff(previousAbsolute, snap.AngleDeg),
        });

        editorChart.Update(HitObject);
        changeHandler?.EndChange();
    }

    protected override void OnDeselected()
    {
        base.OnDeselected();
        selectedNodes.Clear();
        headSelected = false;
    }

    protected override bool OnClick(ClickEvent e)
    {
        // A click that reached the blueprint body (not consumed by a node handle) clears node selection but
        // leaves the whole-slider selection to BlueprintContainer's own click handling.
        if (e.Button == MouseButton.Left)
        {
            selectedNodes.Clear();
            headSelected = false;
        }

        return base.OnClick(e);
    }

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

            // The last control point is never shape-only; deleting the final judged node promotes the new
            // final point so the invariant survives every delete.
            if (controlPoints.Count > 0)
                controlPoints[^1].ShapeOnly = false;

            editorChart.Update(HitObject);
        }

        selectedNodes.Clear();
        changeHandler?.EndChange();
    }

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

        // Same invariant guard as removeNodes: the dropped selection may have included the final point.
        if (controlPoints.Count > 0)
            controlPoints[^1].ShapeOnly = false;

        selectedNodes.Clear();
        headSelected = false;

        editorChart.Update(HitObject);
        changeHandler?.EndChange();
    }

    public override bool HandleQuickDeletion()
    {
        // Shift+RightClick over the head handle deletes (promotes) the head; over a node handle deletes just
        // that node; over the line, fall through (return false) so SelectionHandler removes the whole slider.
        if (headHandles.Any(h => h.IsHovered))
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

    // sizes only the framework's rectangular handle box — bound the whole (primary, unwrapped) polyline so
    // the handles enclose the slider; selection itself is driven by ReceivePositionalInputAt, not this.
    public override Quad SelectionQuad =>
        primaryOutline != null && primaryOutline.Vertices.Count >= 2 ? primaryOutline.ScreenSpaceDrawQuad : ScreenSpaceDrawQuad;

    public override Vector2 ScreenSpaceSelectionPoint =>
        head?.ScreenSpaceDrawQuad.Centre ?? ScreenSpaceDrawQuad.Centre;

    private Vector2 headScreen() => head?.ScreenSpaceDrawQuad.Centre ?? ScreenSpaceDrawQuad.Centre;

    /// <summary>Screen-space centre of the final (latest-time) node handle's primary (raw) copy;
    /// the head when there are no nodes, or the first available handle if the primary isn't visible.</summary>
    public Vector2 FinalNodeScreenPosition
    {
        get
        {
            var controlPoints = HitObject.Path.ControlPoints;
            if (controlPoints.Count == 0)
                return headScreen();

            int finalIndex = controlPoints.Count - 1;
            NodeDragPiece? chosen = null;

            foreach (var handle in nodeHandles)
            {
                if (handle.CpIndex != finalIndex)
                    continue;

                if (handle.WrapK == 0)
                    return handle.ScreenSpaceDrawQuad.Centre;

                chosen ??= handle;
            }

            return chosen?.ScreenSpaceDrawQuad.Centre ?? headScreen();
        }
    }
}
