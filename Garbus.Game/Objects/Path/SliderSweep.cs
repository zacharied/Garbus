// Shared per-link angle evaluation for slider paths: ease the value's progress across a segment,
// optionally cubic-Hermite (Catmull-Rom) smoothing it for a continuous sweep velocity through nodes.
// Used by both the gameplay body (DrawableSliderBody, values in radians) and the editor polyline
// (SliderPolylineVisual, values in degree-offsets) so the two representations cannot drift.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Utils;

namespace Garbus.Game.Objects;

public static class SliderSweep
{
    /// <summary>Straight sub-segments used to approximate each link (arc/curve in value space).</summary>
    public const int SegmentsPerLink = 12;

    /// <summary>
    /// Catmull-Rom tangents (d value / d time) at each node: centred difference for interior nodes,
    /// one-sided at the ends (the Min/Max clamps collapse the difference to the single neighbour).
    /// </summary>
    public static float[] ComputeSlopes(IReadOnlyList<float> values, IReadOnlyList<double> times)
    {
        int count = values.Count;
        var slopes = new float[count];

        for (int n = 0; n < count; n++)
        {
            int lo = Math.Max(0, n - 1);
            int hi = Math.Min(count - 1, n + 1);
            double dt = times[hi] - times[lo];

            slopes[n] = dt > 0 ? (float)((values[hi] - values[lo]) / dt) : 0f;
        }

        return slopes;
    }

    /// <summary>
    /// The value at parameter <paramref name="t"/> (0..1) along <paramref name="link"/>
    /// (node[link] → node[link+1]). Easing reshapes the progress only; endpoints are preserved
    /// (ease(0)=0, ease(1)=1). Linear by default; cubic Hermite when <paramref name="linkSmooth"/>.
    /// </summary>
    public static float ValueAt(IReadOnlyList<float> values, IReadOnlyList<float> slopes, IReadOnlyList<double> times, Easing linkEasing, bool linkSmooth, int link, float t)
    {
        if (linkEasing != Easing.None)
            t = (float)Interpolation.ApplyEasing(linkEasing, t);

        float v0 = values[link];
        float v1 = values[link + 1];

        if (!linkSmooth)
            return v0 + (v1 - v0) * t;

        // Tangents are d value / d time; scale by the link duration to express them per unit of t.
        float h = (float)(times[link + 1] - times[link]);
        float m0 = slopes[link] * h;
        float m1 = slopes[link + 1] * h;

        float t2 = t * t;
        float t3 = t2 * t;

        // Cubic Hermite basis functions.
        float h00 = 2f * t3 - 3f * t2 + 1f;
        float h10 = t3 - 2f * t2 + t;
        float h01 = -2f * t3 + 3f * t2;
        float h11 = t3 - t2;

        return h00 * v0 + h10 * m0 + h01 * v1 + h11 * m1;
    }
}
