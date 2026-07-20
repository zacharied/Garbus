// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/TimingSectionAdjustments.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: operates on EditorChart (mutations routed through PerformOnRange so they land
// in one undo transaction with per-object Update); SliderBody duration is derived from its path, so
// BPM stretching scales the path control points' TimeOffsets instead of writing Duration; no
// IHasRepeats (Garbus has none).

using System.Linq;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Objects;
using Garbus.Game.Charts;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Bulk adjustments of the hit objects in a timing section (the span from a timing point to the
    /// next one). Used to keep objects musically in place when a section's offset or BPM changes.
    /// </summary>
    public static class TimingSectionAdjustments
    {
        /// <summary>
        /// The time range governed by <paramref name="timingControlPoint"/>: from its time (or the
        /// start of time, if it is the first timing point) up to the next timing point (or the end
        /// of time). Call while the point is still registered at the time being asked about.
        /// </summary>
        public static (double start, double end) TimingRange(EditorChart chart, TimingControlPoint timingControlPoint)
        {
            double start = chart.ControlPointInfo.TimingPoints.Any(x => x.Time < timingControlPoint.Time)
                ? timingControlPoint.Time
                : double.MinValue;

            double end = chart.ControlPointInfo.TimingPoints.FirstOrDefault(x => x.Time > timingControlPoint.Time)?.Time
                         ?? double.MaxValue;

            return (start, end);
        }

        /// <summary>
        /// Shifts all objects in the timing section by <paramref name="adjustment"/> milliseconds.
        /// Must be called BEFORE the group is moved (the range is computed from the point's old time).
        /// </summary>
        public static void AdjustHitObjectOffset(EditorChart chart, TimingControlPoint timingControlPoint, double adjustment)
        {
            var (start, end) = TimingRange(chart, timingControlPoint);
            chart.PerformOnRange(start, end, hitObject => hitObject.StartTime += adjustment);
        }

        public static void AdjustHitObjectOffset(GarbusChart chart, double start, double end, double adjustment)
        {
            foreach (var hitObject in chart.HitObjects.Where(h => h.StartTime >= start && h.StartTime < end))
            {
                hitObject.StartTime += adjustment;
                hitObject.ApplyDefaults();
            }
            chart.HitObjects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        }

        /// <summary>
        /// Keeps all objects in the timing section on the same beat after a BPM change.
        /// Must be called AFTER the new <see cref="TimingControlPoint.BeatLength"/> has been set,
        /// passing the previous value as <paramref name="oldBeatLength"/>.
        /// </summary>
        public static void SetHitObjectBPM(EditorChart chart, TimingControlPoint timingControlPoint, double oldBeatLength)
        {
            var (start, end) = TimingRange(chart, timingControlPoint);

            chart.PerformOnRange(start, end, hitObject =>
            {
                double beat = (hitObject.StartTime - timingControlPoint.Time) / oldBeatLength;
                hitObject.StartTime = beat * timingControlPoint.BeatLength + timingControlPoint.Time;

                double durationScale = timingControlPoint.BeatLength / oldBeatLength;

                switch (hitObject)
                {
                    case SliderBody slider:
                        // Duration is derived from the furthest path node; stretch the path itself.
                        foreach (var node in slider.Path.ControlPoints)
                            node.TimeOffset *= durationScale;
                        break;

                    case IHasDuration withDuration:
                        withDuration.Duration *= durationScale;
                        break;
                }
            });
        }

        public static void SetHitObjectBPM(GarbusChart chart, TimingControlPoint timingControlPoint,
                                           double oldBeatLength, double start, double end)
        {
            foreach (var hitObject in chart.HitObjects.Where(h => h.StartTime >= start && h.StartTime < end))
            {
                double beat = (hitObject.StartTime - timingControlPoint.Time) / oldBeatLength;
                hitObject.StartTime = beat * timingControlPoint.BeatLength + timingControlPoint.Time;
                double durationScale = timingControlPoint.BeatLength / oldBeatLength;

                switch (hitObject)
                {
                    case SliderBody slider:
                        foreach (var node in slider.Path.ControlPoints) node.TimeOffset *= durationScale;
                        break;
                    case IHasDuration withDuration:
                        withDuration.Duration *= durationScale;
                        break;
                }

                hitObject.ApplyDefaults();
            }
            chart.HitObjects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        }
    }
}
