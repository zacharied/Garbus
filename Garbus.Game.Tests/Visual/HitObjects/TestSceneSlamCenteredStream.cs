using Garbus.Game.Charts;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Visual.HitObjects
{
    [TestFixture]
    public partial class TestSceneSlamCenteredStream : HitObjectStreamTestScene
    {
        private const double first_slam = 2000;
        private const double spacing = 700;
        private const int count = 24;

        private static readonly int[] angles = { 45, 135, 225, 315 };

        protected override void PopulateChart(GarbusChart chart)
        {
            for (int i = 0; i < count; i++)
            {
                chart.HitObjects.Add(new GarbusSlamCentered
                {
                    StartTime = first_slam + i * spacing,
                    AngleDeg = angles[i % angles.Length],
                    Side = i % 2 == 0 ? HorizontalDirection.Left : HorizontalDirection.Right,
                });
            }
        }
    }
}
