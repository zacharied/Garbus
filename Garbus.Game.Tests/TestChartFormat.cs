// Chart format tests: roundtrip through the versioned JSON serializer, version gating, and agreement
// between the bundled test chart file and its source of truth (GarbusTestChartGenerator). Plain NUnit —
// no game host required.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Format;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using Garbus.Resources;
using NUnit.Framework;
using osu.Framework.IO.Stores;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class TestChartFormat
    {
        [Test]
        public void TestRoundtrip()
        {
            var original = GarbusTestChartGenerator.GenerateChart();

            var roundtripped = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(original));

            assertChartsEqual(original, roundtripped);
        }

        [Test]
        public void TestUnknownVersionRejected()
        {
            string json = GarbusChartSerializer.Encode(GarbusTestChartGenerator.GenerateChart())
                                               .Replace($"\"version\": {GarbusChartSerializer.CURRENT_VERSION}", "\"version\": 9999");

            Assert.Throws<InvalidDataException>(() => GarbusChartSerializer.Decode(json));
        }

        [Test]
        public void TestBundledChartMatchesGenerator()
        {
            var store = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(GarbusResources).Assembly), @"Charts");

            using var stream = store.GetStream(@"test-chart.garbus");
            Assert.That(stream, Is.Not.Null, "bundled test chart missing — run RegenerateBundledTestChart");

            var song = GarbusSongSerializer.Decode(stream).Song;
            Assert.Multiple(() =>
            {
                Assert.That(song.SongId, Is.EqualTo(GarbusTestChartGenerator.TestSongId));
                Assert.That(song.Charts.Single().ChartId, Is.EqualTo(GarbusTestChartGenerator.TestChartId));
            });
            var actual = song.Charts.Single();
            actual.ControlPointInfo = song.ControlPointInfo;
            actual.Metadata.Title = song.Metadata.Title;
            actual.Metadata.Artist = song.Metadata.Artist;
            actual.Metadata.AudioFile = song.Resources.Track;
            assertChartsEqual(GarbusTestChartGenerator.GenerateChart(), actual);
        }

        /// <summary>
        /// Dev utility: rewrites Garbus.Resources/Charts/test-chart.garbus from the generator.
        /// Run explicitly (e.g. dotnet test --filter Name~RegenerateBundledTestChart) after changing
        /// the generator or the chart format.
        /// </summary>
        [Test]
        [Explicit]
        public void RegenerateBundledTestChart()
        {
            // Walk up from the test assembly to the repo root (identified by the resources project).
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Garbus.Resources")))
                dir = dir.Parent;

            Assert.That(dir, Is.Not.Null, "could not locate repo root from test assembly location");

            string chartsDir = Path.Combine(dir!.FullName, "Garbus.Resources", "Charts");
            Directory.CreateDirectory(chartsDir);

            string path = Path.Combine(chartsDir, "test-chart.garbus");
            string json = GarbusSongSerializer.Encode(GarbusTestChartGenerator.GenerateSong()).Replace("\r\n", "\n");
            File.WriteAllText(path, json);

            TestContext.Out.WriteLine($"wrote {path}");
        }

        [Test]
        public void TestNewFieldsRoundtrip()
        {
            var chart = new GarbusChart
            {
                Metadata = new ChartMetadata
                {
                    Title = "T", RomanisedTitle = "T-rom",
                    Artist = "A", RomanisedArtist = "A-rom",
                    Charter = "C", ChartName = "N",
                    Source = "some game", Tags = "tag1 tag2",
                    AudioFile = "track.ogg", BackgroundFile = "bg.png",
                    Difficulty = Difficulty.Expert,
                },
                PreviewTime = 12345.0,
            };

            var decoded = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(chart));

            Assert.That(decoded.Metadata.RomanisedTitle, Is.EqualTo("T-rom"));
            Assert.That(decoded.Metadata.RomanisedArtist, Is.EqualTo("A-rom"));
            Assert.That(decoded.Metadata.Source, Is.EqualTo("some game"));
            Assert.That(decoded.Metadata.Tags, Is.EqualTo("tag1 tag2"));
            Assert.That(decoded.Metadata.BackgroundFile, Is.EqualTo("bg.png"));
            Assert.That(decoded.Metadata.Difficulty, Is.EqualTo(Difficulty.Expert));
            Assert.That(decoded.PreviewTime, Is.EqualTo(12345.0));
        }

        [Test]
        public void ShoulderHoldNoteRoundtrips()
        {
            var chart = new GarbusChart
            {
                HitObjects = new List<GarbusHitObject>
                {
                    new ShoulderHoldNote { StartTime = 1000, Duration = 750, Side = HorizontalDirection.Right },
                },
            };

            var decoded = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(chart));

            var hold = (ShoulderHoldNote)decoded.HitObjects.Single();
            Assert.That(hold.StartTime, Is.EqualTo(1000));
            Assert.That(hold.Duration, Is.EqualTo(750));
            Assert.That(hold.Side, Is.EqualTo(HorizontalDirection.Right));
        }

        [Test]
        public void TestDesignPointsRoundtrip()
        {
            var chart = new GarbusChart();
            chart.DesignPointInfo.Add(new TutorialMessage { StartTime = 1000, EndTime = 3000, Text = "Welcome!" });
            chart.DesignPointInfo.Add(new TutorialMessage { StartTime = 5000, EndTime = 6000, Text = "Press the buttons" });

            var decoded = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(chart));

            Assert.That(decoded.DesignPointInfo.DesignPoints, Has.Count.EqualTo(2));
            var first = (TutorialMessage)decoded.DesignPointInfo.DesignPoints[0];
            Assert.That(first.StartTime, Is.EqualTo(1000));
            Assert.That(first.EndTime, Is.EqualTo(3000));
            Assert.That(first.Text, Is.EqualTo("Welcome!"));
            var second = (TutorialMessage)decoded.DesignPointInfo.DesignPoints[1];
            Assert.That(second.Text, Is.EqualTo("Press the buttons"));
        }

        [Test]
        public void TestChartWithoutDesignPointsDecodesEmpty()
        {
            var chart = new GarbusChart();

            var decoded = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(chart));

            Assert.That(decoded.DesignPointInfo.DesignPoints, Is.Empty);
        }

        private static void assertChartsEqual(GarbusChart expected, GarbusChart actual)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.Metadata.Title, Is.EqualTo(expected.Metadata.Title));
                Assert.That(actual.Metadata.Artist, Is.EqualTo(expected.Metadata.Artist));
                Assert.That(actual.Metadata.Charter, Is.EqualTo(expected.Metadata.Charter));
                Assert.That(actual.Metadata.ChartName, Is.EqualTo(expected.Metadata.ChartName));
                Assert.That(actual.Metadata.AudioFile, Is.EqualTo(expected.Metadata.AudioFile));

                Assert.That(actual.ControlPointInfo.TimingPoints, Has.Count.EqualTo(expected.ControlPointInfo.TimingPoints.Count));

                foreach (var (e, a) in expected.ControlPointInfo.TimingPoints.Zip(actual.ControlPointInfo.TimingPoints))
                {
                    Assert.That(a.Time, Is.EqualTo(e.Time));
                    Assert.That(a.BeatLength, Is.EqualTo(e.BeatLength));
                    Assert.That(a.TimeSignature, Is.EqualTo(e.TimeSignature));
                    Assert.That(a.OmitFirstBarLine, Is.EqualTo(e.OmitFirstBarLine));
                }

                Assert.That(actual.HitObjects, Has.Count.EqualTo(expected.HitObjects.Count));

                foreach (var (e, a) in expected.HitObjects.Zip(actual.HitObjects))
                    assertHitObjectsEqual(e, a);
            });
        }

        private static void assertHitObjectsEqual(GarbusHitObject expected, GarbusHitObject actual)
        {
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()));
            Assert.That(actual.StartTime, Is.EqualTo(expected.StartTime));

            switch (expected)
            {
                case CardinalNote e:
                    Assert.That(((CardinalNote)actual).AngleDeg, Is.EqualTo(e.AngleDeg));
                    break;

                case CardinalHoldNote e:
                    var actualHold = (CardinalHoldNote)actual;
                    Assert.That(actualHold.AngleDeg, Is.EqualTo(e.AngleDeg));
                    Assert.That(actualHold.Duration, Is.EqualTo(e.Duration));
                    break;

                case ShoulderNote e:
                    Assert.That(((ShoulderNote)actual).Side, Is.EqualTo(e.Side));
                    break;

                case ShoulderHoldNote e:
                    var actualShoulderHold = (ShoulderHoldNote)actual;
                    Assert.That(actualShoulderHold.Side, Is.EqualTo(e.Side));
                    Assert.That(actualShoulderHold.Duration, Is.EqualTo(e.Duration));
                    break;

                case SliderBody e:
                    var actualSlider = (SliderBody)actual;
                    Assert.That(actualSlider.AngleDeg, Is.EqualTo(e.AngleDeg));
                    Assert.That(actualSlider.Side, Is.EqualTo(e.Side));
                    Assert.That(actualSlider.Path.ControlPoints, Has.Count.EqualTo(e.Path.ControlPoints.Count));

                    foreach (var (ec, ac) in e.Path.ControlPoints.Zip(actualSlider.Path.ControlPoints))
                    {
                        Assert.That(ac.TimeOffset, Is.EqualTo(ec.TimeOffset));
                        Assert.That(ac.RotationOffset, Is.EqualTo(ec.RotationOffset));
                        Assert.That(ac.Smooth, Is.EqualTo(ec.Smooth));
                        Assert.That(ac.SweepEasing, Is.EqualTo(ec.SweepEasing));
                    }

                    break;

                case GarbusSlamCentered e:
                    Assert.That(((GarbusSlamCentered)actual).AngleDeg, Is.EqualTo(e.AngleDeg));
                    Assert.That(((GarbusSlamCentered)actual).Side, Is.EqualTo(e.Side));
                    break;

                case GarbusSlamEdge e:
                    var actualSlam = (GarbusSlamEdge)actual;
                    Assert.That(actualSlam.AngleDeg, Is.EqualTo(e.AngleDeg));
                    Assert.That(actualSlam.Side, Is.EqualTo(e.Side));
                    Assert.That(actualSlam.Direction, Is.EqualTo(e.Direction));
                    break;

                default:
                    Assert.Fail($"unhandled hit object type {expected.GetType().Name}");
                    break;
            }
        }
    }
}
