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

        // Relation, not absolute styling: the judged marker's fill is visible and the shape-only
        // marker's is not, while only the shape-only marker carries a border ring.
        Assert.That(judged.FillVisible, Is.True);
        Assert.That(shapeOnly.FillVisible, Is.False);
        Assert.That(shapeOnly.BorderThickness, Is.GreaterThan(judged.BorderThickness));
    }
}
