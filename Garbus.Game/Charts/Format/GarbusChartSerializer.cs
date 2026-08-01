// Encoder/decoder between GarbusChart and the versioned .garbus JSON format (ChartFileDto). This
// replaces osu's decoder/encoder pipeline entirely — charts are loaded directly, no conversion step.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Core;
using Garbus.Game.Objects;

namespace Garbus.Game.Charts.Format;

public static class GarbusChartSerializer
{
    /// <summary>
    /// The current chart format version. Bump on breaking format changes; the decoder rejects
    /// versions it does not know.
    /// </summary>
    public const int CURRENT_VERSION = 1;

    /// <summary>
    /// The file extension for native charts, dot included.
    /// </summary>
    public const string FILE_EXTENSION = ".garbus";

    private static readonly JsonSerializerOptions options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string Encode(GarbusChart chart) => JsonSerializer.Serialize(toDto(chart), options);

    /// <summary>
    /// Encodes a single hit object to a compact JSON string used for per-object identity comparison
    /// in the undo/redo diff (GarbusChartChangeHandler.ApplyStateChange).
    /// </summary>
    internal static string EncodeHitObject(GarbusHitObject hitObject)
        => JsonSerializer.Serialize(toDto(hitObject), hitObjectOptions);

    /// <summary>
    /// Encodes a collection of hit objects to a JSON array string, reusing the same DTO mapping
    /// used by the full chart encoder. Suitable for clipboard content.
    /// </summary>
    public static string EncodeHitObjects(IEnumerable<GarbusHitObject> hitObjects)
        => JsonSerializer.Serialize(hitObjects.Select(toDto).ToList(), hitObjectsOptions);

    /// <summary>
    /// Decodes a JSON array of hit objects previously produced by <see cref="EncodeHitObjects"/>.
    /// Returns fresh object instances (deep-cloned through the DTO mapping).
    /// </summary>
    public static IReadOnlyList<GarbusHitObject> DecodeHitObjects(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<HitObjectDto>>(json, hitObjectsOptions)
                   ?? throw new InvalidDataException("Clipboard data contained no hit objects.");
        return dtos.Select(fromDto).ToList();
    }

    // Options for the hit-object array codec (clipboard / copy-paste).
    private static readonly JsonSerializerOptions hitObjectsOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    // Compact (no indentation) options for the per-object identity strings.
    private static readonly JsonSerializerOptions hitObjectOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static GarbusChart Decode(string json)
    {
        var dto = JsonSerializer.Deserialize<ChartFileDto>(json, options)
                  ?? throw new InvalidDataException("Chart file contained no data.");

        if (dto.Version != CURRENT_VERSION)
            throw new InvalidDataException($"Unsupported chart format version {dto.Version} (this build supports version {CURRENT_VERSION}).");

        return fromDto(dto);
    }

    public static GarbusChart Decode(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return Decode(reader.ReadToEnd());
    }

    /// <summary>
    /// The chart's own timing. A standalone chart file always carries its timing inline, so a chart
    /// deferring to song-level shared timing has nothing to write here and belongs in
    /// <see cref="GarbusSongSerializer"/> instead.
    /// </summary>
    private static ControlPointInfo ownTiming(GarbusChart chart) =>
        chart.ControlPointInfo
        ?? throw new InvalidOperationException(
            $"Chart {chart.ChartId} defers to song-level shared timing and cannot be encoded as a standalone chart file.");

    private static ChartFileDto toDto(GarbusChart chart) => new ChartFileDto
    {
        Version = CURRENT_VERSION,
        Metadata = new ChartMetadataDto
        {
            Title = chart.Metadata.Title,
            Artist = chart.Metadata.Artist,
            Charter = chart.Metadata.Charter,
            ChartName = chart.Metadata.ChartName,
            RomanisedTitle = chart.Metadata.RomanisedTitle,
            RomanisedArtist = chart.Metadata.RomanisedArtist,
            Source = chart.Metadata.Source,
            Tags = chart.Metadata.Tags,
            AudioFile = chart.Metadata.AudioFile,
            BackgroundFile = chart.Metadata.BackgroundFile,
            Level = chart.Metadata.Level,
            Difficulty = chart.Metadata.Difficulty.ToString(),
        },
        PreviewTime = chart.PreviewTime,
        TimingPoints = ownTiming(chart).TimingPoints.Select(t => new TimingPointDto
        {
            Time = t.Time,
            BeatLength = t.BeatLength,
            TimeSignature = t.TimeSignature.Numerator,
            OmitFirstBarLine = t.OmitFirstBarLine,
        }).ToList(),
        HitObjects = chart.HitObjects.Select(toDto).ToList(),
        DesignPoints = chart.DesignPointInfo.DesignPoints.Select(toDto).ToList(),
    };

    private static DesignPointDto toDto(DesignPoint point) => point switch
    {
        TutorialMessage tm => new TutorialMessageDto { StartTime = tm.StartTime, EndTime = tm.EndTime, Text = tm.Text },
        _ => throw new ArgumentOutOfRangeException(nameof(point), point.GetType().Name, "design point type has no chart format representation")
    };

    private static DesignPoint fromDto(DesignPointDto dto) => dto switch
    {
        TutorialMessageDto tm => new TutorialMessage { StartTime = tm.StartTime, EndTime = tm.EndTime, Text = tm.Text },
        _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.GetType().Name, "unknown design point dto type")
    };

    private static HitObjectDto toDto(GarbusHitObject hitObject)
    {
        HitObjectDto dto = hitObject switch
        {
            CardinalNote cardinal => new CardinalNoteDto { AngleDeg = cardinal.AngleDeg },
            CardinalHoldNote hold => new CardinalHoldNoteDto { AngleDeg = hold.AngleDeg, Duration = hold.Duration },
            ShoulderNote shoulder => new ShoulderNoteDto { Side = shoulder.Side.ToString() },
            ShoulderHoldNote shoulderHold => new ShoulderHoldNoteDto { Side = shoulderHold.Side.ToString(), Duration = shoulderHold.Duration },
            SliderBody slider => new SliderBodyDto
            {
                AngleDeg = slider.AngleDeg,
                Side = slider.Side.ToString(),
                ControlPoints = slider.Path.ControlPoints.Select(c => new PathControlPointDto
                {
                    TimeOffset = c.TimeOffset,
                    RotationOffset = c.RotationOffset,
                    Smooth = c.Smooth,
                    SweepEasing = c.SweepEasing.ToString(),
                    ShapeOnly = c.ShapeOnly,
                }).ToList(),
            },
            GarbusSlamCentered slam => new SlamCenteredDto { AngleDeg = slam.AngleDeg, Side = slam.Side.ToString() },
            GarbusSlamEdge slam => new SlamEdgeDto { AngleDeg = slam.AngleDeg, Side = slam.Side.ToString(), Direction = slam.Direction.ToString() },
            _ => throw new ArgumentOutOfRangeException(nameof(hitObject), hitObject.GetType().Name, "hit object type has no chart format representation")
        };

        dto.Time = hitObject.StartTime;
        return dto;
    }

    private static GarbusChart fromDto(ChartFileDto dto)
    {
        var chart = new GarbusChart
        {
            Metadata = new ChartMetadata
            {
                Title = dto.Metadata.Title,
                Artist = dto.Metadata.Artist,
                Charter = dto.Metadata.Charter,
                ChartName = dto.Metadata.ChartName,
                RomanisedTitle = dto.Metadata.RomanisedTitle,
                RomanisedArtist = dto.Metadata.RomanisedArtist,
                Source = dto.Metadata.Source,
                Tags = dto.Metadata.Tags,
                AudioFile = dto.Metadata.AudioFile,
                BackgroundFile = dto.Metadata.BackgroundFile,
                Level = dto.Metadata.Level,
                Difficulty = parseEnum<Difficulty>(dto.Metadata.Difficulty),
            },
            PreviewTime = dto.PreviewTime,
            HitObjects = dto.HitObjects.Select(fromDto).ToList(),
        };

        foreach (var timing in dto.TimingPoints)
        {
            chart.ControlPointInfo!.Add(timing.Time, new TimingControlPoint
            {
                BeatLength = timing.BeatLength,
                TimeSignature = new TimeSignature(timing.TimeSignature),
                OmitFirstBarLine = timing.OmitFirstBarLine,
            });
        }

        foreach (var designPoint in dto.DesignPoints)
            chart.DesignPointInfo.Add(fromDto(designPoint));

        return chart;
    }

    private static GarbusHitObject fromDto(HitObjectDto dto)
    {
        GarbusHitObject hitObject = dto switch
        {
            CardinalNoteDto cardinal => new CardinalNote { AngleDeg = cardinal.AngleDeg },
            CardinalHoldNoteDto hold => new CardinalHoldNote { AngleDeg = hold.AngleDeg, Duration = hold.Duration },
            ShoulderNoteDto shoulder => new ShoulderNote { Side = parseEnum<HorizontalDirection>(shoulder.Side) },
            ShoulderHoldNoteDto shoulderHold => new ShoulderHoldNote { Side = parseEnum<HorizontalDirection>(shoulderHold.Side), Duration = shoulderHold.Duration },
            SliderBodyDto slider => decodeSlider(slider),
            SlamCenteredDto slam => new GarbusSlamCentered { AngleDeg = slam.AngleDeg, Side = parseEnum<HorizontalDirection>(slam.Side) },
            SlamEdgeDto slam => new GarbusSlamEdge
            {
                AngleDeg = slam.AngleDeg,
                Side = parseEnum<HorizontalDirection>(slam.Side),
                Direction = parseEnum<RotationalDirection>(slam.Direction),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.GetType().Name, "unknown hit object dto type")
        };

        hitObject.StartTime = dto.Time;
        return hitObject;
    }

    private static SliderBody decodeSlider(SliderBodyDto dto)
    {
        // The last control point is never shape-only — a trailing one would leave the body's tail
        // ungraded. The editor upholds this, so a violation means a hand-edited file.
        if (dto.ControlPoints.Count > 0 && dto.ControlPoints[^1].ShapeOnly)
            throw new InvalidDataException("A slider's last control point cannot be shape-only.");

        return new SliderBody
        {
            AngleDeg = dto.AngleDeg,
            Side = parseEnum<HorizontalDirection>(dto.Side),
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>(dto.ControlPoints.Select(c => new GarbusPathControlPoint
                {
                    TimeOffset = c.TimeOffset,
                    RotationOffset = c.RotationOffset,
                    Smooth = c.Smooth,
                    SweepEasing = parseEnum<Easing>(c.SweepEasing),
                    ShapeOnly = c.ShapeOnly,
                })),
            },
        };
    }

    private static T parseEnum<T>(string value) where T : struct, Enum
        => Enum.TryParse<T>(value, out var parsed)
            ? parsed
            : throw new InvalidDataException($"\"{value}\" is not a valid {typeof(T).Name}.");
}
