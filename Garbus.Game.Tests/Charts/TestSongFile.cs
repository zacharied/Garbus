using System;
using System.IO;
using Garbus.Game.Charts;
using NUnit.Framework;

namespace Garbus.Game.Tests.Charts;

[TestFixture]
public class TestSongFile
{
    private string tempDir = null!;

    [SetUp]
    public void SetUp() => tempDir = Directory.CreateTempSubdirectory("garbus-song-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(tempDir, true);

    [Test]
    public void SaveLoadRoundTrip()
    {
        var song = GarbusSong.CreateDefault();
        song.Metadata.Title = "Song";
        string path = Path.Combine(tempDir, "song.garbus");
        var file = new SongFile(song);
        file.Save(path);

        var loaded = SongFile.Load(path);
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Song.SongId, Is.EqualTo(song.SongId));
            Assert.That(loaded.Song.Metadata.Title, Is.EqualTo("Song"));
            Assert.That(loaded.NeedsVersionUpgrade, Is.False);
        });
    }

    [Test]
    public void V1LoadNeedsUpgradeButDoesNotRewrite()
    {
        string path = Path.Combine(tempDir, "legacy.garbus");
        string json = Garbus.Game.Charts.Format.GarbusChartSerializer.Encode(GarbusTestChartGenerator.GenerateChart());
        File.WriteAllText(path, json);

        var file = SongFile.Load(path);
        Assert.That(file.NeedsVersionUpgrade, Is.True);
        Assert.That(File.ReadAllText(path), Is.EqualTo(json));

        file.Save();
        Assert.That(file.NeedsVersionUpgrade, Is.False);
        Assert.That(File.ReadAllText(path), Does.Contain("\"version\": 2"));
    }

    [Test]
    public void SaveAsCopiesSharedResources()
    {
        string first = Path.Combine(tempDir, "first");
        string second = Path.Combine(tempDir, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        var song = GarbusSong.CreateDefault();
        song.Resources.Track = "audio.ogg";
        song.Resources.Background = "bg.png";
        File.WriteAllBytes(Path.Combine(first, "audio.ogg"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(first, "bg.png"), new byte[] { 2 });

        var file = new SongFile(song);
        file.Save(Path.Combine(first, "song.garbus"));
        file.Save(Path.Combine(second, "song.garbus"));

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(second, "audio.ogg")));
            Assert.That(File.Exists(Path.Combine(second, "bg.png")));
            Assert.That(file.Directory, Is.EqualTo(second));
        });
    }

    [Test]
    public void FailedSaveAsKeepsOriginalRoot()
    {
        string first = Path.Combine(tempDir, "first");
        string second = Path.Combine(tempDir, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        var song = GarbusSong.CreateDefault();
        var file = new SongFile(song);
        file.Save(Path.Combine(first, "song.garbus"));
        song.Resources.Track = "missing.ogg";

        string existingDestination = Path.Combine(second, "song.garbus");
        byte[] previousBytes = { 9, 8, 7 };
        File.WriteAllBytes(existingDestination, previousBytes);

        Assert.Throws<FileNotFoundException>(() => file.Save(existingDestination));
        Assert.That(file.Directory, Is.EqualTo(first));
        Assert.That(File.ReadAllBytes(existingDestination), Is.EqualTo(previousBytes));
    }
}
