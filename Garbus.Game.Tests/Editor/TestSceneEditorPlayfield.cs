// Tests for GarbusEditorPlayfield: verifies that the playfield hosts EditorDrawable* types
// correctly when CardinalNotes are added via EditorChart, and that the x-fraction matches
// EditorAngleMapping.ToX(). Also asserts SliderPolylineVisual wraps seam-crossing sliders.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneEditorPlayfield : GarbusTestScene
    {
        private EditorPlayfieldHarness harness = null!;
        private EditorChart editorChart = null!;

        [SetUp]
        public new void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            editorChart = new EditorChart(chart);

            Child = harness = new EditorPlayfieldHarness(editorChart) { RelativeSizeAxes = Axes.Both };
        });

        [Test]
        public void TestCardinalNoteDrawableAppearsAtCorrectXFraction()
        {
            const float angle = 90f;

            AddUntilStep("composer loaded", () => harness.Composer.IsLoaded);

            CardinalNote? note = null;
            AddStep("add cardinal note at angle 90 / t=1000", () =>
            {
                note = new CardinalNote { AngleDeg = (int)angle, StartTime = 1000 };
                editorChart.Add(note);
            });

            AddUntilStep("EditorDrawableCardinalNote appears", () =>
                harness.Composer.Playfield.ChildrenOfType<EditorDrawableCardinalNote>().Any());

            AddAssert("x fraction matches ToX(90)", () =>
            {
                var drawable = harness.Composer.Playfield.ChildrenOfType<EditorDrawableCardinalNote>().FirstOrDefault();
                if (drawable == null) return false;

                // Drawable.X is relative (RelativePositionAxes = Axes.X), so its value IS the x-fraction.
                float expected = EditorAngleMapping.ToX(angle);
                return Math.Abs(drawable.X - expected) < 0.005f;
            });
        }

        [Test]
        public void TestSliderPolylineVisualRendersWrapCopiesForSeamCrossingSlider()
        {
            AddUntilStep("composer loaded", () => harness.Composer.IsLoaded);

            // A slider with AngleDeg = 135 (the seam) and a control point reaching well into the ghost
            // band creates a range that crosses 0 grid-degrees → VisibleWrapCopies yields [0, 1] (or [-1, 0]).
            AddStep("add seam-crossing slider", () =>
            {
                var slider = new SliderBody
                {
                    AngleDeg = 130, // near left seam
                    Side = Core.HorizontalDirection.Left,
                    StartTime = 2000,
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>
                        {
                            // push the path 40° past the left seam (into the left ghost band)
                            new GarbusPathControlPoint { RotationOffset = -40, TimeOffset = 500 },
                        },
                    },
                };
                editorChart.Add(slider);
            });

            AddUntilStep("SliderPolylineVisual appears", () =>
                harness.Composer.Playfield.ChildrenOfType<SliderPolylineVisual>().Any());

            // Give the Update() loop a frame to build the wrap copies.
            // The slider (AngleDeg=130, bodyGridDeg=355, offset=-40) spans grid range [315, 355].
            // VisibleWrapCopies(315, 355) → k ∈ {0, 1} → exactly 2 active copies.
            // Active copies have their SmoothPath populated (ClearGeometry calls ClearVertices on idle pool slots).
            AddUntilStep("exactly 2 active SmoothPath wrap copies", () =>
                harness.Composer.Playfield.ChildrenOfType<SmoothPath>().Count(p => p.Vertices.Count > 0) == 2);
        }

        // ---------------------------------------------------------------------------
        // Concrete composer that hosts a GarbusEditorPlayfield and creates the correct
        // EditorDrawable* types for each GarbusHitObject subtype.
        // ---------------------------------------------------------------------------

        private partial class EditorPlayfieldComposer : ScrollingHitObjectComposer<GarbusHitObject>
        {
            protected override IReadOnlyList<CompositionTool> CompositionTools => System.Array.Empty<CompositionTool>();

            protected override Playfield CreatePlayfield() => new GarbusEditorPlayfield();

            protected override DrawableHitObject? CreateDrawableRepresentation(GarbusHitObject hitObject) =>
                hitObject switch
                {
                    CardinalNote cn => new EditorDrawableCardinalNote(cn),
                    HoldNote hn => new EditorDrawableHoldNote(hn),
                    ShoulderNote sn => new EditorDrawableShoulderNote(sn),
                    GarbusSlamCentered sc => new EditorDrawableGarbusSlamCentered(sc),
                    GarbusSlamEdge se => new EditorDrawableGarbusSlamEdge(se),
                    SliderBody sb => new EditorDrawableSliderBody(sb),
                    _ => null,
                };

            protected override ComposeBlueprintContainer CreateBlueprintContainer() =>
                new MinimalBlueprintContainer(this);
        }

        private partial class MinimalBlueprintContainer : ComposeBlueprintContainer
        {
            public MinimalBlueprintContainer(HitObjectComposer composer) : base(composer) { }

            public override HitObjectSelectionBlueprint? CreateHitObjectBlueprintFor(GarbusHitObject hitObject) => null;

            protected override bool TryMoveBlueprints(
                osu.Framework.Input.Events.DragEvent e,
                IList<(SelectionBlueprint<GarbusHitObject> blueprint, osuTK.Vector2[] originalSnapPositions)> blueprints) => false;
        }

        // ---------------------------------------------------------------------------
        // Harness
        // ---------------------------------------------------------------------------

        private partial class EditorPlayfieldHarness : Container
        {
            private readonly EditorChart editorChart;
            private DependencyContainer dependencies = null!;

            public EditorPlayfieldComposer Composer { get; private set; } = null!;

            public EditorPlayfieldHarness(EditorChart editorChart)
            {
                this.editorChart = editorChart;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                var beatDivisor = new BindableBeatDivisor(4);
                var editorClock = new EditorClock(editorChart.ControlPointInfo, 60000, beatDivisor);
                editorClock.ChangeSource(new TrackVirtual(60000));

                dependencies.Cache(editorChart);
                dependencies.Cache(editorClock);
                dependencies.Cache(beatDivisor);

                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = Composer = new EditorPlayfieldComposer { RelativeSizeAxes = Axes.Both };
                AddInternal(dependencies.Get<EditorClock>());
            }
        }
    }
}
