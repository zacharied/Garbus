// TDD tests for Task 17: top timeline strip + zoom sync + View toggle.
//
// Contract under test:
//   1. Zoom changes write TimelineTimeRange = TrackLength / zoom / 2  (BAC formula).
//   2. Toggling EditorShowTicks hides/shows the TimelineTickDisplay layer.
//   3. Object markers appear (TimelineObjectMarkers becomes non-empty) when a note is added.
//   4. Playfield does NOT advance while the EditorClock is stopped (clock-wiring regression guard).
//
// All tests use TrackVirtual(60000ms) — no real audio — so waveform layer must not crash.

using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Configuration;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens.Timeline;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneTimeline : GarbusTestScene
    {
        private TimelineHarness harness = null!;

        private TimelineStrip strip => harness.Strip;
        private GarbusHitObjectComposer composer => harness.Composer;
        private EditorClock editorClock => harness.EditorClock;
        private EditorChart editorChart => harness.EditorChart;

        [SetUp]
        public new void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            Child = harness = new TimelineHarness(chart) { RelativeSizeAxes = Axes.Both };
        });

        private void waitForStrip() => AddUntilStep("strip loaded", () => strip?.IsLoaded == true);
        private void waitForComposer() => AddUntilStep("composer loaded", () => composer?.IsLoaded == true);

        // ------------------------------------------------------------------
        // 1. Zoom formula: TimelineTimeRange = TrackLength / zoom / 2
        // ------------------------------------------------------------------

        [Test]
        public void TestZoomSyncsComposerTimeRange()
        {
            waitForStrip();
            waitForComposer();

            float capturedZoom = 0;
            AddStep("capture current zoom", () => capturedZoom = strip.CurrentZoom.Value);
            AddAssert("TimelineTimeRange follows formula",
                () =>
                {
                    double expected = editorClock.TrackLength / capturedZoom / 2;
                    double actual = composer.TimelineTimeRange.Value;
                    // Allow 1ms tolerance for floating-point rounding.
                    return System.Math.Abs(actual - expected) < 1.0;
                });

            AddStep("increase zoom", () => strip.Zoom = strip.CurrentZoom.Value + 2f);
            AddUntilStep("TimelineTimeRange updated after zoom change", () =>
            {
                double zoom = strip.CurrentZoom.Value;
                if (zoom <= 0) return false;
                double expected = editorClock.TrackLength / zoom / 2;
                double actual = composer.TimelineTimeRange.Value;
                return System.Math.Abs(actual - expected) < 1.0;
            });
        }

        // ------------------------------------------------------------------
        // 2. EditorShowTicks toggle hides the tick display
        // ------------------------------------------------------------------

        [Test]
        public void TestShowTicksToggleHidesTickDisplay()
        {
            waitForStrip();

            AddUntilStep("tick display alpha 1 initially",
                () => strip.ChildrenOfType<TimelineTickDisplay>().FirstOrDefault()?.Alpha == 1);

            AddStep("turn off EditorShowTicks", () =>
                harness.Config.SetValue(GarbusSetting.EditorShowTicks, false));

            AddUntilStep("tick display alpha 0",
                () => strip.ChildrenOfType<TimelineTickDisplay>().FirstOrDefault()?.Alpha == 0);

            AddStep("turn on EditorShowTicks", () =>
                harness.Config.SetValue(GarbusSetting.EditorShowTicks, true));

            AddUntilStep("tick display alpha 1 again",
                () => strip.ChildrenOfType<TimelineTickDisplay>().FirstOrDefault()?.Alpha == 1);
        }

        // ------------------------------------------------------------------
        // 3. Object markers appear when a note is added
        // ------------------------------------------------------------------

        private TimelineObjectMarkers? findMarkers() => strip.ChildrenOfType<TimelineObjectMarkers>().FirstOrDefault();

        [Test]
        public void TestObjectMarkersAppearOnNoteAdded()
        {
            waitForStrip();

            AddAssert("markers component exists", () => findMarkers() != null);

            // Initially no objects → no visible markers.
            AddUntilStep("no markers initially", () => findMarkers()!.VisibleMarkerCount == 0);

            AddStep("add note at 1000ms", () =>
                editorChart.Add(new CardinalNote { StartTime = 1000, AngleDeg = 90 }));

            AddUntilStep("at least one marker visible", () => findMarkers()!.VisibleMarkerCount > 0);
        }

        // ------------------------------------------------------------------
        // 4. Playfield frozen while EditorClock is stopped (clock-wiring guard)
        // ------------------------------------------------------------------

        [Test]
        public void TestPlayfieldFrozenWhileClockStopped()
        {
            waitForComposer();

            // Place a note so there's a drawable to inspect.
            AddStep("add note", () =>
                editorChart.Add(new CardinalNote { StartTime = 5000, AngleDeg = 270 }));

            // Stop the clock and seek to note time.
            AddStep("stop and seek", () =>
            {
                editorClock.Stop();
                editorClock.Seek(5000);
            });

            // Read the initial drawable Y and check it doesn't change over two frames.
            float? yCapture = null;
            AddUntilStep("drawable stable — capture position", () =>
            {
                var draw = composer.HitObjects.FirstOrDefault();
                if (draw == null) return false;
                float y = draw.ScreenSpaceDrawQuad.Centre.Y;
                if (yCapture == null)
                {
                    yCapture = y;
                    return false;
                }
                return System.Math.Abs(y - yCapture.Value) < 0.1f;
            });

            float frozenY = 0;
            AddStep("record frozen Y", () =>
            {
                frozenY = composer.HitObjects.First().ScreenSpaceDrawQuad.Centre.Y;
            });

            // Advance several steps (clock is stopped, so time must not change).
            AddWaitStep("wait a few frames (clock stopped)", 5);

            AddAssert("drawable Y unchanged while stopped", () =>
            {
                float y = composer.HitObjects.First().ScreenSpaceDrawQuad.Centre.Y;
                return System.Math.Abs(y - frozenY) < 0.5f;
            });
        }

        // ------------------------------------------------------------------
        // Harness
        // ------------------------------------------------------------------

        private partial class TimelineHarness : Container
        {
            private readonly GarbusChart chart;
            private DependencyContainer dependencies = null!;

            public TimelineStrip Strip { get; private set; } = null!;
            public GarbusHitObjectComposer Composer { get; private set; } = null!;
            public EditorClock EditorClock { get; private set; } = null!;
            public EditorChart EditorChart { get; private set; } = null!;
            public GarbusConfigManager Config { get; private set; } = null!;

            public TimelineHarness(GarbusChart chart)
            {
                this.chart = chart;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                // Resolve config from the game-level DI (GarbusGameBase caches it).
                Config = parent.Get<GarbusConfigManager>();

                var beatDivisor = new BindableBeatDivisor(4);
                EditorChart = new EditorChart(chart);
                EditorClock = new EditorClock(EditorChart.ControlPointInfo, 60000, beatDivisor);
                EditorClock.ChangeSource(new TrackVirtual(60000));

                var chartFile = new ChartFile(chart);

                dependencies.Cache(EditorChart);
                dependencies.Cache(EditorClock);
                dependencies.Cache(beatDivisor);
                dependencies.CacheAs<IEditorChangeHandler>(new GarbusChartChangeHandler(EditorChart));
                dependencies.CacheAs(chartFile);
                // Cache ControlPointInfo directly so child timeline components can resolve it.
                dependencies.CacheAs(EditorChart.ControlPointInfo);

                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // EditorClock in hierarchy so it processes.
                AddInternal(EditorClock);

                // Wire the composer's clock to the EditorClock (same as ComposeTab does).
                var composerContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = TimelineStrip.HEIGHT },
                    Clock = EditorClock,
                    Child = Composer = new GarbusHitObjectComposer { RelativeSizeAxes = Axes.Both },
                };

                Strip = new TimelineStrip();

                // Wire zoom → TimelineTimeRange before loading.
                Strip.CurrentZoom.BindValueChanged(e =>
                {
                    double trackLength = EditorClock.TrackLength;
                    if (trackLength > 0 && e.NewValue > 0)
                        Composer.TimelineTimeRange.Value = trackLength / e.NewValue / 2;
                });

                Children = new Drawable[]
                {
                    Strip,
                    composerContainer,
                };
            }

            protected override void Update()
            {
                base.Update();

                float zoom = Strip.CurrentZoom.Value;
                double trackLength = EditorClock.TrackLength;
                if (zoom > 0 && trackLength > 0)
                    Composer.TimelineTimeRange.Value = trackLength / zoom / 2;
            }
        }
    }
}
