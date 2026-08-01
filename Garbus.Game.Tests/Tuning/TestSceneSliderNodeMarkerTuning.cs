// Interactive tuning scene for the compose-view slider node markers: a horizontal row alternating
// judged (filled) and shape-only (hollow ring) markers, with live size/thickness sliders so the two
// states stay legibly distinct at a glance. No playfield or clock is involved — the markers render
// their own geometry from ShapeOnly alone — so, unlike TestSceneSliderGlowTuning, there's nothing to
// rebuild per change: the sliders mutate the existing drawables in place.

using System.Collections.Generic;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace Garbus.Game.Tests.Tuning;

[TestFixture]
public partial class TestSceneSliderNodeMarkerTuning : GarbusTestScene
{
    private FillFlowContainer<SliderNodeMarker> row = null!;

    // The ring-thickness step only touches the hollow (shape-only) markers, so it needs to target
    // them directly rather than infer "hollow" from a drawable property it is itself about to set.
    private SliderNodeMarker[] shapeOnlyMarkers = null!;

    [SetUp]
    public void SetUp() => Schedule(() =>
    {
        Child = row = new FillFlowContainer<SliderNodeMarker>
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            AutoSizeAxes = Axes.Both,
            Spacing = new Vector2(30),
        };

        var shapeOnlyMarkerList = new List<SliderNodeMarker>();

        for (int i = 0; i < 6; i++)
        {
            bool isShapeOnly = i % 2 == 1;
            var marker = new SliderNodeMarker { ShapeOnly = isShapeOnly, Anchor = Anchor.CentreLeft, Origin = Anchor.Centre };
            row.Add(marker);

            if (isShapeOnly)
                shapeOnlyMarkerList.Add(marker);
        }

        shapeOnlyMarkers = shapeOnlyMarkerList.ToArray();
    });

    [Test]
    public void TuneMarkers()
    {
        AddSliderStep("marker size", 4f, 24f, 10f, size =>
        {
            if (row.IsNotNull())
                foreach (var m in row) m.Size = new Vector2(size);
        });

        AddSliderStep("ring thickness", 1f, 6f, 2.5f, thickness =>
        {
            if (row.IsNotNull())
                foreach (var m in shapeOnlyMarkers) m.BorderThickness = thickness;
        });
    }
}
