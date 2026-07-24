using System;
using Garbus.Game.Input;
using NUnit.Framework;

namespace Garbus.Game.Tests.Input
{
    [TestFixture]
    public class RadialJoystickHandlerTest
    {
        private const float threshold = 0.2f;

        [Test]
        public void RestingDriftIsZeroed()
        {
            // A drifting stick sits near centre — both axes must read exactly 0 so the framework never
            // simulates a phantom axis-button.
            var (x, y) = RadialJoystickHandler.ApplyRadialDeadzone(0.1f, 0.05f, threshold);
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(y, Is.EqualTo(0f));
        }

        [Test]
        public void VectorAboveThresholdPassesThroughUnchanged()
        {
            var (x, y) = RadialJoystickHandler.ApplyRadialDeadzone(0.5f, 0.5f, threshold);
            Assert.That(x, Is.EqualTo(0.5f));
            Assert.That(y, Is.EqualTo(0.5f));
        }

        [Test]
        public void ShallowDiagonalKeepsItsAngle()
        {
            // The whole point of radial vs per-axis: a shallow diagonal above the deadzone must NOT be
            // snapped toward the cardinal — both components survive intact.
            const float px = 0.9f;
            const float py = 0.1f; // small, but the vector magnitude (~0.905) is well above threshold.

            var (x, y) = RadialJoystickHandler.ApplyRadialDeadzone(px, py, threshold);

            Assert.That(x, Is.EqualTo(px));
            Assert.That(y, Is.EqualTo(py));
            Assert.That(MathF.Atan2(y, x), Is.EqualTo(MathF.Atan2(py, px)).Within(1e-6));
        }

        [Test]
        public void GatingIsRadialNotPerAxis()
        {
            // A large X with a tiny Y: a per-axis deadzone would zero Y (snapping to due-east); the
            // radial gate must leave Y alone because the vector magnitude clears the threshold.
            var (_, y) = RadialJoystickHandler.ApplyRadialDeadzone(0.95f, 0.05f, threshold);
            Assert.That(y, Is.EqualTo(0.05f));
        }

        [Test]
        public void JustBelowThresholdZeroed()
        {
            var (x, y) = RadialJoystickHandler.ApplyRadialDeadzone(0.1f, 0.1f, threshold); // mag ~0.141 < 0.2
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(y, Is.EqualTo(0f));
        }

        [Test]
        public void JustAboveThresholdSurvives()
        {
            var (x, y) = RadialJoystickHandler.ApplyRadialDeadzone(0.15f, 0.15f, threshold); // mag ~0.212 > 0.2
            Assert.That(x, Is.EqualTo(0.15f));
            Assert.That(y, Is.EqualTo(0.15f));
        }

        [Test]
        public void ZeroThresholdDisablesGating()
        {
            var (x, y) = RadialJoystickHandler.ApplyRadialDeadzone(0.001f, 0f, 0f);
            Assert.That(x, Is.EqualTo(0.001f));
            Assert.That(y, Is.EqualTo(0f));
        }
    }
}
