using Garbus.Game.Charts;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Visual.HitObjects
{
    [TestFixture]
    public partial class TestSceneShoulderNoteStream : HitObjectStreamTestScene
    {
        private const double first_note = 2000;
        private const double spacing = 500;
        private const int count = 40;

        protected override void PopulateChart(GarbusChart chart)
        {
            for (int i = 0; i < count; i++)
            {
                chart.HitObjects.Add(new ShoulderNote
                {
                    StartTime = first_note + i * spacing,
                    Side = i % 2 == 0 ? HorizontalDirection.Left : HorizontalDirection.Right,
                });
            }
        }
    }
}
