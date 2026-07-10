// TDD tests for Task 21: Timing tab.
//
// Contract under test:
//   1. Add-at-playhead creates a TimingControlPoint at the snapped current time.
//   2. BPM textbox set to 180 → BeatLength ≈ 333.33ms.
//   3. Deletion removes the selected point.
//   4. Undo restores a deleted point.
//   5. 4 simulated taps 500ms apart → BPM 120 (tap handler driven directly).
//
// TapButton.RecordTap(double timestamp) is the injectable test hook — no UI interaction required.
// All timing edits are verified not to move hit objects (none exist in these tests, so the
// ControlPointInfo mutation is the only observable effect).

using System;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Timing;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneTimingTab : GarbusTestScene
    {
        private GarbusEditor editor = null!;

        // ------------------------------------------------------------------
        // Setup helpers
        // ------------------------------------------------------------------

        private osu.Framework.Testing.Input.ManualInputManager input = null!;

        private void setupEditor(double initialBpm = 120.0) => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 60000.0 / initialBpm });

            var chartFile = new ChartFile(chart);
            editor = new GarbusEditor(chartFile);
            Child = input = new osu.Framework.Testing.Input.ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
            };
        });

        private void switchToTimingTab()
        {
            AddUntilStep("editor loaded", () => editor.IsLoaded);
            AddStep("switch to Timing tab", () => editor.Tab.Value = EditorTab.Timing);
            AddUntilStep("timing tab visible", () =>
                editor.ChildrenOfType<TimingTab>().Any() &&
                editor.ChildrenOfType<TimingTab>().First().State.Value == Visibility.Visible &&
                editor.ChildrenOfType<TimingPointList>().Any());
        }

        // ------------------------------------------------------------------
        // 1. Add-at-playhead creates a point at snapped current time
        // ------------------------------------------------------------------

        [Test]
        public void TestAddAtPlayheadCreatesTimingPoint()
        {
            setupEditor();
            switchToTimingTab();

            int initialCount = 0;
            double seekTime = 1000;

            AddStep("capture initial count", () =>
                initialCount = editor.EditorChart.ControlPointInfo.TimingPoints.Count);

            AddStep("seek clock to 1000ms", () =>
                editor.ChildrenOfType<EditorClock>().First().Seek(seekTime));

            AddStep("click Add button", () =>
            {
                var list = editor.ChildrenOfType<TimingPointList>().First();
                // Trigger the AddAtPlayhead via the public Add button.
                var addButton = list.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                    .FirstOrDefault(b => b.Text.ToString() == "Add");
                addButton?.TriggerClick();
            });

            AddAssert("timing point count increased", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == initialCount + 1);

            AddAssert("new point near 1000ms", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints
                    .Any(tp => Math.Abs(tp.Time - seekTime) < 100));
        }

        /// <summary>
        /// Regression guard (ISSUES.md): the Add/Delete buttons must respond to REAL mouse clicks
        /// through the positional input pipeline, not just TriggerClick().
        /// </summary>
        [Test]
        public void TestAddButtonRespondsToRealClick()
        {
            setupEditor();
            switchToTimingTab();

            int initialCount = 0;
            AddStep("capture initial count", () =>
                initialCount = editor.EditorChart.ControlPointInfo.TimingPoints.Count);

            AddStep("seek clock to 1000ms", () =>
                editor.ChildrenOfType<EditorClock>().First().Seek(1000));

            AddStep("really click Add button", () =>
            {
                var addButton = editor.ChildrenOfType<TimingPointList>().First()
                    .ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                    .First(b => b.Text.ToString() == "Add");
                input.MoveMouseTo(addButton);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddAssert("timing point count increased", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == initialCount + 1);
        }

        /// <summary>
        /// ISSUES.md "Add and Delete buttons don't respond to click": on a fresh chart the playhead
        /// sits at 0 where a timing point already exists, so Add silently replaced the point in the
        /// same group and Delete silently refused to remove the only point. The buttons must instead
        /// be visibly disabled when their action can't do anything.
        /// </summary>
        [Test]
        public void TestButtonsDisableWhenActionImpossible()
        {
            setupEditor();
            switchToTimingTab();

            // Fresh chart: playhead at 0 on the existing point, only one timing point.
            AddUntilStep("Add disabled at existing point", () => !addButton().Enabled.Value);
            AddUntilStep("Delete disabled for only point", () => !deleteButton().Enabled.Value);

            AddStep("seek clock to 1000ms", () =>
                editor.ChildrenOfType<EditorClock>().First().Seek(1000));
            AddUntilStep("Add enabled away from points", () => addButton().Enabled.Value);

            AddStep("really click Add", () =>
            {
                input.MoveMouseTo(addButton());
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddAssert("second point added", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == 2);

            AddUntilStep("Add now disabled on new point", () => !addButton().Enabled.Value);
            AddUntilStep("Delete now enabled", () => deleteButton().Enabled.Value);

            AddStep("really click Delete", () =>
            {
                input.MoveMouseTo(deleteButton());
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddAssert("back to one point", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == 1);
        }

        /// <summary>
        /// ISSUES.md: selecting a row seeks onto the point, which used to grey Add out with no way
        /// back. Re-clicking the selected row must deselect it, and Add must re-enable (its meaning
        /// is now "add or focus the group at the playhead", following osu).
        /// </summary>
        [Test]
        public void TestReclickDeselectsAndReenablesAdd()
        {
            setupEditor();
            switchToTimingTab();

            // Fresh chart auto-selects the first group with the playhead parked on it.
            AddUntilStep("point selected", () =>
                editor.ChildrenOfType<TimingPointList>().First().SelectedGroup.Value != null);
            AddUntilStep("Add disabled while selected at playhead", () => !addButton().Enabled.Value);

            AddStep("re-click the selected row", () =>
            {
                var row = editor.ChildrenOfType<TimingPointRow>().First();
                input.MoveMouseTo(row);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddUntilStep("deselected", () =>
                editor.ChildrenOfType<TimingPointList>().First().SelectedGroup.Value == null);
            AddUntilStep("Add re-enabled", () => addButton().Enabled.Value);

            AddStep("click Add (focuses existing group)", () =>
            {
                input.MoveMouseTo(addButton());
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddAssert("no duplicate point added", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == 1);
            AddUntilStep("group focused again", () =>
                editor.ChildrenOfType<TimingPointList>().First().SelectedGroup.Value != null);
        }

        private osu.Framework.Graphics.UserInterface.BasicButton addButton() =>
            editor.ChildrenOfType<TimingPointList>().First()
                  .ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                  .First(b => b.Text.ToString() == "Add");

        private osu.Framework.Graphics.UserInterface.BasicButton deleteButton() =>
            editor.ChildrenOfType<TimingPointList>().First()
                  .ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                  .First(b => b.Text.ToString() == "Delete");

        // ------------------------------------------------------------------
        // 2. BPM textbox 180 → BeatLength ≈ 333.33
        // ------------------------------------------------------------------

        [Test]
        public void TestBpmTextboxSetsBeatLength()
        {
            setupEditor();
            switchToTimingTab();

            AddUntilStep("settings panel loaded", () =>
                editor.ChildrenOfType<TimingPointSettings>().Any());

            AddStep("set BPM to 180 via settings seam", () =>
            {
                var settings = editor.ChildrenOfType<TimingPointSettings>().First();
                settings.SetBpmAndCommit(180);
            });

            AddAssert("BeatLength ≈ 333.33ms", () =>
            {
                var tp = editor.EditorChart.ControlPointInfo.TimingPoints.FirstOrDefault();
                return tp != null && Math.Abs(tp.BeatLength - (60000.0 / 180.0)) < 1.0;
            });
        }

        // ------------------------------------------------------------------
        // 3. Deletion removes the selected point
        // ------------------------------------------------------------------

        [Test]
        public void TestDeleteRemovesSelectedPoint()
        {
            // Start with two timing points so we can delete one.
            Schedule(() =>
            {
                var chart = new GarbusChart();
                chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                chart.ControlPointInfo.Add(2000, new TimingControlPoint { BeatLength = 400 });
                var chartFile = new ChartFile(chart);
                editor = new GarbusEditor(chartFile);
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both };
            });

            switchToTimingTab();

            AddUntilStep("two timing points exist", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == 2);

            AddStep("select second row and delete", () =>
            {
                var list = editor.ChildrenOfType<TimingPointList>().First();

                // Select the second row.
                var rows = list.ChildrenOfType<TimingPointRow>().ToList();
                if (rows.Count >= 2)
                    rows[1].TriggerClick();

                // Click delete.
                var deleteButton = list.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                    .FirstOrDefault(b => b.Text.ToString() == "Delete");
                deleteButton?.TriggerClick();
            });

            AddAssert("one timing point remains", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == 1);
        }

        // ------------------------------------------------------------------
        // 4. Undo restores a deleted point
        // ------------------------------------------------------------------

        [Test]
        public void TestUndoRestoresDeletedPoint()
        {
            Schedule(() =>
            {
                var chart = new GarbusChart();
                chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                chart.ControlPointInfo.Add(2000, new TimingControlPoint { BeatLength = 400 });
                var chartFile = new ChartFile(chart);
                editor = new GarbusEditor(chartFile);
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both };
            });

            switchToTimingTab();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("get change handler", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddUntilStep("two timing points", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == 2);

            AddStep("select second row and delete", () =>
            {
                var list = editor.ChildrenOfType<TimingPointList>().First();
                var rows = list.ChildrenOfType<TimingPointRow>().ToList();
                if (rows.Count >= 2)
                    rows[1].TriggerClick();

                var deleteButton = list.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                    .FirstOrDefault(b => b.Text.ToString() == "Delete");
                deleteButton?.TriggerClick();
            });

            AddUntilStep("one timing point after delete", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == 1);

            AddAssert("undo available", () => changeHandler.CanUndo.Value);

            AddStep("undo deletion", () => changeHandler.Undo());

            AddUntilStep("two timing points restored", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == 2);
        }

        // ------------------------------------------------------------------
        // 5. 4 simulated taps 500ms apart → BPM 120
        // ------------------------------------------------------------------

        [Test]
        public void TestFourTaps500MsApartGivesBpm120()
        {
            setupEditor(60.0); // Start at 60 BPM so we can verify it changes.
            switchToTimingTab();

            AddUntilStep("tap button loaded", () =>
                editor.ChildrenOfType<TapButton>().Any());

            // The tap algorithm: initial_taps_to_ignore = 4, so we need at least 8 taps.
            // Drive them via RecordTap with synthetic timestamps.
            // 8 taps at 500ms intervals: timestamps 0, 500, 1000, 1500, 2000, 2500, 3000, 3500.
            // After skipping first 4, averaging remaining 4 intervals of 500ms each → BPM 120.
            AddStep("record 8 taps at 500ms intervals", () =>
            {
                var tapBtn = editor.ChildrenOfType<TapButton>().First();
                for (int i = 0; i < 8; i++)
                    tapBtn.RecordTap(i * 500.0);
            });

            AddAssert("BeatLength ≈ 500ms (BPM 120)", () =>
            {
                var tp = editor.EditorChart.ControlPointInfo.TimingPoints.FirstOrDefault();
                return tp != null && Math.Abs(tp.BeatLength - 500.0) < 2.0;
            });
        }

        // ------------------------------------------------------------------
        // 6. Tap-written BPM is undoable
        // ------------------------------------------------------------------

        [Test]
        public void TestTapBpmIsUndoable()
        {
            setupEditor(60.0); // Start at 60 BPM (BeatLength 1000ms).
            switchToTimingTab();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("get change handler", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddUntilStep("tap button loaded", () =>
                editor.ChildrenOfType<TapButton>().Any());

            // Capture BeatLength before tapping.
            double priorBeatLength = 0;
            AddStep("capture prior BeatLength", () =>
            {
                var tp = editor.EditorChart.ControlPointInfo.TimingPoints.FirstOrDefault();
                priorBeatLength = tp?.BeatLength ?? 0;
            });

            // 8 taps at 500ms intervals → BPM 120, BeatLength 500ms.
            AddStep("record 8 taps at 500ms intervals", () =>
            {
                var tapBtn = editor.ChildrenOfType<TapButton>().First();
                for (int i = 0; i < 8; i++)
                    tapBtn.RecordTap(i * 500.0);
            });

            AddAssert("BeatLength changed to ≈500ms", () =>
            {
                var tp = editor.EditorChart.ControlPointInfo.TimingPoints.FirstOrDefault();
                return tp != null && Math.Abs(tp.BeatLength - 500.0) < 2.0;
            });

            AddAssert("undo available after tap", () => changeHandler.CanUndo.Value);

            AddStep("undo tap", () => changeHandler.Undo());

            AddAssert("BeatLength restored to prior value", () =>
            {
                var tp = editor.EditorChart.ControlPointInfo.TimingPoints.FirstOrDefault();
                return tp != null && Math.Abs(tp.BeatLength - priorBeatLength) < 1.0;
            });
        }

        // ------------------------------------------------------------------
        // 7. Table rows render attribute chips that live-update
        // ------------------------------------------------------------------

        [Test]
        public void TestRowShowsAttributeChips()
        {
            setupEditor();
            switchToTimingTab();

            AddUntilStep("row has 120 BPM chip", () =>
                editor.ChildrenOfType<TimingPointRow.AttributeChip>()
                      .Any(c => c.Text.ToString() == "120 BPM"));

            AddAssert("row has signature chip", () =>
                editor.ChildrenOfType<TimingPointRow.AttributeChip>()
                      .Any(c => c.Text.ToString() == "4/4"));

            AddAssert("no-barline chip hidden by default", () =>
                editor.ChildrenOfType<TimingPointRow.AttributeChip>()
                      .Single(c => c.Text.ToString() == "no barline").Alpha == 0);

            AddStep("set BPM to 180 via settings seam", () =>
                editor.ChildrenOfType<TimingPointSettings>().First().SetBpmAndCommit(180));

            AddUntilStep("BPM chip updated in place", () =>
                editor.ChildrenOfType<TimingPointRow.AttributeChip>()
                      .Any(c => c.Text.ToString() == "180 BPM"));
        }
    }
}
