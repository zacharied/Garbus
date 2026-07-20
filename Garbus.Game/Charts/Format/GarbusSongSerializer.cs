using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Objects;

namespace Garbus.Game.Charts.Format;

public static class GarbusSongSerializer
{
    public const int CURRENT_VERSION = 2;
    public const string FILE_EXTENSION = ".garbus";

    private static readonly JsonSerializerOptions options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string Encode(GarbusSong song)
    {
        song.ValidateStructure();
        return JsonSerializer.Serialize(toDto(song), options);
    }

    public static SongDecodeResult Decode(string json) => Decode(Encoding.UTF8.GetBytes(json));

    public static SongDecodeResult Decode(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Decode(memory.ToArray());
    }

    public static SongDecodeResult Decode(byte[] bytes)
    {
        try
        {
            ReadOnlyMemory<byte> jsonBytes = withoutUtf8Bom(bytes);
            using var document = JsonDocument.Parse(jsonBytes);
            if (!document.RootElement.TryGetProperty("version", out var versionProperty))
                throw new InvalidDataException("Song file has no version.");

            int version = versionProperty.GetInt32();
            if (version == 1)
                return new SongDecodeResult(convertV1(bytes, jsonBytes), true);
            if (version != CURRENT_VERSION)
                throw new InvalidDataException($"Unsupported song format version {version} (this build supports versions 1 and {CURRENT_VERSION}).");

            var dto = JsonSerializer.Deserialize<SongFileDto>(jsonBytes.Span, options)
                      ?? throw new InvalidDataException("Song file contained no data.");
            var song = fromDto(dto);
            song.ValidateStructure();
            return new SongDecodeResult(song, false);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Song file contains invalid JSON: {ex.Message}", ex);
        }
    }

    public static string EncodeHitObject(GarbusHitObject hitObject) => GarbusChartSerializer.EncodeHitObject(hitObject);
    public static string EncodeHitObjects(IEnumerable<GarbusHitObject> hitObjects) => GarbusChartSerializer.EncodeHitObjects(hitObjects);
    public static IReadOnlyList<GarbusHitObject> DecodeHitObjects(string json) => GarbusChartSerializer.DecodeHitObjects(json);

    private static SongFileDto toDto(GarbusSong song) => new SongFileDto
    {
        Version = CURRENT_VERSION,
        Id = song.SongId,
        Metadata = new SongMetadataDto
        {
            Title = song.Metadata.Title,
            Artist = song.Metadata.Artist,
            TitleRomanized = song.Metadata.TitleRomanized,
            ArtistRomanized = song.Metadata.ArtistRomanized,
            Source = song.Metadata.Source,
        },
        Resources = new SongResourcesDto
        {
            Track = song.Resources.Track,
            Background = song.Resources.Background,
        },
        PreviewTime = song.PreviewTime,
        TimingPoints = song.ControlPointInfo == null ? null : encodeTiming(song.ControlPointInfo),
        Charts = song.Charts.Select(c => toDto(c, song.ControlPointInfo == null)).ToList(),
    };

    private static SongChartDto toDto(GarbusChart chart, bool includeTiming)
    {
        ChartFileDto legacy = getLegacyDto(chart);
        return new SongChartDto
        {
            Id = chart.ChartId,
            Metadata = new SongChartMetadataDto
            {
                ChartName = chart.Metadata.ChartName,
                Charter = chart.Metadata.Charter,
                Level = chart.Metadata.Level,
                Difficulty = chart.Metadata.Difficulty.ToString(),
            },
            TimingPoints = includeTiming ? encodeTiming(chart.ControlPointInfo!) : null,
            HitObjects = legacy.HitObjects,
            DesignPoints = legacy.DesignPoints,
        };
    }

    private static GarbusSong fromDto(SongFileDto dto)
    {
        if (dto.Metadata == null) throw new InvalidDataException("Song metadata is required.");
        if (dto.Resources == null) throw new InvalidDataException("Song resources are required.");
        if (dto.Charts == null) throw new InvalidDataException("Song charts are required.");

        var song = new GarbusSong
        {
            SongId = dto.Id,
            Metadata = new SongMetadata
            {
                Title = dto.Metadata.Title,
                Artist = dto.Metadata.Artist,
                TitleRomanized = dto.Metadata.TitleRomanized,
                ArtistRomanized = dto.Metadata.ArtistRomanized ?? string.Empty,
                Source = dto.Metadata.Source,
            },
            Resources = new SongResources
            {
                Track = dto.Resources.Track,
                Background = dto.Resources.Background,
            },
            PreviewTime = dto.PreviewTime,
            ControlPointInfo = dto.TimingPoints == null ? null : decodeTiming(dto.TimingPoints),
        };

        foreach (var chartDto in dto.Charts)
            song.Charts.Add(fromDto(chartDto));

        return song;
    }

    private static GarbusChart fromDto(SongChartDto dto)
    {
        if (dto.Metadata == null) throw new InvalidDataException($"Chart {dto.Id} metadata is required.");
        if (dto.HitObjects == null) throw new InvalidDataException($"Chart {dto.Id} hit objects are required.");
        if (dto.DesignPoints == null) throw new InvalidDataException($"Chart {dto.Id} design points are required.");

        var legacyDto = new ChartFileDto
        {
            Version = GarbusChartSerializer.CURRENT_VERSION,
            Metadata = new ChartMetadataDto
            {
                ChartName = dto.Metadata.ChartName,
                Charter = dto.Metadata.Charter,
                Level = dto.Metadata.Level,
                Difficulty = dto.Metadata.Difficulty,
            },
            TimingPoints = dto.TimingPoints ?? new List<TimingPointDto>(),
            HitObjects = dto.HitObjects,
            DesignPoints = dto.DesignPoints,
        };

        GarbusChart decoded = GarbusChartSerializer.Decode(JsonSerializer.Serialize(legacyDto, options));
        return new GarbusChart
        {
            ChartId = dto.Id,
            Metadata = decoded.Metadata,
            ControlPointInfo = dto.TimingPoints == null ? null : decoded.ControlPointInfo,
            HitObjects = decoded.HitObjects,
            DesignPointInfo = decoded.DesignPointInfo,
        };
    }

    private static GarbusSong convertV1(byte[] exactBytes, ReadOnlyMemory<byte> jsonBytes)
    {
        string json = Encoding.UTF8.GetString(jsonBytes.Span);
        GarbusChart legacy = GarbusChartSerializer.Decode(json);
        var song = new GarbusSong
        {
            SongId = deterministicGuid("song", exactBytes),
            Metadata = new SongMetadata
            {
                Title = legacy.Metadata.Title,
                Artist = legacy.Metadata.Artist,
                TitleRomanized = legacy.Metadata.RomanisedTitle,
                ArtistRomanized = legacy.Metadata.RomanisedArtist,
                Source = legacy.Metadata.Source,
            },
            Resources = new SongResources
            {
                Track = legacy.Metadata.AudioFile,
                Background = legacy.Metadata.BackgroundFile,
            },
            PreviewTime = legacy.PreviewTime,
            ControlPointInfo = legacy.ControlPointInfo,
            Charts = new List<GarbusChart>
            {
                new GarbusChart
                {
                    ChartId = deterministicGuid("chart", exactBytes),
                    Metadata = new ChartMetadata
                    {
                        ChartName = legacy.Metadata.ChartName,
                        Charter = legacy.Metadata.Charter,
                        Level = legacy.Metadata.Level,
                        Difficulty = legacy.Metadata.Difficulty,
                    },
                    ControlPointInfo = null,
                    HitObjects = legacy.HitObjects,
                    DesignPointInfo = legacy.DesignPointInfo,
                },
            },
        };
        song.ValidateStructure();
        return song;
    }

    private static ReadOnlyMemory<byte> withoutUtf8Bom(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf
            ? bytes.AsMemory(3)
            : bytes;

    private static Guid deterministicGuid(string domain, byte[] source)
    {
        byte[] prefix = Encoding.UTF8.GetBytes(domain + "\0");
        byte[] input = new byte[prefix.Length + source.Length];
        Buffer.BlockCopy(prefix, 0, input, 0, prefix.Length);
        Buffer.BlockCopy(source, 0, input, prefix.Length, source.Length);
        byte[] guidBytes = SHA256.HashData(input).Take(16).ToArray();
        guidBytes[6] = (byte)((guidBytes[6] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes, bigEndian: true);
    }

    private static List<TimingPointDto> encodeTiming(ControlPointInfo timing) => timing.TimingPoints.Select(t => new TimingPointDto
    {
        Time = t.Time,
        BeatLength = t.BeatLength,
        TimeSignature = t.TimeSignature.Numerator,
        OmitFirstBarLine = t.OmitFirstBarLine,
    }).ToList();

    private static ControlPointInfo decodeTiming(IEnumerable<TimingPointDto> timingPoints)
    {
        var result = new ControlPointInfo();
        foreach (var timing in timingPoints)
        {
            result.Add(timing.Time, new TimingControlPoint
            {
                BeatLength = timing.BeatLength,
                TimeSignature = new TimeSignature(timing.TimeSignature),
                OmitFirstBarLine = timing.OmitFirstBarLine,
            });
        }
        return result;
    }

    private static ChartFileDto getLegacyDto(GarbusChart chart)
    {
        var bridge = new GarbusChart
        {
            Metadata = chart.Metadata,
            ControlPointInfo = chart.ControlPointInfo ?? new ControlPointInfo(),
            HitObjects = chart.HitObjects,
            DesignPointInfo = chart.DesignPointInfo,
        };
        return JsonSerializer.Deserialize<ChartFileDto>(GarbusChartSerializer.Encode(bridge), options)!;
    }
}
