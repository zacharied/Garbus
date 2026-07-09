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
            var divisor = new BindableBeatDivisor(4);
            divisor.SelectNext();      // moves within the current preset collection
            Assert.That(divisor.Value, Is.Not.EqualTo(4));
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
