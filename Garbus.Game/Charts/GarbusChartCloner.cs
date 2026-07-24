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
            CardinalNote cardinal => new CardinalNote { AngleDeg = cardinal.AngleDeg },
            CardinalHoldNote hold => new CardinalHoldNote { AngleDeg = hold.AngleDeg, Duration = hold.Duration },
            ShoulderNote shoulder => new ShoulderNote { Side = shoulder.Side },
            ShoulderHoldNote shoulderHold => new ShoulderHoldNote { Side = shoulderHold.Side, Duration = shoulderHold.Duration },
            SliderBody slider => new SliderBody
            {
                AngleDeg = slider.AngleDeg,
                Side = slider.Side,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>(slider.Path.ControlPoints.Select(point => new GarbusPathControlPoint
                    {
                        TimeOffset = point.TimeOffset,
                        RotationOffset = point.RotationOffset,
                        Smooth = point.Smooth,
                        SweepEasing = point.SweepEasing,
                    })),
                },
            },
            GarbusSlamCentered slam => new GarbusSlamCentered { AngleDeg = slam.AngleDeg, Side = slam.Side },
            GarbusSlamEdge slam => new GarbusSlamEdge
            {
                AngleDeg = slam.AngleDeg,
                Side = slam.Side,
                Direction = slam.Direction,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(source), source.GetType().Name, "hit object type cannot be cloned"),
        };

        clone.StartTime = source.StartTime;
        clone.Samples = source.Samples.Select(sample => new GarbusHitSample(sample.Name)).ToList();
        return clone;
    }

    public static ChartMetadata CloneMetadata(ChartMetadata source)
    {
        ArgumentNullException.ThrowIfNull(source);

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

        var clone = new DesignPointInfo();

        foreach (DesignPoint point in source.DesignPoints)
        {
            clone.Add(point switch
            {
                TutorialMessage message => new TutorialMessage
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

        return new GarbusChart
        {
            ChartId = source.ChartId,
            Metadata = CloneMetadata(source.Metadata),
            PreviewTime = source.PreviewTime,
            ControlPointInfo = effectiveControlPointInfo.DeepClone(),
            DesignPointInfo = CloneDesignPointInfo(source.DesignPointInfo),
            HitObjects = source.HitObjects.Select(CloneHitObject).ToList(),
        };
    }
}
