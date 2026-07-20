using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Charts.Design;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor;

[TestFixture]
public class TestEditorSong
{
    [Test]
    public void SelectionDoesNotChangeSerializedSong()
    {
        var song = GarbusSong.CreateDefault();
        song.Charts.Add(new GarbusChart { ControlPointInfo = null });
        var editor = new EditorSong(song);
        string before = GarbusSongSerializer.Encode(song);

        editor.SelectChart(song.Charts[1].ChartId);

        Assert.Multiple(() =>
        {
            Assert.That(editor.ActiveChart, Is.SameAs(song.Charts[1]));
            Assert.That(GarbusSongSerializer.Encode(song), Is.EqualTo(before));
        });
    }

    [Test]
    public void AddChartUnderSharedTimingLeavesLocalTimingNull()
    {
        var editor = new EditorSong(GarbusSong.CreateDefault());

        var added = editor.AddChart();

        Assert.Multiple(() =>
        {
            Assert.That(editor.Song.Charts, Has.Count.EqualTo(2));
            Assert.That(editor.ActiveChart, Is.SameAs(added));
            Assert.That(added.ChartId, Is.Not.EqualTo(editor.Song.Charts[0].ChartId));
            Assert.That(added.ControlPointInfo, Is.Null);
            Assert.That(editor.EffectiveControlPointInfo, Is.SameAs(editor.Song.ControlPointInfo));
        });
    }

    [Test]
    public void AddChartUnderPerChartTimingDeepCopiesActiveTiming()
    {
        var song = GarbusSong.CreateDefault();
        var editor = new EditorSong(song);
        editor.UsePerChartTiming();
        var original = editor.ActiveChart.ControlPointInfo!;

        var added = editor.AddChart();

        Assert.Multiple(() =>
        {
            Assert.That(added.ControlPointInfo, Is.Not.SameAs(original));
            Assert.That(added.ControlPointInfo!.TimingPoints.Select(p => (p.Time, p.BeatLength)),
                Is.EqualTo(original.TimingPoints.Select(p => (p.Time, p.BeatLength))));
        });
    }

    [Test]
    public void RemoveSelectsNearestRemainingChartAndProtectsLastChart()
    {
        var editor = new EditorSong(GarbusSong.CreateDefault());
        var middle = editor.AddChart();
        var last = editor.AddChart();
        editor.SelectChart(middle.ChartId);

        Assert.That(editor.RemoveActiveChart(), Is.True);
        Assert.That(editor.ActiveChart, Is.SameAs(last));
        Assert.That(editor.Song.Charts, Has.Count.EqualTo(2));

        Assert.That(editor.RemoveActiveChart(), Is.True);
        Assert.That(editor.Song.Charts, Has.Count.EqualTo(1));
        Assert.That(editor.RemoveActiveChart(), Is.False);
    }

    [Test]
    public void TimingOwnershipConversionsNeverLeaveMixedState()
    {
        var editor = new EditorSong(GarbusSong.CreateDefault());
        editor.AddChart();

        editor.UsePerChartTiming();
        Assert.Multiple(() =>
        {
            Assert.That(editor.Song.ControlPointInfo, Is.Null);
            Assert.That(editor.Song.Charts.All(c => c.ControlPointInfo != null), Is.True);
        });

        editor.UseSharedTiming();
        Assert.Multiple(() =>
        {
            Assert.That(editor.Song.ControlPointInfo, Is.Not.Null);
            Assert.That(editor.Song.Charts.All(c => c.ControlPointInfo == null), Is.True);
        });
        Assert.DoesNotThrow(editor.Song.ValidateStructure);
    }

    [Test]
    public void ActiveChartFacadeRebindsObjectsDesignAndPerChartTiming()
    {
        var firstTiming = timingAt(0, 500);
        var secondTiming = timingAt(100, 400);
        var first = new GarbusChart { ControlPointInfo = firstTiming };
        first.HitObjects.Add(new CardinalNote { StartTime = 1000, AngleDeg = 0 });
        first.DesignPointInfo.Add(new TutorialMessage { StartTime = 0, EndTime = 1000, Text = "first" });
        var second = new GarbusChart { ControlPointInfo = secondTiming };
        second.HitObjects.Add(new CardinalNote { StartTime = 2000, AngleDeg = 0 });
        second.DesignPointInfo.Add(new TutorialMessage { StartTime = 0, EndTime = 1000, Text = "second" });
        var song = new GarbusSong { ControlPointInfo = null, Charts = { first, second } };
        var editorSong = new EditorSong(song);
        var editorChart = new EditorChart(first, firstTiming);
        editorSong.ActiveChartChanged += (_, chart) => editorChart.Rebind(chart, editorSong.EffectiveControlPointInfo);
        editorSong.TimingSourceChanged += () => editorChart.Rebind(editorSong.ActiveChart, editorSong.EffectiveControlPointInfo);
        editorChart.SelectedHitObjects.Add(first.HitObjects[0]);

        editorSong.SelectChart(second.ChartId);

        Assert.Multiple(() =>
        {
            Assert.That(editorChart.Chart, Is.SameAs(second));
            Assert.That(editorChart.ControlPointInfo, Is.SameAs(secondTiming));
            Assert.That(editorChart.HitObjects.Single().StartTime, Is.EqualTo(2000));
            Assert.That(((TutorialMessage)editorChart.DesignPointInfo.DesignPoints.Single()).Text, Is.EqualTo("second"));
            Assert.That(editorChart.SelectedHitObjects, Is.Empty);
        });
    }

    [Test]
    public void SharedTimingObjectIsStableAcrossChartSelection()
    {
        var song = GarbusSong.CreateDefault();
        song.Charts.Add(new GarbusChart { ControlPointInfo = null });
        var editorSong = new EditorSong(song);
        var editorChart = new EditorChart(editorSong.ActiveChart, editorSong.EffectiveControlPointInfo);
        editorSong.ActiveChartChanged += (_, chart) => editorChart.Rebind(chart, editorSong.EffectiveControlPointInfo);

        editorSong.SelectChart(song.Charts[1].ChartId);

        Assert.That(editorChart.ControlPointInfo, Is.SameAs(song.ControlPointInfo));
    }

    private static ControlPointInfo timingAt(double time, double beatLength)
    {
        var timing = new ControlPointInfo();
        timing.Add(time, new TimingControlPoint { BeatLength = beatLength });
        return timing;
    }
}
