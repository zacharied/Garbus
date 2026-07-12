// Pure-math tests for the shared slider sweep evaluation used by both the gameplay body
// (DrawableSliderBody) and the editor polyline (SliderPolylineVisual). Plain NUnit — no game host.

using System.Collections.Generic;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Utils;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class SliderSweepTest
    {
        // Two-node path: values 10 -> 50 over times 0 -> 100.
        private static readonly float[] two_values = { 10f, 50f };
        private static readonly double[] two_times = { 0.0, 100.0 };

        [Test]
        public void EndpointsPreservedLinear()
        {
            var slopes = SliderSweep.ComputeSlopes(two_values, two_times);

            Assert.That(SliderSweep.ValueAt(two_values, slopes, two_times, Easing.None, false, 0, 0f), Is.EqualTo(10f).Within(1e-4));
            Assert.That(SliderSweep.ValueAt(two_values, slopes, two_times, Easing.None, false, 0, 1f), Is.EqualTo(50f).Within(1e-4));
        }

        [Test]
        public void EndpointsPreservedSmooth()
        {
            var slopes = SliderSweep.ComputeSlopes(two_values, two_times);

            Assert.That(SliderSweep.ValueAt(two_values, slopes, two_times, Easing.None, true, 0, 0f), Is.EqualTo(10f).Within(1e-4));
            Assert.That(SliderSweep.ValueAt(two_values, slopes, two_times, Easing.None, true, 0, 1f), Is.EqualTo(50f).Within(1e-4));
        }

        [Test]
        public void LinearParity()
        {
            var slopes = SliderSweep.ComputeSlopes(two_values, two_times);

            // No easing, not smooth => plain lerp: 10 + (50-10)*0.25 = 20.
            float actual = SliderSweep.ValueAt(two_values, slopes, two_times, Easing.None, false, 0, 0.25f);
            Assert.That(actual, Is.EqualTo(20f).Within(1e-4));
        }

        [Test]
        public void EasingIsApplied()
        {
            float[] values = { 0f, 100f };
            double[] times = { 0.0, 100.0 };
            var slopes = SliderSweep.ComputeSlopes(values, times);

            // InQuint at t=0.5 => 100 * ApplyEasing(InQuint, 0.5), which is far from the linear midpoint 50.
            float expected = (float)(100.0 * Interpolation.ApplyEasing(Easing.InQuint, 0.5));
            float actual = SliderSweep.ValueAt(values, slopes, times, Easing.InQuint, false, 0, 0.5f);

            Assert.That(actual, Is.EqualTo(expected).Within(1e-4));
            Assert.That(actual, Is.Not.EqualTo(50f).Within(1f));
        }

        [Test]
        public void SmoothHermiteMatchesGoldenValue()
        {
            // 3 nodes with curvature so Hermite differs from linear.
            float[] values = { 0f, 0f, 90f };
            double[] times = { 0.0, 100.0, 300.0 };
            var slopes = SliderSweep.ComputeSlopes(values, times);

            // Link 1 (node1 -> node2), smooth, t=0.5. Hand-computed cubic Hermite = 41.25
            // (linear midpoint would be 45), pinning the exact gameplay formula.
            float actual = SliderSweep.ValueAt(values, slopes, times, Easing.None, true, 1, 0.5f);
            Assert.That(actual, Is.EqualTo(41.25f).Within(1e-3));
        }
    }
}
