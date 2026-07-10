using System.Linq;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Gameplay.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Charts
{
    [TestFixture]
    public class TestBarLineGenerator
    {
        private static ControlPointInfo cpi(params (double time, double beatLength, int numerator, bool omitFirst)[] points)
        {
            var info = new ControlPointInfo();
            foreach (var (time, beatLength, numerator, omitFirst) in points)
            {
                info.Add(time, new TimingControlPoint
                {
                    BeatLength = beatLength,
                    TimeSignature = new TimeSignature(numerator),
                    OmitFirstBarLine = omitFirst,
                });
            }
            return info;
        }

        [Test]
        public void TestSingleQuadrupleSection()
        {
            // beatLength 500, 4/4 => barLength 2000. endTime 8000 is exclusive.
            var lines = BarLineGenerator.Generate(cpi((0, 500, 4, false)), 8000);

            Assert.That(lines.Select(l => l.StartTime), Is.EqualTo(new double[] { 0, 2000, 4000, 6000 }));
            Assert.That(lines.Select(l => l.MeasureIndex), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void TestNumeratorChangeAcrossTwoSections()
        {
            // Section A: 3/4 @500 => barLength 1500, runs [0,3000). Section B: 4/4 @500 => 2000, runs [3000,7000).
            var lines = BarLineGenerator.Generate(cpi((0, 500, 3, false), (3000, 500, 4, false)), 7000);

            Assert.That(lines.Select(l => l.StartTime), Is.EqualTo(new double[] { 0, 1500, 3000, 5000 }));
            Assert.That(lines.Select(l => l.MeasureIndex), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void TestOmitFirstBarLineSkipsSectionStart()
        {
            // barLength 2000, omit first => start at 2000.
            var lines = BarLineGenerator.Generate(cpi((0, 500, 4, true)), 8000);

            Assert.That(lines.Select(l => l.StartTime), Is.EqualTo(new double[] { 2000, 4000, 6000 }));
            Assert.That(lines.Select(l => l.MeasureIndex), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void TestNoTimingPointsYieldsEmpty()
        {
            Assert.That(BarLineGenerator.Generate(new ControlPointInfo(), 8000), Is.Empty);
        }
    }
}
