using System;
using System.Linq;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Objects;
using osu.Framework.Bindables;

namespace Garbus.Game.Charts;

internal static class GarbusChartCloner
{
    public static GarbusHitObject CloneHitObject(GarbusHitObject source)
    {
        ArgumentNullException.ThrowIfNull(source);

        GarbusHitObject clone = source switch
        {
            CardinalNote cardinal when source.GetType() == typeof(CardinalNote) => new CardinalNote { AngleDeg = cardinal.AngleDeg },
            CardinalHoldNote hold when source.GetType() == typeof(CardinalHoldNote) => new CardinalHoldNote { AngleDeg = hold.AngleDeg, Duration = hold.Duration },
            ShoulderNote shoulder when source.GetType() == typeof(ShoulderNote) => new ShoulderNote { Side = shoulder.Side },
            ShoulderHoldNote shoulderHold when source.GetType() == typeof(ShoulderHoldNote) => new ShoulderHoldNote { Side = shoulderHold.Side, Duration = shoulderHold.Duration },
            SliderBody slider when source.GetType() == typeof(SliderBody) => new SliderBody
            {
                AngleDeg = slider.AngleDeg,
                Side = slider.Side,
                Path = clonePath(slider.Path),
            },
            GarbusSlamCentered slam when source.GetType() == typeof(GarbusSlamCentered) => new GarbusSlamCentered { AngleDeg = slam.AngleDeg, Side = slam.Side },
            GarbusSlamEdge slam when source.GetType() == typeof(GarbusSlamEdge) => new GarbusSlamEdge
            {
                AngleDeg = slam.AngleDeg,
                Side = slam.Side,
                Direction = slam.Direction,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(source), source.GetType().Name, "hit object type cannot be cloned"),
        };

        clone.StartTime = source.StartTime;
        clone.Samples = source.Samples.Select(cloneHitSample).ToList();
        return clone;
    }

    public static ChartMetadata CloneMetadata(ChartMetadata source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ensureExactType<ChartMetadata>(source, "metadata");

        return new ChartMetadata
        {
            Title = source.Title,
            Artist = source.Artist,
            Charter = source.Charter,
            ChartName = source.ChartName,
            RomanisedTitle = source.RomanisedTitle,
            RomanisedArtist = source.RomanisedArtist,
            Source = source.Source,
            Tags = source.Tags,
            AudioFile = source.AudioFile,
            BackgroundFile = source.BackgroundFile,
            Level = source.Level,
            Difficulty = source.Difficulty,
        };
    }

    public static DesignPointInfo CloneDesignPointInfo(DesignPointInfo source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ensureExactType<DesignPointInfo>(source, "design point info");

        var clone = new DesignPointInfo();

        foreach (DesignPoint point in source.DesignPoints)
        {
            clone.Add(point switch
            {
                TutorialMessage message when point.GetType() == typeof(TutorialMessage) => new TutorialMessage
                {
                    StartTime = message.StartTime,
                    EndTime = message.EndTime,
                    Text = message.Text,
                },
                _ => throw new ArgumentOutOfRangeException(nameof(source), point.GetType().Name, "design point type cannot be cloned"),
            });
        }

        return clone;
    }

    public static GarbusChart CloneChart(GarbusChart source, ControlPointInfo effectiveControlPointInfo)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(effectiveControlPointInfo);
        ensureExactType<GarbusChart>(source, "chart");

        return new GarbusChart
        {
            ChartId = source.ChartId,
            Metadata = CloneMetadata(source.Metadata),
            PreviewTime = source.PreviewTime,
            ControlPointInfo = CloneControlPointInfo(effectiveControlPointInfo),
            DesignPointInfo = CloneDesignPointInfo(source.DesignPointInfo),
            HitObjects = source.HitObjects.Select(CloneHitObject).ToList(),
        };
    }

    private static GarbusPath clonePath(GarbusPath source)
    {
        ensureExactType<GarbusPath>(source, "path");

        return new GarbusPath
        {
            ControlPoints = new BindableList<GarbusPathControlPoint>(source.ControlPoints.Select(clonePathControlPoint)),
        };
    }

    private static GarbusPathControlPoint clonePathControlPoint(GarbusPathControlPoint source)
    {
        ensureExactType<GarbusPathControlPoint>(source, "path control point");

        return new GarbusPathControlPoint
        {
            TimeOffset = source.TimeOffset,
            RotationOffset = source.RotationOffset,
            Smooth = source.Smooth,
            SweepEasing = source.SweepEasing,
        };
    }

    private static GarbusHitSample cloneHitSample(GarbusHitSample source)
    {
        ensureExactType<GarbusHitSample>(source, "hit sample");
        return new GarbusHitSample(source.Name);
    }

    internal static ControlPointInfo CloneControlPointInfo(ControlPointInfo source)
    {
        ensureExactType<ControlPointInfo>(source, "control point info");

        foreach (ControlPointGroup group in source.Groups)
        {
            ensureExactType<ControlPointGroup>(group, "control point group");

            foreach (ControlPoint point in group.ControlPoints)
            {
                ensureExactType<TimingControlPoint>(point, "control point");
                ensureExactType<TimeSignature>(((TimingControlPoint)point).TimeSignature, "time signature");
            }
        }

        ControlPointInfo clone = source.DeepClone();

        for (int i = 0; i < source.TimingPoints.Count; i++)
        {
            TimeSignature sourceSignature = source.TimingPoints[i].TimeSignature;

            // Bindable suppresses value-equal assignments, so change the value before assigning the detached equivalent.
            clone.TimingPoints[i].TimeSignature = new TimeSignature(sourceSignature.Numerator == 1 ? 2 : 1);
            clone.TimingPoints[i].TimeSignature = new TimeSignature(sourceSignature.Numerator);
        }

        foreach (ControlPointGroup group in source.Groups.Where(group => group.ControlPoints.Count == 0))
            clone.GroupAt(group.Time, true);

        return clone;
    }

    private static void ensureExactType<T>(object source, string description)
    {
        if (source.GetType() != typeof(T))
            throw new ArgumentOutOfRangeException(nameof(source), source.GetType().Name, $"{description} type cannot be cloned");
    }
}
