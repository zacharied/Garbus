// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Drawables/SliderPolylineVisual.cs).
// BacPathControlPoint → GarbusPathControlPoint; SliderBody → Garbus.Game.Objects.SliderBody;
// Core.Constants → Garbus.Game.Constants; osu.Game.Rulesets.UI.Playfield → Garbus.Game.Gameplay.UI.Playfield.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
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

        if (vertexListEquals(newVertices) && wrapCopies.SequenceEqual(newCopies))
            return;

        vertices.Clear();
        vertices.AddRange(newVertices);
        nodePositions.Clear();
        nodePositions.AddRange(newNodes);
        wrapCopies.Clear();
        wrapCopies.AddRange(newCopies);

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

            copyPool[i].SetGeometry(vertices, nodePositions, -wrapCopies[i] * 360 * pxPerDeg);
        }

        // hide any pooled copies not needed this frame (cheaper than removing/recreating them).
        for (int i = wrapCopies.Count; i < copyPool.Count; i++)
            copyPool[i].ClearGeometry();
    }

    /// <summary>A single reusable wrap copy: a buffered path plus a dot per node. Geometry is set in place.</summary>
    private partial class PathCopy : CompositeDrawable
    {
        private readonly SmoothPath path;
        private readonly Container<Circle> markers;

        public PathCopy()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                path = new SmoothPath { PathRadius = 3 },
                markers = new Container<Circle> { RelativeSizeAxes = Axes.Both },
            };
        }

        public void SetGeometry(IReadOnlyList<Vector2> pathVertices, IReadOnlyList<Vector2> nodePositions, float offsetX)
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
                markers.Add(new Circle { Size = new Vector2(10), Origin = Anchor.Centre });

            for (int i = 0; i < nodePositions.Count; i++)
                markers[i].Position = nodePositions[i];
        }

        public void ClearGeometry()
        {
            Alpha = 0;
            path.ClearVertices();
        }
    }

    private void buildGeometry(float pxPerDeg, List<Vector2> polyline, List<Vector2> nodes)
    {
        double duration = slider.Duration;
        if (duration <= 0)
            return;

        float centreX = DrawWidth / 2;

        var controlPoints = slider.Path.ControlPoints;
        int count = 1 + controlPoints.Count;

        // Node value = angle offset in degrees (head = 0); node time = TimeOffset (head = 0).
        var values = new float[count];
        var times = new double[count];
        var linkEasing = new Easing[count - 1];
        var linkSmooth = new bool[count - 1];

        values[0] = 0f;
        times[0] = 0.0;

        for (int i = 0; i < controlPoints.Count; i++)
        {
            var cp = controlPoints[i];

            values[i + 1] = cp.RotationOffset;
            times[i + 1] = cp.TimeOffset;

            // A control point governs the segment leading into it: link[i] ends at node[i+1] = CP[i].
            linkEasing[i] = cp.SweepEasing;
            linkSmooth[i] = cp.Smooth;
        }

        var slopes = SliderSweep.ComputeSlopes(values, times);

        // Map an (angle-offset, time-offset) node/sub-point into editor space: x from angle, y from time
        // (head at the bottom = DrawHeight, later times rising). Time stays linear (matches gameplay).
        Vector2 toPoint(float angleOffset, double timeOffset)
            => new Vector2(centreX + angleOffset * pxPerDeg, DrawHeight * (float)(1 - timeOffset / duration));

        for (int n = 0; n < count; n++)
            nodes.Add(toPoint(values[n], times[n]));

        polyline.Add(toPoint(values[0], times[0]));

        for (int link = 0; link < count - 1; link++)
        {
            for (int k = 1; k <= SliderSweep.SegmentsPerLink; k++)
            {
                float t = (float)k / SliderSweep.SegmentsPerLink;
                float angle = SliderSweep.ValueAt(values, slopes, times, linkEasing[link], linkSmooth[link], link, t);
                double time = times[link] + (times[link + 1] - times[link]) * t;
                polyline.Add(toPoint(angle, time));
            }
        }
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

        return EditorAngleMapping.VisibleWrapCopies(bodyGridDeg + minOffset, bodyGridDeg + maxOffset).ToList();
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
