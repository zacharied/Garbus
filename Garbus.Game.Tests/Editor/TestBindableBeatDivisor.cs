// Tests for BindableBeatDivisor: preset cycling, arbitrary divisor.
// Plain NUnit — no game host required.

using Garbus.Game.Edit;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestBindableBeatDivisor
    {
        [Test]
        public void TestPresetCycle()
        {
            // COMMON presets are 1, 2, 4, 8, 16 — cycling from 4 lands exactly on its neighbours.
            var divisor = new BindableBeatDivisor(4);
            divisor.SelectNext();
            Assert.That(divisor.Value, Is.EqualTo(8));
            divisor.SelectPrevious();
            Assert.That(divisor.Value, Is.EqualTo(4));
        }

        [Test]
        public void TestArbitraryDivisor()
        {
            var divisor = new BindableBeatDivisor();
            divisor.SetArbitraryDivisor(5);
            Assert.That(divisor.Value, Is.EqualTo(5));
        }
    }
}
