// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/SliderSelectionBlueprint.cs).
// EditorBeatmap → EditorChart; BigAssCircleHitObjectComposer → GarbusHitObjectComposer;
// BacSnapResult → GarbusSnapResult; BacPathControlPoint → GarbusPathControlPoint;
// osu.Game.Screens.Edit IEditorChangeHandler → Garbus.Game.Edit.IEditorChangeHandler;
// OsuColour resolve removed — the outline/handle yellow (osu's OsuColour.Yellow #FFCC22) is inlined.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input;
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
internal partial class SliderSelectionBlueprint : GarbusSelectionBlueprint<SliderBody>
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
    private EditSquarePiece head = null!;

    // Outline paths are buffered drawables — pooled/reused (never new'd per frame), one per visible wrap copy.
    private readonly List<SmoothPath> outlinePool = new List<SmoothPath>();
    private readonly List<Vector2> outlineVertices = new List<Vector2>();
    private SmoothPath? primaryOutline;

    // Node selection is local to this blueprint (osu's PathControlPointVisualiser pattern): a set of the
    // stable control-point references. Not part of EditorChart.SelectedHitObjects / undo / clipboard.
    private readonly HashSet<GarbusPathControlPoint> selectedNodes = new HashSet<GarbusPathControlPoint>();

    internal IReadOnlyCollection<GarbusPathControlPoint> SelectedNodes => selectedNodes;

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
            head = new EditSquarePiece
            {
                RelativeSizeAxes = Axes.X,
                Height = EditorDrawableCardinalNote.NOTE_SIZE,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.Centre,
            },
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

        while (nodeHandles.Count > controlPoints.Count)
            nodeHandles.Remove(nodeHandles[^1], true);

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

        double duration = HitObject.Duration;
        if (duration <= 0)
        {
            clearOutline();
            return;
        }

        float pxPerDeg = HitObjectContainer.DrawWidth / EditorAngleMapping.TOTAL_DEGREES;
        float bodyGridDeg = EditorAngleMapping.ToGridDegrees(HitObject.AngleDeg);

        for (int i = 0; i < controlPoints.Count; i++)
        {
            var cp = controlPoints[i];

            // one handle per node, at the node's WRAPPED (on-grid) position — the polyline may draw the
            // node again in a ghost band via a wrap copy, but only this handle is interactable.
            float nodeGridDeg = EditorAngleMapping.ToGridDegrees(HitObject.AngleDeg + cp.RotationOffset);

            nodeHandles[i].Position = new Vector2(
                DrawWidth / 2 + (nodeGridDeg - bodyGridDeg) * pxPerDeg,
                DrawHeight * (float)(1 - cp.TimeOffset / duration));

            nodeHandles[i].NodeSelected = selectedNodes.Contains(cp);
        }

        // Drop references orphaned by undo/redo restoring a fresh control-point list.
        selectedNodes.RemoveWhere(n => !controlPoints.Contains(n));

        updateOutline(pxPerDeg, bodyGridDeg, duration);
    }

    /// <summary>
    /// Rebuilds the outline polyline to match the drawn slider exactly: raw (unwrapped)
    /// <see cref="GarbusPathControlPoint.RotationOffset"/> per node, drawn once per visible wrap copy. Kept
    /// current every frame (even while deselected) because the paths back path-precise hit-testing.
    /// </summary>
    private void updateOutline(float pxPerDeg, float bodyGridDeg, double duration)
    {
        outlineVertices.Clear();
        outlineVertices.Add(new Vector2(DrawWidth / 2, DrawHeight));

        int minOffset = 0, maxOffset = 0;

        foreach (var cp in HitObject.Path.ControlPoints)
        {
            outlineVertices.Add(new Vector2(
                DrawWidth / 2 + cp.RotationOffset * pxPerDeg,
                DrawHeight * (float)(1 - cp.TimeOffset / duration)));

            minOffset = Math.Min(minOffset, cp.RotationOffset);
            maxOffset = Math.Max(maxOffset, cp.RotationOffset);
        }

        primaryOutline = null;
        int used = 0;

        if (outlineVertices.Count >= 2)
        {
            foreach (int k in EditorAngleMapping.VisibleWrapCopies(bodyGridDeg + minOffset, bodyGridDeg + maxOffset))
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

    private void clearOutline()
    {
        primaryOutline = null;
        foreach (var path in outlinePool)
            path.ClearVertices();
    }

    private SmoothPath poolOutline(int index)
    {
        while (outlinePool.Count <= index)
        {
            var path = new SmoothPath { PathRadius = outline_radius };
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

        if (head.ReceivePositionalInputAt(screenSpacePos))
            return true;

        foreach (var handle in nodeHandles)
        {
            if (handle.ReceivePositionalInputAt(screenSpacePos))
                return true;
        }

        return false;
    }

    private void dragNode(int index, Vector2 screenSpacePosition)
    {
        if (composer == null || editorChart == null)
            return;

        var controlPoints = HitObject.Path.ControlPoints;
        if (index >= controlPoints.Count)
            return;

        var cp = controlPoints[index];
        var result = composer.FindSnappedAngleTimeAndPosition(screenSpacePosition);

        bool changed = false;

        if (result.Time is double proposedTime)
        {
            double proposedOffset = proposedTime - HitObject.StartTime;
            double minOffset = index > 0 ? controlPoints[index - 1].TimeOffset : 0;
            double? maxOffset = index < controlPoints.Count - 1 ? controlPoints[index + 1].TimeOffset : null;

            if (proposedOffset != cp.TimeOffset && proposedOffset > minOffset && (maxOffset == null || proposedOffset < maxOffset))
            {
                cp.TimeOffset = proposedOffset;
                changed = true;
            }
        }

        if (result is GarbusSnapResult snap)
        {
            int currentAbsolute = EditorAngleMapping.NormalizeDeg(HitObject.AngleDeg + cp.RotationOffset);
            int diff = EditorAngleMapping.MinimalDiff(currentAbsolute, snap.AngleDeg);

            if (diff != 0)
            {
                cp.RotationOffset += diff;
                changed = true;
            }
        }

        // Only run the (ApplyDefaults + state-save) update when the node actually moved — mouse-move
        // events inside the same snap cell would otherwise re-apply the whole slider per event.
        if (changed)
            editorChart.Update(HitObject);
    }

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

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key != Key.T || e.Repeat || !IsSelected)
            return base.OnKeyDown(e);

        insertNodeAtCursor();
        return true;
    }

    private void insertNodeAtCursor()
    {
        if (composer == null || editorChart == null)
            return;

        var result = composer.FindSnappedAngleTimeAndPosition(inputManager.CurrentState.Mouse.Position);

        if (result.Time is not double time || result is not GarbusSnapResult snap)
            return;

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
    }

    protected override bool OnClick(ClickEvent e)
    {
        // A click that reached the blueprint body (not consumed by a node handle) clears node selection but
        // leaves the whole-slider selection to BlueprintContainer's own click handling.
        if (e.Button == MouseButton.Left)
            selectedNodes.Clear();

        return base.OnClick(e);
    }

    // sizes only the framework's rectangular handle box — bound the whole (primary, unwrapped) polyline so
    // the handles enclose the slider; selection itself is driven by ReceivePositionalInputAt, not this.
    public override Quad SelectionQuad =>
        primaryOutline != null && primaryOutline.Vertices.Count >= 2 ? primaryOutline.ScreenSpaceDrawQuad : ScreenSpaceDrawQuad;

    public override Vector2 ScreenSpaceSelectionPoint => head.ScreenSpaceDrawQuad.Centre;

    /// <summary>Screen-space centre of the final (latest-time) node handle; the head when there are none.</summary>
    public Vector2 FinalNodeScreenPosition => nodeHandles.Count > 0
        ? nodeHandles[^1].ScreenSpaceDrawQuad.Centre
        : head.ScreenSpaceDrawQuad.Centre;
}
