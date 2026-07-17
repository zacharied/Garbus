// Builds the editor-space slider polyline shared by the compose-view drawable (SliderPolylineVisual)
// and the selection outline (SliderSelectionBlueprint), so both trace the exact same swept geometry.
// Each link is subdivided and its angle run through SliderSweep (matching gameplay's DrawableSliderBody);
// time stays linear. Space: x = centreX + Direction * angleOffsetDeg * pxPerDeg, y = drawHeight * (1 - timeOffset/duration)
// (EditorAngleMapping.Direction reflects the angle axis in the reversed view).

using System.Collections.Generic;
using Garbus.Game.Objects;
using osu.Framework.Graphics;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

public static class EditorSliderPolyline
{
    /// <summary>
    /// Fills <paramref name="polyline"/> with the subdivided, eased/smoothed line vertices and
    /// <paramref name="nodes"/> with one point per real node (head + each control point). Node 0 is the
    /// head at <c>(centreX, drawHeight)</c>; angle offsets are the raw (unwrapped)
    /// <see cref="GarbusPathControlPoint.RotationOffset"/>. The caller supplies fresh (or cleared) lists.
    /// <paramref name="duration"/> may be 0 — a path collapsed entirely to the head's time; every node
    /// then pins to the bottom line (y = drawHeight).
    /// </summary>
    public static void Build(IReadOnlyList<GarbusPathControlPoint> controlPoints, float pxPerDeg, float centreX, float drawHeight, double duration, List<Vector2> polyline, List<Vector2> nodes)
    {
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

        // EditorAngleMapping.Direction reflects the angle axis (CCW↔CW) in the reversed view, so a node's
        // signed offset from the head flips sign — the same reflection the node handles and body apply.
        // A zero-duration path (every node at time 0) has no vertical extent — pin every node to the
        // bottom line (the StartTime / judgement line) instead of dividing by zero.
        Vector2 toPoint(float angleOffset, double timeOffset)
        {
            float yFrac = duration > 0 ? (float)(timeOffset / duration) : 0f;
            return new Vector2(centreX + EditorAngleMapping.Direction * angleOffset * pxPerDeg, drawHeight * (1 - yFrac));
        }

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
}
