using Garbus.Game.Charts;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace Garbus.Game.Tests.Visual.HitObjects
{
    [TestFixture]
    public partial class TestSceneSliderBodyStream : HitObjectStreamTestScene
    {
        private const double first_slider = 2000;
        private const double slider_duration = 2000;
        private const double gap = 500;
        private const int count = 12;

        protected override void PopulateChart(GarbusChart chart)
        {
            for (int i = 0; i < count; i++)
            {
                chart.HitObjects.Add(new SliderBody
                {
                    AngleDeg = 0,
                    StartTime = first_slider + i * (slider_duration + gap),
                    Side = i % 2 == 0 ? HorizontalDirection.Right : HorizontalDirection.Left,
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>(new[]
                        {
                            new GarbusPathControlPoint { RotationOffset = 0, TimeOffset = slider_duration / 2 },
                            new GarbusPathControlPoint { RotationOffset = 90, TimeOffset = slider_duration, SweepEasing = Easing.None },
                        }),
                    },
                });
            }
        }
    }
}
