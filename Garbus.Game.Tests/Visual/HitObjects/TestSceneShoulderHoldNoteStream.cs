using Garbus.Game.Charts;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Visual.HitObjects
{
    [TestFixture]
    public partial class TestSceneShoulderHoldNoteStream : HitObjectStreamTestScene
    {
        private const double first_note = 2000;
        private const double duration = 1000;
        private const double gap = 1000;
        private const int count = 20;

        protected override void PopulateChart(GarbusChart chart)
        {
            for (int i = 0; i < count; i++)
            {
                chart.HitObjects.Add(new ShoulderHoldNote
                {
                    StartTime = first_note + i * (duration + gap),
                    Duration = duration,
                    Side = i % 2 == 0 ? HorizontalDirection.Left : HorizontalDirection.Right,
                });
            }
        }
    }
}
