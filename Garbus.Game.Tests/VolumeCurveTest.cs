using Garbus.Game.Settings;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class VolumeCurveTest
    {
        [Test]
        public void PositionThirtyPercentMapsToThreePercentGain()
        {
            // The calibration anchor: slider at 30% should output ~3% actual gain.
            Assert.That(VolumeCurve.ToGain(0.30), Is.EqualTo(0.03).Within(0.005));
        }

        [Test]
        public void EndpointsPreserved()
        {
            Assert.That(VolumeCurve.ToGain(0), Is.EqualTo(0));
            Assert.That(VolumeCurve.ToGain(1), Is.EqualTo(1));
            Assert.That(VolumeCurve.ToPosition(0), Is.EqualTo(0));
            Assert.That(VolumeCurve.ToPosition(1), Is.EqualTo(1));
        }

        [Test]
        public void RoundTripsWithinTolerance()
        {
            foreach (double pos in new[] { 0.1, 0.25, 0.5, 0.75, 0.9 })
                Assert.That(VolumeCurve.ToPosition(VolumeCurve.ToGain(pos)), Is.EqualTo(pos).Within(1e-9));
        }

        [Test]
        public void GainIsQuieterThanLinearInUsableRange()
        {
            // The whole point: at a given slider position, actual gain sits below the linear value.
            Assert.That(VolumeCurve.ToGain(0.5), Is.LessThan(0.5));
        }

        [Test]
        public void NonPositiveAndTinyInputsStayFiniteAndNonNegative()
        {
            // Guards against Math.Pow returning NaN for a negative base with a fractional exponent.
            Assert.That(VolumeCurve.ToGain(-0.1), Is.EqualTo(0));
            Assert.That(VolumeCurve.ToPosition(-0.1), Is.EqualTo(0));

            Assert.That(double.IsNaN(VolumeCurve.ToGain(1e-12)), Is.False);
            Assert.That(VolumeCurve.ToGain(1e-12), Is.GreaterThanOrEqualTo(0));
        }
    }
}
