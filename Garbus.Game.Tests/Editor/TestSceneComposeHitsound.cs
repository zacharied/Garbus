// Reproduction: hitsounds should play as the editor playhead crosses hit objects during playback.

using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Gameplay.Audio;
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
    public partial class TestSceneComposeHitsound : GarbusTestScene
    {
        private ComposeHitsoundHarness harness = null!;
        private EditorChart editorChart = null!;

        private GarbusHitObjectComposer composer => harness.Composer;
        private EditorClock clock => harness.EditorClock;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            editorChart = new EditorChart(chart);

            Child = harness = new ComposeHitsoundHarness(editorChart) { RelativeSizeAxes = Axes.Both };
        });

        [Test]
        public void TestPlaybackPlaysHitsound()
        {
            AddStep("add cardinal note at 2000ms", () => editorChart.Add(new CardinalNote { StartTime = 2000, AngleDeg = 270 }));
            AddUntilStep("composer loaded", () => composer?.IsLoaded == true);

            AddStep("seek to 1500ms", () => clock.Seek(1500));
            AddUntilStep("clock settled before note", () => clock.CurrentTime >= 1400 && clock.CurrentTime < 2000);

            AddStep("start playback", () => clock.Start());
            AddUntilStep("clock passes note", () => clock.CurrentTime > 2200);
            AddStep("stop playback", () => clock.Stop());

            AddAssert("a hitsound played", () => totalPlayCount() >= 1);
        }

        [Test]
        public void TestPlaybackAfterScrubbingPastPlaysHitsound()
        {
            AddStep("add cardinal note at 2000ms", () => editorChart.Add(new CardinalNote { StartTime = 2000, AngleDeg = 270 }));
            AddUntilStep("composer loaded", () => composer?.IsLoaded == true);

            // Scrub PAST the object while stopped (as a user does while authoring). This silently
            // judges it (clock not running → no sound).
            AddStep("seek past note to 2500ms", () => clock.Seek(2500));
            AddUntilStep("clock settled past note", () => clock.CurrentTime >= 2400);

            // Now rewind and play from before it.
            AddStep("seek back to 1500ms", () => clock.Seek(1500));
            AddUntilStep("clock settled before note", () => clock.CurrentTime < 2000);

            AddStep("start playback", () => clock.Start());
            AddUntilStep("clock passes note", () => clock.CurrentTime > 2200);
            AddStep("stop playback", () => clock.Stop());

            AddAssert("a hitsound played", () => totalPlayCount() >= 1);
        }

        private int totalPlayCount() =>
            composer.ChildrenOfType<HitSoundContainer>().Sum(c => c.PlayCount);

        private partial class ComposeHitsoundHarness : Container
        {
            private readonly EditorChart editorChart;
            private DependencyContainer dependencies = null!;

            public GarbusHitObjectComposer Composer { get; private set; } = null!;
            // Not named "Clock": that would hide Drawable.Clock, which the framework still drives itself.
            public EditorClock EditorClock { get; private set; } = null!;

            public ComposeHitsoundHarness(EditorChart editorChart)
            {
                this.editorChart = editorChart;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                var beatDivisor = new BindableBeatDivisor(4);
                EditorClock = new EditorClock(editorChart.ControlPointInfo, 60000, beatDivisor);
                EditorClock.ChangeSource(new TrackVirtual(60000));

                dependencies.Cache(editorChart);
                dependencies.Cache(EditorClock);
                dependencies.Cache(beatDivisor);

                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = EditorClock,
                    Child = Composer = new GarbusHitObjectComposer { RelativeSizeAxes = Axes.Both },
                };
                AddInternal(EditorClock);
            }
        }
    }
}
