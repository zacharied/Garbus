using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Objects;

namespace Garbus.Game.UI;

/// <summary>
/// Pure reveal logic for the slider/slam warning indicators (GAR-3). Precomputes, per <see cref="HorizontalDirection"/>,
/// which indicated objects (slider heads and <see cref="GarbusSlamCentered"/>) are eligible for a warning, and answers
/// which one — if any — should be revealed at a given time. Only slider heads are indicated (slams occupy
/// the stick but are not telegraphed). See docs/rules-specs/Inputs.md "Warning indicator".
/// </summary>
public sealed class WarningIndicatorSchedule
{
    public readonly record struct IndicatedObject(HorizontalDirection Side, int AngleDeg, double StartTime);

    private readonly double warningTime;

    private readonly Dictionary<HorizontalDirection, List<IndicatedObject>> eligibleBySide = new()
    {
        [HorizontalDirection.Left] = new List<IndicatedObject>(),
        [HorizontalDirection.Right] = new List<IndicatedObject>(),
    };

    public WarningIndicatorSchedule(IEnumerable<GarbusHitObject> objects, double warningTime)
    {
        this.warningTime = warningTime;

        var all = objects.ToList();

        // Stick objects: anything occupying a side's analog stick (both slam types + sliders), as (start, end).
        var stickBySide = new Dictionary<HorizontalDirection, List<(double Start, double End)>>
        {
            [HorizontalDirection.Left] = new(),
            [HorizontalDirection.Right] = new(),
        };

        foreach (var o in all)
        {
            switch (o)
            {
                case SliderBody s:
                    stickBySide[s.Side].Add((s.StartTime, s.EndTime));
                    break;
                case GarbusSlamCentered sc:
                    stickBySide[sc.Side].Add((sc.StartTime, sc.StartTime));
                    break;
                case GarbusSlamEdge se:
                    stickBySide[se.Side].Add((se.StartTime, se.StartTime));
                    break;
            }
        }

        // Indicated objects: slider heads only. Slams occupy the stick (counted above) but are not
        // telegraphed. Eligible when the same-side stick has been idle longer than warningTime before the
        // object (gap measured from the previous object's end time), or when there is no earlier same-side
        // stick object at all.
        foreach (var o in all)
        {
            IndicatedObject? indicated = o switch
            {
                SliderBody s => new IndicatedObject(s.Side, s.AngleDeg, s.StartTime),
                _ => null,
            };

            if (indicated is not { } x)
                continue;

            if (gapBefore(stickBySide[x.Side], x.StartTime) > warningTime)
                eligibleBySide[x.Side].Add(x);
        }

        foreach (var list in eligibleBySide.Values)
            list.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
    }

    // Idle time on the stick immediately before startTime: startTime minus the greatest end time of any
    // same-side stick object that starts strictly earlier. +Infinity when there is no earlier object.
    private static double gapBefore(List<(double Start, double End)> sameSide, double startTime)
    {
        double previousEnd = double.NegativeInfinity;
        bool found = false;

        foreach (var (start, end) in sameSide)
        {
            if (start < startTime)
            {
                found = true;
                if (end > previousEnd)
                    previousEnd = end;
            }
        }

        return found ? startTime - previousEnd : double.PositiveInfinity;
    }

    /// <summary>
    /// The indicated object whose warning should be showing for <paramref name="side"/> at
    /// <paramref name="time"/>, or null if none. At most one object per side is ever revealed at once.
    /// </summary>
    public IndicatedObject? Revealed(HorizontalDirection side, double time)
    {
        foreach (var x in eligibleBySide[side])
        {
            if (time >= x.StartTime - warningTime && time < x.StartTime)
                return x;
        }

        return null;
    }
}
