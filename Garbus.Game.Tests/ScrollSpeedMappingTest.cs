using Garbus.Game.Settings;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class ScrollSpeedMappingTest
    {
        [Test]
        public void SpeedTenReproducesDefaultTimeRange()
        {
            Assert.That(ScrollSpeedMapping.ToTimeRange(10), Is.EqualTo(700).Within(0.001));
        }

        [Test]
        public void HigherSpeedGivesShorterTimeRange()
        {
            Assert.That(ScrollSpeedMapping.ToTimeRange(20), Is.LessThan(ScrollSpeedMapping.ToTimeRange(10)));
            Assert.That(ScrollSpeedMapping.ToTimeRange(1), Is.GreaterThan(ScrollSpeedMapping.ToTimeRange(10)));
        }
    }
}
