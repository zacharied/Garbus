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

        [Test]
        public void FormatSpeedShowsOneDecimal()
        {
            Assert.That(ScrollSpeedMapping.FormatSpeed(4), Is.EqualTo("4.0"));
            Assert.That(ScrollSpeedMapping.FormatSpeed(12.5), Is.EqualTo("12.5"));
            Assert.That(ScrollSpeedMapping.FormatSpeed(10), Is.EqualTo("10.0"));
        }
    }
}
