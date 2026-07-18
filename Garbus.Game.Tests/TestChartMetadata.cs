// Unit tests for ChartMetadata display helpers. Plain NUnit — no game host required.

using Garbus.Game.Charts;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class TestChartMetadata
    {
        [Test]
        public void GetDisplayedChartName_UsesChartNameWhenSet()
        {
            var meta = new ChartMetadata { ChartName = "Insane", Difficulty = Difficulty.Expert };

            Assert.That(meta.GetDisplayedChartName(), Is.EqualTo("Insane"));
        }

        [Test]
        public void GetDisplayedChartName_FallsBackToDifficultyWhenEmpty()
        {
            var meta = new ChartMetadata { ChartName = string.Empty, Difficulty = Difficulty.Advanced };

            Assert.That(meta.GetDisplayedChartName(), Is.EqualTo("Advanced"));
        }
    }
}
