using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Bindables;

namespace Garbus.Game.Tests.Editor;

[TestFixture]
public class InspectorShapeOnlyEligibilityTest
{
    [Test]
    public void FinalControlPointOfEachSliderIsExcluded()
    {
        var sliderA = makeSlider(100, 200, 300);
        var sliderB = makeSlider(150, 250);

        // Select every node of both sliders.
        var nodes = sliderA.Path.ControlPoints.Concat(sliderB.Path.ControlPoints).ToArray();

        var eligible = Inspector.ShapeOnlyEligible(nodes, new[] { sliderA, sliderB });

        // Each slider's final point (offsets 300 and 250) is excluded; all others remain.
        Assert.That(eligible, Is.EquivalentTo(new[]
        {
            sliderA.Path.ControlPoints[0], sliderA.Path.ControlPoints[1],
            sliderB.Path.ControlPoints[0],
        }));
    }

    private static SliderBody makeSlider(params double[] offsets)
        => new()
        {
            StartTime = 1000,
            AngleDeg = 0,
            Side = HorizontalDirection.Left,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>(
                    offsets.Select(o => new GarbusPathControlPoint { TimeOffset = o }).ToList()),
            },
        };
}
