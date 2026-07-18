// Tests for the Design tab: enum ordering, tab visibility + timeline strip, and (later tasks) the
// point list, settings pane, and timeline region overlay.

using System;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Timeline;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneDesignTab : GarbusTestScene
    {
        private GarbusEditor editor = null!;
        private osu.Framework.Testing.Input.ManualInputManager input = null!;

        private void setupEditor() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new Garbus.Game.Charts.Timing.TimingControlPoint { BeatLength = 500 });

            var chartFile = new ChartFile(chart);
            editor = new GarbusEditor(chartFile);
            Child = input = new osu.Framework.Testing.Input.ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
            };
        });

        private void switchToDesignTab()
        {
            AddUntilStep("editor loaded", () => editor.IsLoaded);
            AddStep("switch to Design tab", () => editor.Tab.Value = EditorTab.Design);
            AddUntilStep("design tab visible", () =>
                editor.ChildrenOfType<DesignTab>().Any() &&
                editor.ChildrenOfType<DesignTab>().First().State.Value == Visibility.Visible);
        }

        [Test]
        public void TestDesignTabIsBetweenTimingAndVerify()
        {
            AddAssert("Design after Timing", () => (int)EditorTab.Design > (int)EditorTab.Timing);
            AddAssert("Design before Verify", () => (int)EditorTab.Design < (int)EditorTab.Verify);
        }

        [Test]
        public void TestDesignTabShowsTimelineStrip()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("timeline strip present in design tab", () =>
                editor.ChildrenOfType<DesignTab>().First().ChildrenOfType<TimelineStrip>().Any());
        }
    }
}
