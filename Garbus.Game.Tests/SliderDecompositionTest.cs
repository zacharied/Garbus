// Pure-logic tests for SliderDecomposition.DecomposeIntoHeads — samples a slider at a fixed time step
// and produces one head-only slider (empty path) per grid sample. Plain NUnit — no host.

using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Bindables;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class SliderDecompositionTest
    {
        private static SliderBody slider(int angleDeg, double startTime, HorizontalDirection side, params GarbusPathControlPoint[] controlPoints) => new SliderBody
        {
            StartTime = startTime,
            AngleDeg = angleDeg,
            Side = side,
            Path = new GarbusPath { ControlPoints = new BindableList<GarbusPathControlPoint>(controlPoints) },
        };

        [Test]
        public void SamplesHeadThenEveryStepUpToTheEnd()
        {
            // Head at 90°, end +60° after 300ms (linear). Step 100ms lands on 1000/1100/1200/1300 = the end.
            var s = slider(90, 1000, HorizontalDirection.Right, new GarbusPathControlPoint { TimeOffset = 300, RotationOffset = 60 });

            var heads = SliderDecomposition.DecomposeIntoHeads(s, 100);

            Assert.That(heads.Select(h => h.StartTime), Is.EqualTo(new double[] { 1000, 1100, 1200, 1300 }));
            Assert.That(heads.Select(h => h.AngleDeg), Is.EqualTo(new[] { 90, 110, 130, 150 })); // +20°/step
        }

        [Test]
        public void ProducesOnlyHeadOnlySlidersInheritingSide()
        {
            var s = slider(45, 0, HorizontalDirection.Left, new GarbusPathControlPoint { TimeOffset = 200, RotationOffset = 20 });

            var heads = SliderDecomposition.DecomposeIntoHeads(s, 100);

            Assert.That(heads, Is.Not.Empty);
            Assert.That(heads.All(h => h.Path.ControlPoints.Count == 0), Is.True);
            Assert.That(heads.All(h => h.Side == HorizontalDirection.Left), Is.True);
        }

        [Test]
        public void GridStepsOnly_EndOffGridIsNotSampled()
        {
            // End at 1250ms; step 100 lands on 1000/1100/1200 only — the off-grid end gets no head.
            var s = slider(0, 1000, HorizontalDirection.Right, new GarbusPathControlPoint { TimeOffset = 250, RotationOffset = 0 });

            var heads = SliderDecomposition.DecomposeIntoHeads(s, 100);

            Assert.That(heads.Select(h => h.StartTime), Is.EqualTo(new double[] { 1000, 1100, 1200 }));
        }

        [Test]
        public void GridAlignedEndIsIncludedDespiteFloatDrift()
        {
            // beatLength/divisor that isn't exactly representable: 500/3. The end sits on the 3rd step, which
            // must be included even though 3 * (500/3) drifts a hair from 500.
            double step = 500.0 / 3.0;
            var s = slider(0, 0, HorizontalDirection.Right, new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 0 });

            var heads = SliderDecomposition.DecomposeIntoHeads(s, step);

            Assert.That(heads.Count, Is.EqualTo(4)); // t = 0, ~166.7, ~333.3, ~500
        }

        [Test]
        public void NonPositiveStepProducesASingleHead()
        {
            var s = slider(30, 1000, HorizontalDirection.Right, new GarbusPathControlPoint { TimeOffset = 300, RotationOffset = 90 });

            Assert.That(SliderDecomposition.DecomposeIntoHeads(s, 0).Select(h => h.StartTime), Is.EqualTo(new double[] { 1000 }));
            Assert.That(SliderDecomposition.DecomposeIntoHeads(s, -5).Select(h => h.StartTime), Is.EqualTo(new double[] { 1000 }));
        }
    }
}
