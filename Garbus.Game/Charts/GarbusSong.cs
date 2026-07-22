using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game.Charts.Timing;

namespace Garbus.Game.Charts;

public class GarbusSong
{
    public Guid SongId { get; init; } = Guid.NewGuid();

    public SongMetadata Metadata { get; init; } = new SongMetadata();

    public SongResources Resources { get; init; } = new SongResources();

    public double? PreviewTime { get; set; }

    /// <summary>Shared timing, or null when every chart owns timing.</summary>
    public ControlPointInfo? ControlPointInfo { get; set; }

    public List<GarbusChart> Charts { get; init; } = new List<GarbusChart>();

    public bool UsesSharedTiming => ControlPointInfo != null;

    public GarbusChart GetChart(Guid chartId) =>
        Charts.SingleOrDefault(c => c.ChartId == chartId)
        ?? throw new KeyNotFoundException($"Chart {chartId} does not exist in song {SongId}.");

    public ControlPointInfo GetEffectiveControlPointInfo(GarbusChart chart)
    {
        if (!Charts.Contains(chart))
            throw new ArgumentException("The chart does not belong to this song.", nameof(chart));

        return ControlPointInfo
               ?? chart.ControlPointInfo
               ?? throw new InvalidDataException($"Chart {chart.ChartId} has no timing information.");
    }

    public PlayableChart CreatePlayableChart(Guid chartId)
    {
        var chart = GetChart(chartId);
        chart.ApplyDefaults();
        return new PlayableChart(this, chart, GetEffectiveControlPointInfo(chart));
    }

    public void ValidateStructure()
    {
        if (SongId == Guid.Empty)
            throw new InvalidDataException("Song ID must not be empty.");
        if (Charts.Count == 0)
            throw new InvalidDataException("A song must contain at least one chart.");
        if (Charts.Any(c => c.ChartId == Guid.Empty))
            throw new InvalidDataException("Chart ID must not be empty.");
        if (Charts.GroupBy(c => c.ChartId).Any(g => g.Count() > 1))
            throw new InvalidDataException("Chart IDs must be unique within a song.");

        if (ControlPointInfo != null)
        {
            if (Charts.Any(c => c.ControlPointInfo != null))
                throw new InvalidDataException("Shared song timing cannot be combined with chart-local timing.");
        }
        else if (Charts.Any(c => c.ControlPointInfo == null))
        {
            throw new InvalidDataException("Every chart must contain timing when the song does not use shared timing.");
        }

        validateResourcePath(Resources.Track, "Track");
        validateResourcePath(Resources.Background, "Background");
    }

    private static void validateResourcePath(string resource, string name)
    {
        if (string.IsNullOrEmpty(resource)) return;
        // Portability: a .garbus is authored on one OS and played on another, so the check cannot lean
        // on the host's path semantics (Path.IsPathRooted misses a "C:\..." drive path on Linux, and a
        // "/etc/..." path on Windows). Reject anything rooted on EITHER platform, plus any ".." escape.
        if (isRootedOnAnyPlatform(resource)
            || resource.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
            throw new InvalidDataException($"Song {name} resource must be a relative path within the song directory.");
    }

    private static bool isRootedOnAnyPlatform(string resource) =>
        Path.IsPathRooted(resource)
        || resource[0] == '/' || resource[0] == '\\'                       // unix / UNC / drive-relative root
        || (resource.Length >= 2 && char.IsLetter(resource[0]) && resource[1] == ':'); // "C:" drive prefix

    public static GarbusSong CreateDefault()
    {
        var timing = new ControlPointInfo();
        timing.Add(0, new TimingControlPoint { BeatLength = 500 });

        return new GarbusSong
        {
            ControlPointInfo = timing,
            Charts = new List<GarbusChart>
            {
                new GarbusChart { ControlPointInfo = null },
            },
        };
    }
}
