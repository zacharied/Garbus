// Interactive tuning scene for the compose-view slider node markers: a horizontal row alternating
// judged (filled) and shape-only (hollow ring) markers, with live size/thickness sliders so the two
// states stay legibly distinct at a glance. The row sits on a polyline-width strip drawn beneath the
// markers and everything shares one side-colour tint, reproducing the compose stacking — the line is
// wider than the ring's interior, so hollowness can only be judged over the line, never on an empty
// background. A second row shows the NodeDragPiece selection handles in all four states
// (unselected/selected × judged/shape-only) so the punch-out dot over the white selected fill is
// tuned in the same context. No playfield or clock is involved — unlike TestSceneSliderGlowTuning,
// there's nothing to rebuild per change: the sliders mutate the existing drawables in place.

using System.Collections.Generic;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osuTK;

namespace Garbus.Game.Tests.Tuning;

[TestFixture]
public partial class TestSceneSliderNodeMarkerTuning : GarbusTestScene
{
    private FillFlowContainer<SliderNodeMarker> row = null!;
    private FillFlowContainer<NodeDragPiece> handleRow = null!;

    // The ring-thickness step only touches the hollow (shape-only) markers, so it needs to target
    // them directly rather than infer "hollow" from a drawable property it is itself about to set.
    private SliderNodeMarker[] shapeOnlyMarkers = null!;

    [SetUp]
    public void SetUp() => Schedule(() =>
    {
        // One tint over line and markers, like SliderPolylineVisual's side colour: the punch-out
        // interior must stay visibly dark under the same tint that colours the ring and the line.
        // The handle row is untinted (blueprint handles draw outside the polyline's tint), but sits
        // on the same line strip since that's what the dot must stay legible over.
        Child = new FillFlowContainer
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 24),
            Children = new Drawable[]
            {
                new Container
                {
                    Size = new Vector2(320, 60),
                    Colour = Constants.LeftColour,
                    Children = new Drawable[]
                    {
                        // Stand-in for the SmoothPath: PathRadius 3 in the real visual = a 6px-wide
                        // line drawn beneath the markers.
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 6,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                        row = new FillFlowContainer<SliderNodeMarker>
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                            Spacing = new Vector2(30),
                        },
                    },
                },
                new Container
                {
                    Size = new Vector2(320, 60),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 6,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Colour = Constants.LeftColour,
                        },
                        handleRow = new FillFlowContainer<NodeDragPiece>
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                            Spacing = new Vector2(30),
                        },
                    },
                },
            },
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

        // All four handle states: unselected/selected × judged/shape-only.
        for (int i = 0; i < 4; i++)
        {
            handleRow.Add(new NodeDragPiece
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                NodeSelected = i >= 2,
                ShapeOnly = i % 2 == 1,
            });
        }
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

        // The dot is the handle's only Circle (the outer shell is a plain CircularContainer), so the
        // scene reaches it directly rather than adding a production-side size property for tuning.
        AddSliderStep("handle dot size", 3f, 12f, 6f, size =>
        {
            if (handleRow.IsNotNull())
            {
                foreach (var dot in handleRow.ChildrenOfType<Circle>())
                    dot.Size = new Vector2(size);
            }
        });
    }
}
