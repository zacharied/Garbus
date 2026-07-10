using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using System;
using System.Collections.Generic;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneEditorBarLines : GarbusTestScene
    {
        private Harness harness = null!;
        private EditorChart editorChart = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            editorChart = new EditorChart(chart);
            Child = harness = new Harness(editorChart) { RelativeSizeAxes = Axes.Both };
        });

        [Test]
        public void TestBarLinesGeneratedForWholeTrack()
        {
            AddUntilStep("composer loaded", () => harness.Composer.IsLoaded);

            AddAssert("display generated 30 barlines", () =>
            {
                var display = harness.Composer.Playfield.ChildrenOfType<EditorBarLineDisplay>().Single();
                return display.BarLines.Count == 30
                       && display.BarLines.Count == BarLineGenerator.Generate(editorChart.ControlPointInfo, 60000).Count;
            });

            AddUntilStep("at least one DrawableBarLine visible", () =>
                harness.Composer.Playfield.ChildrenOfType<EditorBarLineDisplay>().Single()
                    .ChildrenOfType<Drawable>().Any(d => d.GetType().Name == "DrawableBarLine"));
        }

        [Test]
        public void TestRegeneratesWhenTimingChanges()
        {
            AddUntilStep("composer loaded", () => harness.Composer.IsLoaded);

            AddStep("add a second timing point halving the bar length", () =>
                editorChart.ControlPointInfo.Add(30000, new TimingControlPoint { BeatLength = 250 }));

            AddAssert("barline count reflects both sections", () =>
            {
                var display = harness.Composer.Playfield.ChildrenOfType<EditorBarLineDisplay>().Single();
                int expected = BarLineGenerator.Generate(editorChart.ControlPointInfo, 60000).Count;
                return display.BarLines.Count == expected && expected > 30;
            });
        }

        private partial class Harness : Container
        {
            private readonly EditorChart editorChart;
            private DependencyContainer dependencies = null!;
            public EditorBarLineComposer Composer { get; private set; } = null!;

            public Harness(EditorChart editorChart) => this.editorChart = editorChart;

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
                Child = Composer = new EditorBarLineComposer { RelativeSizeAxes = Axes.Both };
                AddInternal(dependencies.Get<EditorClock>());
            }
        }

        private partial class EditorBarLineComposer : ScrollingHitObjectComposer<GarbusHitObject>
        {
            protected override IReadOnlyList<CompositionTool> CompositionTools => Array.Empty<CompositionTool>();
            protected override Playfield CreatePlayfield() => new GarbusEditorPlayfield();
            protected override DrawableHitObject? CreateDrawableRepresentation(GarbusHitObject hitObject) => null;
            protected override ComposeBlueprintContainer CreateBlueprintContainer() => new MinimalBlueprintContainer(this);
        }

        private partial class MinimalBlueprintContainer : ComposeBlueprintContainer
        {
            public MinimalBlueprintContainer(HitObjectComposer composer) : base(composer) { }
            public override HitObjectSelectionBlueprint? CreateHitObjectBlueprintFor(GarbusHitObject hitObject) => null;
            protected override bool TryMoveBlueprints(
                osu.Framework.Input.Events.DragEvent e,
                IList<(SelectionBlueprint<GarbusHitObject> blueprint, osuTK.Vector2[] originalSnapPositions)> blueprints) => false;
        }
    }
}
