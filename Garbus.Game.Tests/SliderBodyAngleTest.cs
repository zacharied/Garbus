// Pure-math tests for SliderBody.AngleDegAt — the model-side angle sampler that decomposition (and any
// non-drawable caller) uses to read the swept angle at a time. Shares SliderSweep with the gameplay body
// and editor polyline, so these pin that the model agrees with the rendered geometry. Plain NUnit — no host.

using Garbus.Game.Core;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Utils;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class SliderBodyAngleTest
    {
        private static SliderBody slider(int angleDeg, double startTime, params GarbusPathControlPoint[] controlPoints) => new SliderBody
        {
            StartTime = startTime,
            AngleDeg = angleDeg,
            Side = HorizontalDirection.Right,
            Path = new GarbusPath { ControlPoints = new BindableList<GarbusPathControlPoint>(controlPoints) },
        };

        [Test]
        public void HeadOnlySliderHasConstantAngle()
        {
            var s = slider(90, 1000);

            Assert.That(s.AngleDegAt(500), Is.EqualTo(90f).Within(1e-4));
            Assert.That(s.AngleDegAt(1000), Is.EqualTo(90f).Within(1e-4));
            Assert.That(s.AngleDegAt(9999), Is.EqualTo(90f).Within(1e-4));
        }

        [Test]
        public void LinearLinkInterpolatesAbsoluteAngle()
        {
            // Head at 90°, one node +40° after 200ms: absolute end angle 130°.
            var s = slider(90, 1000, new GarbusPathControlPoint { TimeOffset = 200, RotationOffset = 40 });

            Assert.That(s.AngleDegAt(1000), Is.EqualTo(90f).Within(1e-4));
            Assert.That(s.AngleDegAt(1100), Is.EqualTo(110f).Within(1e-4)); // linear midpoint
            Assert.That(s.AngleDegAt(1200), Is.EqualTo(130f).Within(1e-4));
        }

        [Test]
        public void TimesOutsideThePathClampToTheEndNodes()
        {
            var s = slider(90, 1000, new GarbusPathControlPoint { TimeOffset = 200, RotationOffset = 40 });

            Assert.That(s.AngleDegAt(0), Is.EqualTo(90f).Within(1e-4));    // before head
            Assert.That(s.AngleDegAt(5000), Is.EqualTo(130f).Within(1e-4)); // after last node
        }

        [Test]
        public void EasingReshapesTheProgress()
        {
            var s = slider(0, 0, new GarbusPathControlPoint { TimeOffset = 100, RotationOffset = 100, SweepEasing = Easing.InQuint });

            // Angle offset eases (InQuint); at the time-midpoint the value is far from the linear 50°.
            float expected = (float)(100.0 * Interpolation.ApplyEasing(Easing.InQuint, 0.5));
            Assert.That(s.AngleDegAt(50), Is.EqualTo(expected).Within(1e-3));
            Assert.That(s.AngleDegAt(50), Is.Not.EqualTo(50f).Within(1f));
        }
    }
}
