// Regression test for the "Auto-Seek on Placement" View toggle not taking effect until an editor
// reload.
//
// Root cause: ComposeTab obtained the config bindable via GarbusConfigManager.GetBindable(...) into a
// LOCAL variable and only kept it alive through a BindValueChanged closure (which captures the
// composer, not the bindable). GetBindable returns a bound copy the config holds only via a weak
// reference, so once load() returned the copy became GC-eligible; after collection the menu toggle's
// changes no longer propagated to composer.AutoSeekOnPlacement. A reload reconstructed ComposeTab and
// re-synced the current value, masking the leak. Fix: store the bindable in a field (as TimelineStrip
// already does for its View-toggle bindables).
//
// This test hosts the REAL ComposeTab, forces a GC after load, then flips the config and asserts the
// composer's bindable follows.

using System;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Configuration;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
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
    public partial class TestSceneComposeTabConfig : GarbusTestScene
    {
        private ComposeTabHarness harness = null!;

        private GarbusHitObjectComposer composer =>
            harness.ChildrenOfType<GarbusHitObjectComposer>().First();

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            Child = harness = new ComposeTabHarness(chart) { RelativeSizeAxes = Axes.Both };
        });

        [Test]
        public void TestAutoSeekToggleAppliesAfterGc()
        {
            AddUntilStep("composer loaded", () =>
                harness.ChildrenOfType<GarbusHitObjectComposer>().Any(c => c.IsLoaded));

            AddAssert("auto-seek initially on", () => composer.AutoSeekOnPlacement.Value);

            // Force collection of any config bindable copy that is only weakly referenced. If the
            // ComposeTab kept its copy in a local variable, this severs the wiring.
            AddStep("force GC", () =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });

            AddStep("turn auto-seek off via config", () =>
                harness.Config.SetValue(GarbusSetting.EditorAutoSeekOnPlacement, false));

            AddAssert("composer auto-seek followed to off",
                () => !composer.AutoSeekOnPlacement.Value);

            AddStep("turn auto-seek on via config", () =>
                harness.Config.SetValue(GarbusSetting.EditorAutoSeekOnPlacement, true));

            AddAssert("composer auto-seek followed to on",
                () => composer.AutoSeekOnPlacement.Value);
        }

        // ------------------------------------------------------------------
        // Harness: caches the DI deps the ComposeTab subtree requires, then
        // hosts the real ComposeTab as its child (mirrors TimelineHarness).
        // ------------------------------------------------------------------

        private partial class ComposeTabHarness : Container
        {
            private readonly GarbusChart chart;
            private DependencyContainer dependencies = null!;

            public GarbusConfigManager Config { get; private set; } = null!;

            public ComposeTabHarness(GarbusChart chart)
            {
                this.chart = chart;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                Config = parent.Get<GarbusConfigManager>();

                var beatDivisor = new BindableBeatDivisor(4);
                var editorChart = new EditorChart(chart);
                var editorClock = new EditorClock(editorChart.ControlPointInfo, 60000, beatDivisor);
                editorClock.ChangeSource(new TrackVirtual(60000));

                dependencies.Cache(editorChart);
                dependencies.Cache(editorClock);
                dependencies.Cache(beatDivisor);
                dependencies.CacheAs<IEditorChangeHandler>(new GarbusChartChangeHandler(editorChart));
                dependencies.CacheAs(new ChartFile(chart));
                dependencies.CacheAs(editorChart.ControlPointInfo);
                dependencies.CacheAs(editorChart.DesignPointInfo);

                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AddInternal(dependencies.Get<EditorClock>());
                AddInternal(new ComposeTab { RelativeSizeAxes = Axes.Both, State = { Value = Visibility.Visible } });
            }
        }
    }
}
