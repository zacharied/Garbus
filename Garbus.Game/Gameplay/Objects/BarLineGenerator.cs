// Adapted from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Objects/BarLineGenerator.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: generic BarLineGenerator<TBarLine>/IBarLine and beatmap coupling removed — this
// takes ControlPointInfo + an explicit endTime and returns concrete BarLine objects. The Major flag is
// dropped (all bar lines uniform); a running 1-based MeasureIndex replaces osu's per-section beat/major
// bookkeeping. Section ranges are half-open (strictly less than the next timing point / endTime) via
// Precision.DefinitelyBigger so a boundary bar line is emitted once, by the incoming section.

using System;
using System.Collections.Generic;
using Garbus.Game.Charts.Timing;
using osu.Framework.Utils;

namespace Garbus.Game.Gameplay.Objects
{
    /// <summary>
    /// Generates <see cref="BarLine"/>s at every measure boundary (one per
    /// <see cref="TimeSignature.Numerator"/> beats) from timing information.
    /// </summary>
    public static class BarLineGenerator
    {
        public static List<BarLine> Generate(ControlPointInfo controlPointInfo, double endTime)
        {
            var barLines = new List<BarLine>();
            var timingPoints = controlPointInfo.TimingPoints;

            if (timingPoints.Count == 0)
                return barLines;

            int measureIndex = 1;

            for (int i = 0; i < timingPoints.Count; i++)
            {
                var point = timingPoints[i];
                double barLength = point.BeatLength * point.TimeSignature.Numerator;
                double sectionEnd = i < timingPoints.Count - 1 ? timingPoints[i + 1].Time : endTime;

                double startTime = point.Time;
                if (point.OmitFirstBarLine)
                    startTime += barLength;

                for (double t = startTime; Precision.DefinitelyBigger(sectionEnd, t); t += barLength)
                {
                    double rounded = Math.Round(t, MidpointRounding.AwayFromZero);
                    if (Precision.AlmostEquals(t, rounded))
                        t = rounded;

                    barLines.Add(new BarLine { StartTime = t, MeasureIndex = measureIndex++ });
                }
            }

            return barLines;
        }
    }
}
