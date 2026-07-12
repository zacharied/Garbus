using Garbus.Game.Charts;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Visual.HitObjects
{
    [TestFixture]
    public partial class TestSceneCardinalNoteStream : HitObjectStreamTestScene
    {
        private const double first_note = 2000;
        private const double spacing = 400;
        private const int count = 60;

        // Cycle through the four cardinal directions so every button gets exercised.
        private static readonly int[] angles = { 0, 90, 180, 270 };

        protected override void PopulateChart(GarbusChart chart)
        {
            for (int i = 0; i < count; i++)
            {
                chart.HitObjects.Add(new CardinalNote
                {
                    StartTime = first_note + i * spacing,
                    AngleDeg = angles[i % angles.Length],
                });
            }
        }
    }
}
