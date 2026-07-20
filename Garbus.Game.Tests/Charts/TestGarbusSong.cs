using System;
using System.IO;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using NUnit.Framework;

namespace Garbus.Game.Tests.Charts;

[TestFixture]
public class TestGarbusSong
{
    [Test]
    public void DefaultSongHasOneChartAndSharedTiming()
    {
        var song = GarbusSong.CreateDefault();
        Assert.Multiple(() =>
        {
            Assert.That(song.SongId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(song.Charts, Has.Count.EqualTo(1));
            Assert.That(song.Charts[0].ChartId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(song.ControlPointInfo, Is.Not.Null);
            Assert.That(song.Charts[0].ControlPointInfo, Is.Null);
            Assert.That(song.GetEffectiveControlPointInfo(song.Charts[0]), Is.SameAs(song.ControlPointInfo));
        });
        Assert.DoesNotThrow(song.ValidateStructure);
    }

    [Test]
    public void PerChartTimingResolvesLocally()
    {
        var chart = new GarbusChart { ControlPointInfo = new ControlPointInfo() };
        var song = new GarbusSong { ControlPointInfo = null, Charts = { chart } };
        Assert.That(song.GetEffectiveControlPointInfo(chart), Is.SameAs(chart.ControlPointInfo));
        Assert.DoesNotThrow(song.ValidateStructure);
    }

    [Test]
    public void MixedTimingRejected()
    {
        var song = GarbusSong.CreateDefault();
        song.Charts[0].ControlPointInfo = new ControlPointInfo();
        Assert.Throws<InvalidDataException>(song.ValidateStructure);
    }

    [Test]
    public void DuplicateChartIdRejected()
    {
        Guid id = Guid.NewGuid();
        var song = new GarbusSong
        {
            ControlPointInfo = new ControlPointInfo(),
            Charts =
            {
                new GarbusChart { ChartId = id, ControlPointInfo = null },
                new GarbusChart { ChartId = id, ControlPointInfo = null },
            },
        };
        Assert.Throws<InvalidDataException>(song.ValidateStructure);
    }
}
