// Chart format tests: roundtrip through the versioned JSON serializer, version gating, and agreement
// between the bundled test chart file and its source of truth (GarbusTestChartGenerator). Plain NUnit —
// no game host required.

using System;
using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
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

            assertChartsEqual(GarbusTestChartGenerator.GenerateChart(), GarbusChartSerializer.Decode(stream));
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
            File.WriteAllText(path, GarbusChartSerializer.Encode(GarbusTestChartGenerator.GenerateChart()));

            TestContext.Out.WriteLine($"wrote {path}");
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

                case HoldNote e:
                    var actualHold = (HoldNote)actual;
                    Assert.That(actualHold.AngleDeg, Is.EqualTo(e.AngleDeg));
                    Assert.That(actualHold.Duration, Is.EqualTo(e.Duration));
                    break;

                case ShoulderNote e:
                    Assert.That(((ShoulderNote)actual).Side, Is.EqualTo(e.Side));
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
