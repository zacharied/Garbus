using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneChordHighlightEditor : GarbusTestScene
    {
        private ChordEditorHarness harness = null!;
        private EditorChart editorChart = null!;

        private GarbusEditorPlayfield playfield => harness.Composer.Playfield;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo!.Add(0, new TimingControlPoint { BeatLength = 500 });
            editorChart = new EditorChart(chart);
            Child = harness = new ChordEditorHarness(editorChart) { RelativeSizeAxes = Axes.Both };
        });

        private void waitForComposer() => AddUntilStep("wait for composer", () => harness.Composer?.IsLoaded == true);

        private EditorDrawableCardinalNote drawableFor(CardinalNote note) =>
            playfield.ChildrenOfType<EditorDrawableCardinalNote>().Single(d => d.HitObject == note);

        [Test]
        public void CoincidentPairColouredYellowLoneStaysWhite()
        {
            var a = new CardinalNote { AngleDeg = 90, StartTime = 1000 };
            var b = new CardinalNote { AngleDeg = 270, StartTime = 1000 };
            var lone = new CardinalNote { AngleDeg = 0, StartTime = 2000 };

            waitForComposer();
            AddStep("add three notes (two coincident)", () => editorChart.AddRange(new[] { a, b, lone }));

            AddUntilStep("a yellow", () => drawableFor(a).Colour.Equals((ColourInfo)ChordColours.Highlight));
            AddUntilStep("b yellow", () => drawableFor(b).Colour.Equals((ColourInfo)ChordColours.Highlight));
            AddUntilStep("lone white", () => drawableFor(lone).Colour.Equals((ColourInfo)Colour4.White));
        }

        [Test]
        public void MovingNoteOffChordReturnsItToWhite()
        {
            var a = new CardinalNote { AngleDeg = 90, StartTime = 1000 };
            var b = new CardinalNote { AngleDeg = 270, StartTime = 1000 };

            waitForComposer();
            AddStep("add coincident pair", () => editorChart.AddRange(new[] { a, b }));
            AddUntilStep("both yellow", () =>
                drawableFor(a).Colour.Equals((ColourInfo)ChordColours.Highlight) &&
                drawableFor(b).Colour.Equals((ColourInfo)ChordColours.Highlight));

            AddStep("move b to a new time", () =>
            {
                b.StartTime = 3000;
                editorChart.Update(b);
            });

            AddUntilStep("a back to white", () => drawableFor(a).Colour.Equals((ColourInfo)Colour4.White));
            AddUntilStep("b white", () => drawableFor(b).Colour.Equals((ColourInfo)Colour4.White));
        }

        // Self-contained copy of TestSceneComposePlacement's DI harness (that one is private/nested).
        // Caches the deps the composer tree resolves, wires the composer subtree to the EditorClock, and
        // hosts the real GarbusHitObjectComposer.
        private partial class ChordEditorHarness : Container
        {
            private readonly EditorChart editorChart;
            private DependencyContainer dependencies = null!;

            public GarbusHitObjectComposer Composer { get; private set; } = null!;

            public ChordEditorHarness(EditorChart editorChart)
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
                Child = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Clock = dependencies.Get<EditorClock>(),
                        Child = Composer = new GarbusHitObjectComposer { RelativeSizeAxes = Axes.Both },
                    },
                };
                AddInternal(dependencies.Get<EditorClock>());
            }
        }
    }
}
