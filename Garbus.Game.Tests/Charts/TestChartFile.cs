// Tests for ChartFile: disk save/load roundtrip, unsaved-path guard, and resource import.
// Plain NUnit — no game host required.

using System;
using System.IO;
using Garbus.Game.Charts;
using NUnit.Framework;

namespace Garbus.Game.Tests.Charts
{
    [TestFixture]
    public class TestChartFile
    {
        private string tempDir = null!;

        [SetUp]
        public void SetUp() => tempDir = Directory.CreateTempSubdirectory("garbus-test-").FullName;

        [TearDown]
        public void TearDown() => Directory.Delete(tempDir, true);

        [Test]
        public void TestSaveLoadRoundtrip()
        {
            var chart = GarbusTestChartGenerator.GenerateChart();
            var file = new ChartFile(chart);
            Assert.That(file.FilePath, Is.Null);
            Assert.That(file.Directory, Is.Null);

            string path = Path.Combine(tempDir, "my chart.garbus");
            file.Save(path);
            Assert.That(File.Exists(path));
            Assert.That(file.Directory, Is.EqualTo(tempDir));

            var loaded = ChartFile.Load(path);
            Assert.That(loaded.Chart.HitObjects.Count, Is.EqualTo(chart.HitObjects.Count));
            Assert.That(loaded.FilePath, Is.EqualTo(path));
        }

        [Test]
        public void TestSaveWithoutPathThrows()
            => Assert.Throws<InvalidOperationException>(() => new ChartFile(new GarbusChart()).Save());

        [Test]
        public void TestImportResourceCopiesIntoDirectory()
        {
            string source = Path.Combine(tempDir, "elsewhere", "song.ogg");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllBytes(source, new byte[] { 1, 2, 3 });

            var file = new ChartFile(new GarbusChart());
            Assert.Throws<InvalidOperationException>(() => file.ImportResource(source)); // unsaved

            file.Save(Path.Combine(tempDir, "c.garbus"));
            string name = file.ImportResource(source);
            Assert.That(name, Is.EqualTo("song.ogg"));
            Assert.That(File.Exists(Path.Combine(tempDir, "song.ogg")));
        }

        [Test]
        public void TestGetAudioStreamReturnsNullWhenUnsaved()
        {
            var file = new ChartFile(new GarbusChart { Metadata = { AudioFile = "song.ogg" } });
            Assert.That(file.GetAudioStream(), Is.Null);
        }

        [Test]
        public void TestGetAudioStreamReturnsNullWhenNoAudioFile()
        {
            var file = new ChartFile(new GarbusChart());
            file.Save(Path.Combine(tempDir, "chart.garbus"));
            Assert.That(file.GetAudioStream(), Is.Null);
        }

        [Test]
        public void TestGetAudioStreamReturnsNullWhenFileMissing()
        {
            var file = new ChartFile(new GarbusChart { Metadata = { AudioFile = "missing.ogg" } });
            file.Save(Path.Combine(tempDir, "chart.garbus"));
            Assert.That(file.GetAudioStream(), Is.Null);
        }

        [Test]
        public void TestGetAudioStreamReturnsFileContents()
        {
            var file = new ChartFile(new GarbusChart { Metadata = { AudioFile = "song.ogg" } });
            file.Save(Path.Combine(tempDir, "chart.garbus"));

            byte[] audioBytes = { 10, 20, 30, 40 };
            File.WriteAllBytes(Path.Combine(tempDir, "song.ogg"), audioBytes);

            using var stream = file.GetAudioStream();
            Assert.That(stream, Is.Not.Null);

            using var ms = new MemoryStream();
            stream!.CopyTo(ms);
            Assert.That(ms.ToArray(), Is.EqualTo(audioBytes));
        }

        [Test]
        public void TestImportResourceFromSameDirectoryDoesNotThrow()
        {
            var chart = new GarbusChart();
            var file = new ChartFile(chart);
            file.Save(Path.Combine(tempDir, "chart.garbus"));

            // Create a file inside the chart directory.
            string resourcePath = Path.Combine(tempDir, "resource.wav");
            File.WriteAllBytes(resourcePath, new byte[] { 42 });

            // ImportResource should not throw even when source == dest.
            string name = file.ImportResource(resourcePath);
            Assert.That(name, Is.EqualTo("resource.wav"));
            Assert.That(File.Exists(resourcePath));
        }
    }
}
