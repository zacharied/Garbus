using System;
using System.IO;
using System.Linq;
using System.Text;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using Garbus.Game.Charts.Timing;
using NUnit.Framework;

namespace Garbus.Game.Tests.Charts;

[TestFixture]
public class TestSongFormat
{
    [Test]
    public void SharedTimingRoundTripsMultipleCharts()
    {
        var song = GarbusSong.CreateDefault();
        song.Metadata.Title = "Title";
        song.Resources.Track = "track.ogg";
        song.Charts.Add(new GarbusChart
        {
            ControlPointInfo = null,
            Metadata = new ChartMetadata { ChartName = "Expert", Difficulty = Difficulty.Expert, Level = 9 },
        });

        string json = GarbusSongSerializer.Encode(song);
        SongDecodeResult decoded = GarbusSongSerializer.Decode(json);

        Assert.Multiple(() =>
        {
            Assert.That(decoded.WasConvertedFromVersion1, Is.False);
            Assert.That(decoded.Song.SongId, Is.EqualTo(song.SongId));
            Assert.That(decoded.Song.Charts.Select(c => c.ChartId), Is.EqualTo(song.Charts.Select(c => c.ChartId)));
            Assert.That(decoded.Song.ControlPointInfo, Is.Not.Null);
            Assert.That(decoded.Song.Charts.All(c => c.ControlPointInfo == null));
            Assert.That(decoded.Song.Metadata.Title, Is.EqualTo("Title"));
            Assert.That(decoded.Song.Resources.Track, Is.EqualTo("track.ogg"));
        });
    }

    [Test]
    public void PerChartTimingRoundTripsWithSongTimingOmitted()
    {
        var timing = new ControlPointInfo();
        timing.Add(100, new TimingControlPoint { BeatLength = 400 });
        var song = new GarbusSong
        {
            ControlPointInfo = null,
            Charts = { new GarbusChart { ControlPointInfo = timing } },
        };

        string json = GarbusSongSerializer.Encode(song);
        Assert.That(json, Does.Not.Contain("\"timingPoints\": null"));
        var decoded = GarbusSongSerializer.Decode(json).Song;
        Assert.That(decoded.ControlPointInfo, Is.Null);
        Assert.That(decoded.Charts.Single().ControlPointInfo!.TimingPoints.Single().Time, Is.EqualTo(100));
    }

    [Test]
    public void VersionOneConversionIsDeterministic()
    {
        var legacy = GarbusTestChartGenerator.GenerateChart();
        legacy.Metadata.RomanisedTitle = "Romanized";
        legacy.Metadata.Source = "Source";
        legacy.Metadata.BackgroundFile = "bg.png";
        legacy.PreviewTime = 1234;
        string json = GarbusChartSerializer.Encode(legacy);

        SongDecodeResult first = GarbusSongSerializer.Decode(json);
        SongDecodeResult second = GarbusSongSerializer.Decode(json);

        Assert.Multiple(() =>
        {
            Assert.That(first.WasConvertedFromVersion1, Is.True);
            Assert.That(first.Song.SongId, Is.EqualTo(second.Song.SongId));
            Assert.That(first.Song.Charts.Single().ChartId, Is.EqualTo(second.Song.Charts.Single().ChartId));
            Assert.That(first.Song.SongId, Is.Not.EqualTo(first.Song.Charts.Single().ChartId));
            Assert.That(first.Song.Metadata.TitleRomanized, Is.EqualTo("Romanized"));
            Assert.That(first.Song.Metadata.Source, Is.EqualTo("Source"));
            Assert.That(first.Song.Resources.Track, Is.EqualTo(legacy.Metadata.AudioFile));
            Assert.That(first.Song.Resources.Background, Is.EqualTo("bg.png"));
            Assert.That(first.Song.PreviewTime, Is.EqualTo(1234));
            Assert.That(first.Song.ControlPointInfo, Is.Not.Null);
            Assert.That(first.Song.Charts.Single().ControlPointInfo, Is.Null);
        });
    }

    [Test]
    public void EmptySongIdRejected()
    {
        var song = GarbusSong.CreateDefault();
        var invalid = new GarbusSong
        {
            SongId = Guid.Empty,
            ControlPointInfo = song.ControlPointInfo,
            Charts = song.Charts,
        };
        Assert.Throws<InvalidDataException>(() => GarbusSongSerializer.Encode(invalid));
    }

    [Test]
    public void UnknownVersionIsRejectedWithActionableError()
    {
        var error = Assert.Throws<InvalidDataException>(() => GarbusSongSerializer.Decode("{\"version\":99}"));
        Assert.That(error!.Message, Does.Contain("Unsupported song format version 99"));
    }

    [Test]
    public void OmittedTimingIsDistinctFromExplicitEmptyTiming()
    {
        var song = new GarbusSong
        {
            ControlPointInfo = null,
            Charts = { new GarbusChart { ControlPointInfo = new ControlPointInfo() } },
        };

        string json = GarbusSongSerializer.Encode(song);
        Assert.That(json, Does.Contain("\"timingPoints\": []"));

        var decoded = GarbusSongSerializer.Decode(json).Song;
        Assert.Multiple(() =>
        {
            Assert.That(decoded.ControlPointInfo, Is.Null);
            Assert.That(decoded.Charts.Single().ControlPointInfo, Is.Not.Null);
            Assert.That(decoded.Charts.Single().ControlPointInfo!.TimingPoints, Is.Empty);
        });
    }

    [TestCase("../outside.ogg")]
    [TestCase("C:\\outside.ogg")]
    public void ResourcePathsMustStayInsideSongDirectory(string path)
    {
        var song = GarbusSong.CreateDefault();
        song.Resources.Track = path;
        Assert.Throws<InvalidDataException>(() => GarbusSongSerializer.Encode(song));
    }

    [Test]
    public void Utf8BomFilesDecodeAndStillUseExactBytesForLegacyIds()
    {
        string legacy = GarbusChartSerializer.Encode(GarbusTestChartGenerator.GenerateChart());
        byte[] plain = Encoding.UTF8.GetBytes(legacy);
        byte[] withBom = Encoding.UTF8.GetPreamble().Concat(plain).ToArray();

        var plainSong = GarbusSongSerializer.Decode(plain).Song;
        var bomSong = GarbusSongSerializer.Decode(withBom).Song;

        Assert.Multiple(() =>
        {
            Assert.That(bomSong.Metadata.Title, Is.EqualTo(plainSong.Metadata.Title));
            Assert.That(bomSong.SongId, Is.Not.EqualTo(plainSong.SongId));
        });
    }
}
