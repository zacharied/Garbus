using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Garbus.Game.Charts.Format;

public class SongFileDto
{
    public int Version { get; set; }
    public Guid Id { get; set; }
    public SongMetadataDto Metadata { get; set; } = new SongMetadataDto();
    public SongResourcesDto Resources { get; set; } = new SongResourcesDto();
    public double? PreviewTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TimingPointDto>? TimingPoints { get; set; }

    public List<SongChartDto> Charts { get; set; } = new List<SongChartDto>();
}

public class SongMetadataDto
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string TitleRomanized { get; set; } = string.Empty;
    public string ArtistRomanized { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public class SongResourcesDto
{
    public string Track { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
}

public class SongChartDto
{
    public Guid Id { get; set; }
    public SongChartMetadataDto Metadata { get; set; } = new SongChartMetadataDto();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TimingPointDto>? TimingPoints { get; set; }

    public List<HitObjectDto> HitObjects { get; set; } = new List<HitObjectDto>();
    public List<DesignPointDto> DesignPoints { get; set; } = new List<DesignPointDto>();
}

public class SongChartMetadataDto
{
    public string ChartName { get; set; } = string.Empty;
    public string Charter { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Difficulty { get; set; } = nameof(Charts.Difficulty.Novice);
}
