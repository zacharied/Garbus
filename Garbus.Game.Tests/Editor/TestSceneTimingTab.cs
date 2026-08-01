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
// Timing edits move the objects in the affected timing section when "Move objects with timing
// changes" is enabled (the default) — see the section-adjustment tests below.

using System;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Timeline;
using Garbus.Game.Edit.Screens.Timing;
using Garbus.Game.Objects;
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

        private void setupEditor(double initialBpm = 120.0, double? secondPointTime = null) => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo!.Add(0, new TimingControlPoint { BeatLength = 60000.0 / initialBpm });
            if (secondPointTime != null)
                chart.ControlPointInfo.Add(secondPointTime.Value, new TimingControlPoint { BeatLength = 500 });

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

        [Test]
        public void TestTimingScopeBannerTracksOwnership()
        {
            Schedule(() =>
            {
                editor = new GarbusEditor(new SongFile(GarbusSong.CreateDefault()));
                Child = input = new osu.Framework.Testing.Input.ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
                };
            });
            switchToTimingTab();

            AddAssert("shared timing message", () =>
                editor.ChildrenOfType<TimingPointList>().First().ScopeText.Text.ToString()
                == "Changes made here will affect all charts in this song.");

            AddAssert("table viewport starts below banner and header", () =>
            {
                var list = editor.ChildrenOfType<TimingPointList>().First();
                var scroll = list.ChildrenOfType<BasicScrollContainer>().Single();
                return scroll.ScreenSpaceDrawQuad.AABBFloat.Top
                       >= list.ScreenSpaceDrawQuad.AABBFloat.Top + 70 - 1;
            });

            AddStep("switch to per-chart timing", () => editor.EditorSong.UsePerChartTiming());
            AddUntilStep("per-chart timing message", () =>
                editor.ChildrenOfType<TimingPointList>().First().ScopeText.Text.ToString()
                == "Changes made here will affect only this chart.");
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
        /// Regression guard ("Add and Delete buttons don't respond to click"): on a fresh chart the playhead
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
        /// Selecting a row seeks onto the point, which used to grey Add out with no way
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
        // Undo restores a deleted point
        // ------------------------------------------------------------------

        [Test]
        public void TestUndoRestoresDeletedPoint()
        {
            Schedule(() =>
            {
                var chart = new GarbusChart();
                chart.ControlPointInfo!.Add(0, new TimingControlPoint { BeatLength = 500 });
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
        // Tap-written BPM is undoable
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

            // The tap algorithm: initial_taps_to_ignore = 4, so we need at least 8 taps.
            // 8 taps at 500ms intervals: timestamps 0..3500; after skipping the first 4,
            // averaging the remaining 4 intervals of 500ms each → BPM 120, BeatLength 500ms.
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
        // 8. Arrow keys move the selection
        // ------------------------------------------------------------------

        [Test]
        public void TestArrowKeysMoveSelection()
        {
            setupEditor(secondPointTime: 2000);
            switchToTimingTab();

            AddUntilStep("first group auto-selected", () =>
                timingList().SelectedGroup.Value?.Time == 0);

            AddStep("press Down", () =>
            {
                input.PressKey(osuTK.Input.Key.Down);
                input.ReleaseKey(osuTK.Input.Key.Down);
            });

            AddUntilStep("second group selected", () =>
                timingList().SelectedGroup.Value?.Time == 2000);
            AddUntilStep("clock seeked to selection", () =>
                Math.Abs(editor.ChildrenOfType<EditorClock>().First().CurrentTime - 2000) < 50);

            AddStep("press Down at last row", () =>
            {
                input.PressKey(osuTK.Input.Key.Down);
                input.ReleaseKey(osuTK.Input.Key.Down);
            });
            AddAssert("selection stays on last", () =>
                timingList().SelectedGroup.Value?.Time == 2000);

            AddStep("press Up", () =>
            {
                input.PressKey(osuTK.Input.Key.Up);
                input.ReleaseKey(osuTK.Input.Key.Up);
            });
            AddUntilStep("first group selected again", () =>
                timingList().SelectedGroup.Value?.Time == 0);

            // Deselect (re-clicking the selected row clears the selection), then Up selects the LAST group.
            AddStep("re-click selected row to deselect", () =>
                editor.ChildrenOfType<TimingPointRow>().First().TriggerClick());
            AddUntilStep("nothing selected", () =>
                timingList().SelectedGroup.Value == null);

            AddStep("press Up with no selection", () =>
            {
                input.PressKey(osuTK.Input.Key.Up);
                input.ReleaseKey(osuTK.Input.Key.Up);
            });
            AddUntilStep("last group selected", () =>
                timingList().SelectedGroup.Value?.Time == 2000);
        }

        private TimingPointList timingList() => editor.ChildrenOfType<TimingPointList>().First();

        // ------------------------------------------------------------------
        // 13. Timing edits move objects in the affected section
        // ------------------------------------------------------------------

        private void setupEditorWithObjects() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo!.Add(0, new TimingControlPoint { BeatLength = 500 });
            chart.ControlPointInfo.Add(4000, new TimingControlPoint { BeatLength = 400 });
            chart.HitObjects.Add(new CardinalNote { StartTime = 1000, AngleDeg = 0 });
            chart.HitObjects.Add(new CardinalNote { StartTime = 5000, AngleDeg = 90 });

            var chartFile = new ChartFile(chart);
            editor = new GarbusEditor(chartFile);
            Child = input = new osu.Framework.Testing.Input.ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
            };
        });

        private TimingPointSettings settings() => editor.ChildrenOfType<TimingPointSettings>().First();

        /// <summary>
        /// The right panel scrolls; content near its bottom (the tap-timing adjust rows) must be
        /// scrolled into view before it can be really-clicked.
        /// </summary>
        private void scrollRightPanelToEnd() => AddStep("scroll right panel to end", () =>
            editor.ChildrenOfType<TimingTab>().First()
                  .ChildrenOfType<osu.Framework.Graphics.Containers.BasicScrollContainer>()
                  .First(s => s.ChildrenOfType<TapTimingControl>().Any())
                  .ScrollToEnd(false));

        [Test]
        public void TestOffsetMoveShiftsObjectsInSection()
        {
            setupEditorWithObjects();
            switchToTimingTab();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("get change handler", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddUntilStep("rows loaded", () =>
                editor.ChildrenOfType<TimingPointRow>().Count() == 2);
            AddStep("select second point", () =>
                editor.ChildrenOfType<TimingPointRow>().ElementAt(1).TriggerClick());
            AddUntilStep("second selected", () =>
                timingList().SelectedGroup.Value?.Time == 4000);

            AddStep("move group to 4100 via seam", () => settings().SetOffsetAndCommit(4100));

            AddAssert("note in section shifted to 5100", () =>
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 5100) < 0.01));
            AddAssert("note in earlier section unmoved", () =>
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 1000) < 0.01));

            AddStep("undo", () => changeHandler.Undo());
            AddUntilStep("single undo restores point and note", () =>
                editor.EditorChart.ControlPointInfo.GroupAt(4000) != null &&
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 5000) < 0.01));
        }

        [Test]
        public void TestBpmChangeRescalesObjectsInSection()
        {
            setupEditorWithObjects();
            switchToTimingTab();

            AddUntilStep("rows loaded", () =>
                editor.ChildrenOfType<TimingPointRow>().Count() == 2);
            AddUntilStep("first point auto-selected", () =>
                timingList().SelectedGroup.Value?.Time == 0);

            // First point: BeatLength 500 → 250 (BPM 120 → 240). The note at 1000 sits on beat 2,
            // so it must land on 2 * 250 = 500. The note at 5000 is in the next section: unmoved.
            AddStep("set BPM to 240 via seam", () => settings().SetBpmAndCommit(240));

            AddAssert("note rescaled to 500", () =>
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 500) < 0.01));
            AddAssert("note in next section unmoved", () =>
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 5000) < 0.01));
        }

        [Test]
        public void TestAdjustmentToggleOffLeavesObjectsAlone()
        {
            setupEditorWithObjects();
            switchToTimingTab();

            AddUntilStep("first point auto-selected", () =>
                timingList().SelectedGroup.Value?.Time == 0);

            AddStep("disable move-objects toggle", () =>
                settings().AdjustObjectsOnTimingChange.Value = false);

            AddStep("set BPM to 240 via seam", () => settings().SetBpmAndCommit(240));

            AddAssert("BeatLength changed", () =>
                Math.Abs(editor.EditorChart.ControlPointInfo.TimingPoints.First().BeatLength - 250) < 0.01);
            AddAssert("objects unmoved", () =>
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 1000) < 0.01));
        }

        // ------------------------------------------------------------------
        // 12. "Use current time" moves the selected group to the playhead
        // ------------------------------------------------------------------

        [Test]
        public void TestUseCurrentTimeMovesGroup()
        {
            setupEditor(secondPointTime: 2000);
            switchToTimingTab();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("get change handler", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddUntilStep("rows loaded", () =>
                editor.ChildrenOfType<TimingPointRow>().Count() == 2);

            AddStep("select second point", () =>
                editor.ChildrenOfType<TimingPointRow>().ElementAt(1).TriggerClick());
            AddUntilStep("second selected", () =>
                timingList().SelectedGroup.Value?.Time == 2000);

            AddStep("seek to 3000", () =>
                editor.ChildrenOfType<EditorClock>().First().Seek(3000));
            AddUntilStep("clock at 3000", () =>
                Math.Abs(editor.ChildrenOfType<EditorClock>().First().CurrentTime - 3000) < 50);

            AddStep("really click Use current time", () =>
            {
                var button = editor.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                    .First(b => b.Text.ToString() == "Use current time");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddUntilStep("group moved to 3000", () =>
                editor.EditorChart.ControlPointInfo.GroupAt(3000) != null &&
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == 2);

            AddStep("undo", () => changeHandler.Undo());
            AddUntilStep("group back at 2000", () =>
                editor.EditorChart.ControlPointInfo.GroupAt(2000) != null &&
                editor.EditorChart.ControlPointInfo.GroupAt(3000) == null);
        }

        // ------------------------------------------------------------------
        // 14. Tap-timing adjust buttons
        // ------------------------------------------------------------------

        [Test]
        public void TestTapTimingAdjustButtons()
        {
            setupEditor(secondPointTime: 2000);
            switchToTimingTab();

            AddUntilStep("rows loaded", () =>
                editor.ChildrenOfType<TimingPointRow>().Count() == 2);
            AddStep("select second point", () =>
                editor.ChildrenOfType<TimingPointRow>().ElementAt(1).TriggerClick());
            AddUntilStep("second selected", () =>
                timingList().SelectedGroup.Value?.Time == 2000);

            scrollRightPanelToEnd();

            AddStep("really click tap offset -10", () =>
            {
                var button = editor.ChildrenOfType<RepeatNudgeButton>().First(b => b.Name == "tap-offset-minus10");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddUntilStep("group moved to 1990", () =>
                timingList().SelectedGroup.Value?.Time == 1990);

            AddStep("really click tap BPM +1", () =>
            {
                var button = editor.ChildrenOfType<RepeatNudgeButton>().First(b => b.Name == "tap-bpm-plus1");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddAssert("selected point BPM is 121", () =>
            {
                var tp = timingList().SelectedGroup.Value?.ControlPoints
                    .OfType<TimingControlPoint>().First();
                return tp != null && Math.Abs(tp.BPM - 121) < 0.01;
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

        // ------------------------------------------------------------------
        // 9. Time signature numerator textbox
        // ------------------------------------------------------------------

        [Test]
        public void TestTimeSignatureTextboxCommits()
        {
            setupEditor();
            switchToTimingTab();

            AddUntilStep("settings panel loaded", () =>
                editor.ChildrenOfType<TimingPointSettings>().Any());

            AddStep("set signature to 7", () =>
                editor.ChildrenOfType<TimingPointSettings>().First().SetSignatureAndCommit("7"));

            AddAssert("numerator is 7", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.First().TimeSignature.Numerator == 7);

            AddStep("try invalid signature 0", () =>
                editor.ChildrenOfType<TimingPointSettings>().First().SetSignatureAndCommit("0"));

            AddAssert("numerator still 7", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.First().TimeSignature.Numerator == 7);

            AddStep("try non-numeric signature", () =>
                editor.ChildrenOfType<TimingPointSettings>().First().SetSignatureAndCommit("abc"));

            AddAssert("numerator still 7 after garbage", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.First().TimeSignature.Numerator == 7);
        }

        // ------------------------------------------------------------------
        // 10. Omit first barline toggle
        // ------------------------------------------------------------------

        [Test]
        public void TestOmitFirstBarLineToggle()
        {
            setupEditor();
            switchToTimingTab();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("get change handler", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddUntilStep("checkbox loaded", () =>
                editor.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicCheckbox>()
                      .Any(c => c.LabelText.ToString() == "Omit first barline"));

            AddStep("really click the checkbox", () =>
            {
                var checkbox = editor.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicCheckbox>()
                    .First(c => c.LabelText.ToString() == "Omit first barline");
                input.MoveMouseTo(checkbox);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddAssert("point has OmitFirstBarLine set", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.First().OmitFirstBarLine);

            AddUntilStep("no-barline chip visible", () =>
                editor.ChildrenOfType<TimingPointRow.AttributeChip>()
                      .Single(c => c.Text.ToString() == "no barline").Alpha == 1);

            AddAssert("undo available", () => changeHandler.CanUndo.Value);
            AddStep("undo", () => changeHandler.Undo());

            AddUntilStep("OmitFirstBarLine cleared", () =>
                !editor.EditorChart.ControlPointInfo.TimingPoints.First().OmitFirstBarLine);
        }

        // ------------------------------------------------------------------
        // 15. BPM textbox stays live when the point changes outside the panel
        // ------------------------------------------------------------------

        [Test]
        public void TestBpmTextboxRefreshesOnExternalBpmChange()
        {
            setupEditor(); // 120 BPM
            switchToTimingTab();

            AddUntilStep("settings panel loaded", () =>
                editor.ChildrenOfType<TimingPointSettings>().Any());

            AddUntilStep("BPM textbox shows 120", () =>
                settings().BpmTextForTests == "120");

            scrollRightPanelToEnd();

            // Change the point's BPM from OUTSIDE the settings panel (tap-timing adjust button).
            AddStep("really click tap BPM +1", () =>
            {
                var button = editor.ChildrenOfType<RepeatNudgeButton>().First(b => b.Name == "tap-bpm-plus1");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddAssert("point BPM is 121", () =>
                Math.Abs(editor.EditorChart.ControlPointInfo.TimingPoints.First().BPM - 121) < 0.01);

            AddUntilStep("BPM textbox refreshed to 121", () =>
                settings().BpmTextForTests == "121");
        }

        // ------------------------------------------------------------------
        // 16. Settings panel controls are not overlapped by the tap control
        // ------------------------------------------------------------------

        [Test]
        public void TestSettingsControlsNotOverlappedByTapControl()
        {
            setupEditor();
            switchToTimingTab();

            AddUntilStep("settings and tap control loaded", () =>
                editor.ChildrenOfType<TimingPointSettings>().Any() &&
                editor.ChildrenOfType<TapTimingControl>().Any());

            // The settings panel must be tall enough for ALL its controls — the tap control
            // (drawn after it in the flow) must start below the last of them.
            AddUntilStep("omit checkbox sits above the tap control", () =>
            {
                var checkbox = editor.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicCheckbox>()
                    .First(c => c.LabelText.ToString() == "Omit first barline");
                var tap = editor.ChildrenOfType<TapTimingControl>().First();
                return checkbox.ScreenSpaceDrawQuad.AABBFloat.Bottom <= tap.ScreenSpaceDrawQuad.AABBFloat.Top + 1;
            });
        }

        /// <summary>
        /// The settings panel and the tap-timing panel stack in the same scroll column, so their
        /// content must share one left and right edge — the waveform comparison's right edge
        /// included. The two panels carried different insets, which left every tap-panel control
        /// sitting wider than the settings controls directly above them.
        /// </summary>
        [Test]
        public void TestRightColumnPanelsShareContentEdges()
        {
            setupEditor();
            switchToTimingTab();

            AddUntilStep("right column loaded", () =>
                editor.ChildrenOfType<TimingPointSettings>().Any()
                && editor.ChildrenOfType<TapButton>().Any()
                && editor.ChildrenOfType<WaveformComparisonDisplay>().Any());

            AddUntilStep("tap controls sit on the settings' content edges", () =>
            {
                // Aggregated over every text box, so these are the settings panel's content edges
                // whatever order its controls sit in — the narrow time-signature box can widen
                // neither bound, and the full-width boxes define both.
                var boxes = editor.ChildrenOfType<TimingPointSettings>().First()
                                  .ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicTextBox>()
                                  .ToList();

                float settingsLeft = boxes.Min(b => b.ScreenSpaceDrawQuad.AABBFloat.Left);
                float settingsRight = boxes.Max(b => b.ScreenSpaceDrawQuad.AABBFloat.Right);

                var tap = editor.ChildrenOfType<TapButton>().Single().ScreenSpaceDrawQuad.AABBFloat;

                return Math.Abs(tap.Left - settingsLeft) <= 0.5f
                       && Math.Abs(tap.Right - settingsRight) <= 0.5f;
            });

            AddAssert("waveform comparison sits on the same right edge", () =>
            {
                var tap = editor.ChildrenOfType<TapButton>().Single().ScreenSpaceDrawQuad.AABBFloat;
                var waveform = editor.ChildrenOfType<WaveformComparisonDisplay>().Single()
                                     .ScreenSpaceDrawQuad.AABBFloat;

                return Math.Abs(waveform.Right - tap.Right) <= 0.5f;
            });
        }

        // ------------------------------------------------------------------
        // 17. Timeline strip shown at the top of the Timing tab
        // ------------------------------------------------------------------

        [Test]
        public void TestTimingTabShowsTimelineStrip()
        {
            setupEditor();
            switchToTimingTab();

            AddUntilStep("timeline strip present in timing tab", () =>
                editor.ChildrenOfType<TimingTab>().First().ChildrenOfType<TimelineStrip>().Any());

            // The tab content must start below the strip — the strip must not draw over the point list.
            AddUntilStep("point list sits below the strip", () =>
            {
                var strip = editor.ChildrenOfType<TimingTab>().First().ChildrenOfType<TimelineStrip>().First();
                var list = editor.ChildrenOfType<TimingPointList>().First();
                return list.ScreenSpaceDrawQuad.AABBFloat.Top >= strip.ScreenSpaceDrawQuad.AABBFloat.Bottom - 1;
            });
        }

    }
}
