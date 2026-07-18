// Tests for the Design tab: enum ordering, tab visibility + timeline strip, and (later tasks) the
// point list, settings pane, and timeline region overlay.

using System;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Design;
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

        private DesignPointList designList() => editor.ChildrenOfType<DesignPointList>().First();

        private osu.Framework.Graphics.UserInterface.BasicButton designButton(string text) =>
            designList().ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                        .First(b => b.Text.ToString() == text);

        [Test]
        public void TestAddCreatesTutorialMessageAtPlayhead()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("list present", () => editor.ChildrenOfType<DesignPointList>().Any());
            AddStep("seek clock to 4000ms", () =>
                editor.ChildrenOfType<EditorClock>().First().Seek(4000));

            AddStep("really click Add", () =>
            {
                input.MoveMouseTo(designButton("Add"));
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddAssert("one design point exists", () =>
                editor.EditorChart.DesignPointInfo.DesignPoints.Count == 1);
            AddAssert("it is a TutorialMessage near 4000 spanning 2000ms", () =>
            {
                var p = editor.EditorChart.DesignPointInfo.DesignPoints[0] as TutorialMessage;
                return p != null && Math.Abs(p.StartTime - 4000) < 100 && Math.Abs(p.EndTime - p.StartTime - 2000) < 1;
            });
        }

        [Test]
        public void TestDeleteRemovesSelectedPoint()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("list present", () => editor.ChildrenOfType<DesignPointList>().Any());
            AddStep("seek to 4000 and add", () =>
            {
                editor.ChildrenOfType<EditorClock>().First().Seek(4000);
                input.MoveMouseTo(designButton("Add"));
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddUntilStep("one point + selected", () =>
                editor.EditorChart.DesignPointInfo.DesignPoints.Count == 1 &&
                designList().SelectedPoint.Value != null);

            AddStep("really click Delete", () =>
            {
                input.MoveMouseTo(designButton("Delete"));
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddAssert("no design points remain", () =>
                editor.EditorChart.DesignPointInfo.DesignPoints.Count == 0);
        }

        [Test]
        public void TestSelectingRowSeeksClock()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("list present", () => editor.ChildrenOfType<DesignPointList>().Any());
            AddStep("add a point at 5000", () =>
            {
                editor.ChildrenOfType<EditorClock>().First().Seek(5000);
                input.MoveMouseTo(designButton("Add"));
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddStep("seek away to 0", () =>
                editor.ChildrenOfType<EditorClock>().First().Seek(0));

            // Add auto-selects the new point, so the first click deselects it (no seek); the second
            // click re-selects it and seeks the clock to its StartTime.
            AddStep("click the row to deselect", () =>
            {
                var row = editor.ChildrenOfType<DesignPointRow>().First();
                input.MoveMouseTo(row);
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddUntilStep("deselected", () => designList().SelectedPoint.Value == null);

            AddStep("click the row to select + seek", () =>
            {
                var row = editor.ChildrenOfType<DesignPointRow>().First();
                input.MoveMouseTo(row);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddUntilStep("clock seeked back near 5000", () =>
                Math.Abs(editor.ChildrenOfType<EditorClock>().First().CurrentTime - 5000) < 100);
        }
    }
}
