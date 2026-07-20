// The slam gesture machine: a per-side rolling buffer of recent analog-stick samples that answers
// motion queries for slam judgement. Scanning a recency buffer (rather than an edge-triggered
// "this frame" flag) lets a gesture that completed slightly before the poll — including before the
// object's StartTime, as early-permissive slams require — still register.

using System;
using System.Collections.Generic;
using System.Numerics;
using Garbus.Game.Core;

namespace Garbus.Game.Input;

public class StickGestureTracker
{
    // Tunable physical-input thresholds. Timing grades live in SlamHitWindows.
    public const float FLICK_THRESHOLD = 0.7f;      // outward radius crossing that counts as a flick
    public const float EDGE_THRESHOLD = 0.7f;       // radius at/beyond which the stick is "at the edge"
    public const float ANGLE_TOLERANCE_DEG = 30f;   // flick angle must be within this of the slam angle
    public const double SAMPLE_RETENTION_MS = 550;  // exceeds the full -200..+300 early-permissive watch span

    private readonly struct Sample
    {
        public readonly double Time;
        public readonly Vector2 Position;
        public Sample(double time, Vector2 position) { Time = time; Position = position; }

        public float Radius => Position.Length();
        public float Angle => MathF.Atan2(-Position.Y, Position.X);
    }

    private readonly List<Sample> samples = new();

    public void AddSample(double time, Vector2 position)
    {
        // Clock ran backward (a restart or seek): the buffered samples are from a stale timeline and
        // would otherwise satisfy queries for the new pass, so drop them. Also keeps the list
        // monotonic, which the front-truncation pruning below relies on.
        if (samples.Count > 0 && time < samples[^1].Time)
            samples.Clear();

        samples.Add(new Sample(time, position));

        double cutoff = time - SAMPLE_RETENTION_MS;
        int drop = 0;
        while (drop < samples.Count && samples[drop].Time < cutoff)
            drop++;
        if (drop > 0)
            samples.RemoveRange(0, drop);
    }

    /// <summary>
    /// True if, at or after <paramref name="sinceTime"/>, the stick radius crossed the flick threshold
    /// outward with its angle within tolerance of <paramref name="angleDeg"/>.
    /// </summary>
    public bool FlickedTowards(int angleDeg, double sinceTime)
        => TryGetFlickTime(angleDeg, sinceTime, out _);

    /// <summary>Finds the timestamp of the first qualifying outward flick at or after <paramref name="sinceTime"/>.</summary>
    public bool TryGetFlickTime(int angleDeg, double sinceTime, out double gestureTime)
    {
        float target = angleDeg * MathF.PI / 180f;
        float tol = ANGLE_TOLERANCE_DEG * MathF.PI / 180f;

        for (int i = 1; i < samples.Count; i++)
        {
            Sample prev = samples[i - 1], cur = samples[i];
            if (cur.Time < sinceTime)
                continue;

            bool crossedOutward = prev.Radius < FLICK_THRESHOLD && cur.Radius >= FLICK_THRESHOLD;
            if (!crossedOutward)
                continue;

            if (MathF.Abs(WrapPi(target - cur.Angle)) <= tol)
            {
                gestureTime = cur.Time;
                return true;
            }
        }

        gestureTime = 0;
        return false;
    }

    /// <summary>
    /// True if, at or after <paramref name="sinceTime"/>, the stick — with both endpoints of a sample
    /// step at or beyond the edge threshold — swept through <paramref name="angleDeg"/> travelling in
    /// <paramref name="dir"/>.
    /// </summary>
    public bool SweptThrough(int angleDeg, RotationalDirection dir, double sinceTime)
        => TryGetSweepTime(angleDeg, dir, sinceTime, out _);

    /// <summary>Finds the timestamp of the first qualifying directed sweep at or after <paramref name="sinceTime"/>.</summary>
    public bool TryGetSweepTime(int angleDeg, RotationalDirection dir, double sinceTime, out double gestureTime)
    {
        float target = angleDeg * MathF.PI / 180f;
        // Increasing angle (atan2(-y, x)) is anticlockwise; Clockwise = 1, Anticlockwise = -1.
        int expectedSign = -(int)dir;

        for (int i = 1; i < samples.Count; i++)
        {
            Sample prev = samples[i - 1], cur = samples[i];
            if (cur.Time < sinceTime)
                continue;

            if (prev.Radius < EDGE_THRESHOLD || cur.Radius < EDGE_THRESHOLD)
                continue;

            float d = WrapPi(cur.Angle - prev.Angle);   // signed travel this step
            float t = WrapPi(target - prev.Angle);       // target offset from step start

            bool crossed = expectedSign > 0
                ? d > 0 && t >= 0 && t <= d
                : d < 0 && t <= 0 && t >= d;

            if (crossed)
            {
                gestureTime = cur.Time;
                return true;
            }
        }

        gestureTime = 0;
        return false;
    }

    /// <summary>Shortest signed angular distance, wrapped to (-pi, pi].</summary>
    protected static float WrapPi(float x) => x - MathF.Tau * MathF.Floor((x + MathF.PI) / MathF.Tau);
}
