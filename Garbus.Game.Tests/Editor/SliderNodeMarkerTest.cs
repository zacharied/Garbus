using Garbus.Game.Edit.Drawables;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor;

[TestFixture]
public class SliderNodeMarkerTest
{
    [Test]
    public void ShapeOnlyMarkerIsHollowJudgedMarkerIsFilled()
    {
        var judged = new SliderNodeMarker { ShapeOnly = false };
        var shapeOnly = new SliderNodeMarker { ShapeOnly = true };

        // Relations, not absolute styling. The polyline draws beneath the markers and is wider than
        // the ring's interior, so a transparent interior would read as filled — the hollow look
        // comes from an opaque punch-out fill that must be strictly darker than the judged fill,
        // and it must stay opaque so the line cannot show through the hole.
        Assert.That(shapeOnly.FillMaxRgb, Is.LessThan(judged.FillMinRgb));
        Assert.That(shapeOnly.FillOpaque, Is.True);
        Assert.That(judged.FillOpaque, Is.True);
        Assert.That(shapeOnly.BorderThickness, Is.GreaterThan(judged.BorderThickness));
    }
}
