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

        Colour = slider.Side == Core.HorizontalDirection.Left ? Constants.LeftColour : Constants.RightColour;
    }

    protected override void Update()
    {
        base.Update();

        float pxPerDeg = playfield.DrawWidth / EditorAngleMapping.TOTAL_DEGREES;

        var newVertices = computeVertices(pxPerDeg);
        var newCopies = computeWrapCopies();

        if (vertexListEquals(newVertices) && wrapCopies.SequenceEqual(newCopies))
            return;

        vertices.Clear();
        vertices.AddRange(newVertices);
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

            copyPool[i].SetGeometry(vertices, -wrapCopies[i] * 360 * pxPerDeg);
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

        public void SetGeometry(IReadOnlyList<Vector2> vertices, float offsetX)
        {
            Alpha = 1;
            X = offsetX;

            path.Vertices = vertices;
            // Path auto-sizes to its vertex bounds; undo the bounding-box offset so vertex coordinates
            // land in our local space (same idiom as the gameplay DrawableSliderBody).
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);

            while (markers.Count > vertices.Count)
                markers.Remove(markers[^1], true);
            while (markers.Count < vertices.Count)
                markers.Add(new Circle { Size = new Vector2(10), Origin = Anchor.Centre });

            for (int i = 0; i < vertices.Count; i++)
                markers[i].Position = vertices[i];
        }

        public void ClearGeometry()
        {
            Alpha = 0;
            path.ClearVertices();
        }
    }

    private List<Vector2> computeVertices(float pxPerDeg)
    {
        var result = new List<Vector2>();

        double duration = slider.Duration;
        if (duration <= 0)
            return result;

        float centreX = DrawWidth / 2;

        // head at the bottom (start time), nodes rising toward the end time.
        result.Add(new Vector2(centreX, DrawHeight));

        foreach (var cp in slider.Path.ControlPoints)
        {
            result.Add(new Vector2(
                centreX + cp.RotationOffset * pxPerDeg,
                DrawHeight * (float)(1 - cp.TimeOffset / duration)));
        }

        return result;
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
