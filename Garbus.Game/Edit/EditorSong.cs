using System;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;

namespace Garbus.Game.Edit;

public partial class EditorSong : TransactionalCommitComponent
{
    public GarbusSong Song { get; }

    public GarbusChart ActiveChart { get; private set; }

    public Guid ActiveChartId => ActiveChart.ChartId;

    public ControlPointInfo EffectiveControlPointInfo => Song.GetEffectiveControlPointInfo(ActiveChart);

    public event Action<GarbusChart, GarbusChart>? ActiveChartChanged;
    public event Action? ChartsChanged;
    public event Action? TimingSourceChanged;
    public event Action? StateRestored;

    public EditorSong(GarbusSong song, Guid? activeChartId = null)
    {
        song.ValidateStructure();
        Song = song;
        ActiveChart = activeChartId.HasValue ? song.GetChart(activeChartId.Value) : song.Charts[0];
    }

    public void SelectChart(Guid chartId)
    {
        var next = Song.GetChart(chartId);
        if (ReferenceEquals(next, ActiveChart)) return;
        var previous = ActiveChart;
        ActiveChart = next;
        ActiveChartChanged?.Invoke(previous, next);
        TimingSourceChanged?.Invoke();
    }

    public GarbusChart AddChart()
    {
        var chart = new GarbusChart
        {
            ControlPointInfo = Song.UsesSharedTiming ? null : cloneTiming(EffectiveControlPointInfo),
        };

        BeginChange();
        Song.Charts.Add(chart);
        ChartsChanged?.Invoke();
        SaveState();
        EndChange();
        SelectChart(chart.ChartId);
        return chart;
    }

    public bool RemoveActiveChart()
    {
        if (Song.Charts.Count <= 1) return false;
        int index = Song.Charts.IndexOf(ActiveChart);
        BeginChange();
        Song.Charts.RemoveAt(index);
        var previous = ActiveChart;
        ActiveChart = Song.Charts[Math.Min(index, Song.Charts.Count - 1)];
        ChartsChanged?.Invoke();
        ActiveChartChanged?.Invoke(previous, ActiveChart);
        TimingSourceChanged?.Invoke();
        SaveState();
        EndChange();
        return true;
    }

    public void UseSharedTiming()
    {
        if (Song.UsesSharedTiming) return;
        BeginChange();
        Song.ControlPointInfo = cloneTiming(ActiveChart.ControlPointInfo!);
        foreach (var chart in Song.Charts) chart.ControlPointInfo = null;
        TimingSourceChanged?.Invoke();
        SaveState();
        EndChange();
    }

    public void UsePerChartTiming()
    {
        if (!Song.UsesSharedTiming) return;
        BeginChange();
        foreach (var chart in Song.Charts) chart.ControlPointInfo = cloneTiming(Song.ControlPointInfo!);
        Song.ControlPointInfo = null;
        TimingSourceChanged?.Invoke();
        SaveState();
        EndChange();
    }

    public bool ChartTimingsAreIdentical()
    {
        if (Song.UsesSharedTiming) return true;
        string first = timingIdentity(Song.Charts[0].ControlPointInfo!);
        return Song.Charts.Skip(1).All(c => timingIdentity(c.ControlPointInfo!) == first);
    }

    private static ControlPointInfo cloneTiming(ControlPointInfo source)
    {
        var clone = new ControlPointInfo();
        foreach (var point in source.TimingPoints)
        {
            clone.Add(point.Time, new TimingControlPoint
            {
                BeatLength = point.BeatLength,
                TimeSignature = point.TimeSignature,
                OmitFirstBarLine = point.OmitFirstBarLine,
            });
        }
        return clone;
    }

    private static string timingIdentity(ControlPointInfo source) => string.Join(";", source.TimingPoints.Select(
        p => $"{p.Time:R},{p.BeatLength:R},{p.TimeSignature.Numerator},{p.OmitFirstBarLine}"));

    internal void RestoreFrom(GarbusSong target, Guid preferredChartId)
    {
        Song.Metadata.Title = target.Metadata.Title;
        Song.Metadata.Artist = target.Metadata.Artist;
        Song.Metadata.TitleRomanized = target.Metadata.TitleRomanized;
        Song.Metadata.ArtistRomanized = target.Metadata.ArtistRomanized;
        Song.Metadata.Source = target.Metadata.Source;
        Song.Resources.Track = target.Resources.Track;
        Song.Resources.Background = target.Resources.Background;
        Song.PreviewTime = target.PreviewTime;
        Song.ControlPointInfo = target.ControlPointInfo;
        Song.Charts.Clear();
        Song.Charts.AddRange(target.Charts);

        var previous = ActiveChart;
        ActiveChart = Song.Charts.FirstOrDefault(c => c.ChartId == preferredChartId) ?? Song.Charts[0];
        ChartsChanged?.Invoke();
        ActiveChartChanged?.Invoke(previous, ActiveChart);
        TimingSourceChanged?.Invoke();
        StateRestored?.Invoke();
    }

    protected override void UpdateState()
    {
    }
}
