using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Statistics;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// The editor's slider representation: a polyline joining the head to each control-point node in
/// (angle → x, time → y) space, with a dot at every node. Node x offsets use the raw (unwrapped)
/// <see cref="GarbusPathControlPoint.RotationOffset"/>, and the whole polyline is drawn once per visible
/// wrap copy (<see cref="EditorAngleMapping.VisibleWrapCopies"/>) so a path crossing the wrap seam
/// re-enters from the opposite edge — including arbitrarily many full turns.
///
/// Vertices are recomputed each frame (the scroll scale can change with timeline zoom) but copies are
/// only rebuilt when the vertices or the copy set actually changed. Note the copy set depends on the
/// BODY angle too: dragging the body toward the seam changes which copies are visible while leaving the
/// body-relative vertices identical.
/// </summary>
public partial class SliderPolylineVisual : CompositeDrawable
{
    private readonly SliderBody slider;

    private readonly List<Vector2> vertices = new List<Vector2>();

    // One entry per real node (head + each control point) — where the dot markers go. Distinct from
    // `vertices`, which is the subdivided polyline fed to the SmoothPath.
    private readonly List<Vector2> nodePositions = new List<Vector2>();

    // Parallel to nodePositions: whether each node is a shape-only control point (never the head).
    private readonly List<bool> nodeShapeFlags = new List<bool>();
    private readonly List<int> wrapCopies = new List<int>();

    // Wrap copies are pooled and reused: each copy owns a buffered SmoothPath (its own framebuffer), so
    // recreating them per rebuild allocated a fresh framebuffer every frame during a node drag.
    private readonly List<PathCopy> copyPool = new List<PathCopy>();

    // Temporary diagnostic: watch this climb in the Ctrl+F2 global-statistics overlay. If it advances at
    // frame rate while a slider merely sits selected, the vertex/copy early-out is thrashing.
    private static readonly GlobalStatistic<int> rebuild_count = GlobalStatistics.Get<int>("Garbus", "Slider polyline rebuilds");

    [Resolved]
    private Playfield playfield { get; set; } = null!;

    public SliderPolylineVisual(SliderBody slider)
    {
        this.slider = slider;
        RelativeSizeAxes = Axes.Both;
    }

    protected override void Update()
    {
        base.Update();

        // Read Side live (not once in the ctor): the editor mutates it in place via EditorChart.Update,
        // which re-Apply()s the drawable rather than recreating it, so a Side change must recolour here.
        var sideColour = slider.Side == Core.HorizontalDirection.Left ? Constants.LeftColour : Constants.RightColour;
        if (!Colour.Equals(sideColour))
            Colour = sideColour;

        float pxPerDeg = playfield.DrawWidth / EditorAngleMapping.TOTAL_DEGREES;

        var newVertices = new List<Vector2>();
        var newNodes = new List<Vector2>();
        buildGeometry(pxPerDeg, newVertices, newNodes);
        var newCopies = computeWrapCopies();

        var newFlags = new List<bool>(1 + slider.Path.ControlPoints.Count) { false }; // head is always judged
        foreach (var cp in slider.Path.ControlPoints)
            newFlags.Add(cp.ShapeOnly);

        if (vertexListEquals(newVertices) && wrapCopies.SequenceEqual(newCopies) && nodeShapeFlags.SequenceEqual(newFlags))
            return;

        vertices.Clear();
        vertices.AddRange(newVertices);
        nodePositions.Clear();
        nodePositions.AddRange(newNodes);
        wrapCopies.Clear();
        wrapCopies.AddRange(newCopies);
        nodeShapeFlags.Clear();
        nodeShapeFlags.AddRange(newFlags);

        rebuildCopies(pxPerDeg);
    }

    private void rebuildCopies(float pxPerDeg)
    {
        rebuild_count.Value++;

        for (int i = 0; i < wrapCopies.Count; i++)
        {
            while (copyPool.Count <= i)
            {
                var created = new PathCopy();
                copyPool.Add(created);
                AddInternal(created);
            }

            copyPool[i].SetGeometry(vertices, nodePositions, nodeShapeFlags, -wrapCopies[i] * 360 * pxPerDeg);
        }

        // hide any pooled copies not needed this frame (cheaper than removing/recreating them).
        for (int i = wrapCopies.Count; i < copyPool.Count; i++)
            copyPool[i].ClearGeometry();
    }

    /// <summary>A single reusable wrap copy: a buffered path plus a dot per node. Geometry is set in place.</summary>
    private partial class PathCopy : CompositeDrawable
    {
        private readonly SmoothPath path;
        private readonly Container<SliderNodeMarker> markers;

        public PathCopy()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                path = new SmoothPath { PathRadius = 3 },
                markers = new Container<SliderNodeMarker> { RelativeSizeAxes = Axes.Both },
            };
        }

        public void SetGeometry(IReadOnlyList<Vector2> pathVertices, IReadOnlyList<Vector2> nodePositions, IReadOnlyList<bool> nodeShapeFlags, float offsetX)
        {
            Alpha = 1;
            X = offsetX;

            path.Vertices = pathVertices;
            // Path auto-sizes to its vertex bounds; undo the bounding-box offset so vertex coordinates
            // land in our local space (same idiom as the gameplay DrawableSliderBody).
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);

            while (markers.Count > nodePositions.Count)
                markers.Remove(markers[^1], true);
            while (markers.Count < nodePositions.Count)
                markers.Add(new SliderNodeMarker());

            for (int i = 0; i < nodePositions.Count; i++)
            {
                markers[i].Position = nodePositions[i];
                markers[i].ShapeOnly = nodeShapeFlags[i];
            }
        }

        public void ClearGeometry()
        {
            Alpha = 0;
            path.ClearVertices();
        }
    }

    private void buildGeometry(float pxPerDeg, List<Vector2> polyline, List<Vector2> nodes)
    {
        // Duration may be 0 (a constant-radius arc at a single instant); EditorSliderPolyline.Build pins
        // every node to the bottom line in that case rather than dividing by zero.
        EditorSliderPolyline.Build(slider.Path.ControlPoints, pxPerDeg, DrawWidth / 2, DrawHeight, slider.Duration, polyline, nodes);
    }

    private List<int> computeWrapCopies()
    {
        float bodyGridDeg = EditorAngleMapping.ToGridDegrees(slider.AngleDeg);

        int minOffset = 0, maxOffset = 0;

        foreach (var cp in slider.Path.ControlPoints)
        {
            if (cp.RotationOffset < minOffset) minOffset = cp.RotationOffset;
            if (cp.RotationOffset > maxOffset) maxOffset = cp.RotationOffset;
        }

        return EditorAngleMapping.VisibleWrapCopiesForOffsets(bodyGridDeg, minOffset, maxOffset).ToList();
    }

    private bool vertexListEquals(List<Vector2> other)
    {
        if (vertices.Count != other.Count)
            return false;

        for (int i = 0; i < vertices.Count; i++)
        {
            if (vertices[i] != other[i])
                return false;
        }

        return true;
    }
}
