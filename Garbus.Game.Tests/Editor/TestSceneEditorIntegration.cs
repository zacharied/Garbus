// Full-loop integration test for the Phase 4 editor. Drives a real GarbusEditor shell (hosted in a
// ScreenStack, the pattern from TestSceneEditorShell) through the complete authoring cycle in ONE
// scene: new chart → place a cardinal + hold + seam-crossing slider → edit the metadata title via the
// real Setup FormRow → add a timing point at 10000 (BPM 180) → Save to a temp dir → assert the file on
// disk decodes to 3 objects + 2 timing points + the new title → Undo ×3 / Redo ×3 with object counts
// tracked → clipboard-clone a note → run the Verify tab and confirm the missing-audio issue is reported
// → switch through all four tabs without error.
//
// This is the cross-task wiring test: every editor subsystem (EditorChart, change handler, clipboard,
// ChartFile disk I/O, tabs, verify checks) is exercised against the same live editor instance.

using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Setup;
using Garbus.Game.Edit.Screens.Verify;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneEditorIntegration : GarbusTestScene
    {
        private GarbusEditor editor = null!;
        private string tempDir = null!;
        private string savePath = null!;

        private EditorChart editorChart => editor.EditorChart;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            // A brand-new chart, exactly as the File → New flow produces it: a single 120 BPM timing
            // point at 0 and no hit objects. Unsaved (FilePath == null) until we Save later.
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            System.IO.Directory.CreateDirectory(tempDir);
            savePath = Path.Combine(tempDir, "integration.garbus");

            Child = new ScreenStack(editor = new GarbusEditor(new ChartFile(chart))) { RelativeSizeAxes = Axes.Both };
        });

        [TearDown]
        public void TearDown()
        {
            if (tempDir != null && System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, recursive: true);
        }

        [Test]
        public void TestFullEditorLoop()
        {
            waitForEditor();

            // --- Place three hit objects: a cardinal note, a hold, and a seam-crossing slider. ---
            // Placement is done via EditorChart.Add (real-input placement is covered exhaustively by
            // TestSceneComposePlacement; here we exercise the shell + change-handler + save path).
            AddStep("place cardinal note", () => editorChart.Add(new CardinalNote { StartTime = 1000, AngleDeg = 270 }));
            AddStep("place hold note", () => editorChart.Add(new CardinalHoldNote { StartTime = 2000, AngleDeg = 90, Duration = 1000 }));
            AddStep("place seam-crossing slider", () => editorChart.Add(makeSeamCrossingSlider(3000)));
            AddAssert("three objects placed", () => editorChart.HitObjects.Count, () => Is.EqualTo(3));

            // --- Edit the metadata title through the real Setup tab UI. ---
            AddStep("switch to setup", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup visible", () => setupTab().State.Value == Visibility.Visible);
            AddStep("type + commit title", () =>
            {
                var titleRow = titleFormRow();
                titleRow.TextBox.Text = "Integration Title";
                titleRow.TriggerCommit();
            });
            AddAssert("title applied to chart", () => editorChart.Metadata.Title, () => Is.EqualTo("Integration Title"));

            // --- Add a timing point at 10000 ms / BPM 180, as an undoable change. ---
            AddStep("add timing point 10000 / 180 BPM", () =>
            {
                var handler = editor.ChangeHandlerForTests;
                handler.BeginChange();
                editorChart.ControlPointInfo.Add(10000, new TimingControlPoint { BeatLength = 60000.0 / 180.0 });
                editorChart.SaveState();
                handler.EndChange();
            });
            AddAssert("two timing points in memory", () => editorChart.ControlPointInfo.TimingPoints.Count, () => Is.EqualTo(2));

            // --- Save to disk and assert the on-disk file round-trips everything. ---
            AddStep("save to temp dir", () => editor.ChartFile.Save(savePath));
            AddAssert("file exists on disk", () => File.Exists(savePath));
            AddAssert("decoded file has 3 objects", () => decodeSaved().HitObjects.Count, () => Is.EqualTo(3));
            AddAssert("decoded file has 2 timing points", () => decodeSaved().ControlPointInfo.TimingPoints.Count, () => Is.EqualTo(2));
            AddAssert("decoded file has the title", () => decodeSaved().Metadata.Title, () => Is.EqualTo("Integration Title"));
            AddAssert("decoded file 180 BPM point present",
                () => decodeSaved().ControlPointInfo.TimingPoints.Any(t => t.Time == 10000 && System.Math.Abs(t.BeatLength - 60000.0 / 180.0) < 0.01));

            // --- Undo ×3 / Redo ×3, tracking object counts across the changes. ---
            // Undo history (most-recent last): [+cardinal] [+hold] [+slider] [title] [+timing].
            // Undo 1 → timing removed (still 3 objects). Undo 2 → title reverted (3 objects).
            // Undo 3 → slider removed (2 objects).
            AddStep("undo #1 (timing)", () => editor.ChangeHandlerForTests.Undo());
            AddAssert("still 3 objects, 1 timing point",
                () => editorChart.HitObjects.Count == 3 && editorChart.ControlPointInfo.TimingPoints.Count == 1);
            AddStep("undo #2 (title)", () => editor.ChangeHandlerForTests.Undo());
            AddAssert("title reverted, 3 objects",
                () => editorChart.HitObjects.Count == 3 && editorChart.Metadata.Title == string.Empty);
            AddStep("undo #3 (slider)", () => editor.ChangeHandlerForTests.Undo());
            AddAssert("2 objects after slider undo", () => editorChart.HitObjects.Count, () => Is.EqualTo(2));

            AddStep("redo #1 (slider)", () => editor.ChangeHandlerForTests.Redo());
            AddAssert("3 objects after slider redo", () => editorChart.HitObjects.Count, () => Is.EqualTo(3));
            AddStep("redo #2 (title)", () => editor.ChangeHandlerForTests.Redo());
            AddAssert("title restored", () => editorChart.Metadata.Title, () => Is.EqualTo("Integration Title"));
            AddStep("redo #3 (timing)", () => editor.ChangeHandlerForTests.Redo());
            AddAssert("2 timing points restored", () => editorChart.ControlPointInfo.TimingPoints.Count, () => Is.EqualTo(2));
            AddAssert("back to 3 objects", () => editorChart.HitObjects.Count, () => Is.EqualTo(3));

            // --- Clipboard clone: select a note, clone it, expect a fourth object. ---
            AddStep("select the cardinal note", () =>
            {
                editorChart.SelectedHitObjects.Clear();
                editorChart.SelectedHitObjects.Add(editorChart.HitObjects.OfType<CardinalNote>().First());
            });
            AddStep("clone via clipboard", () => editor.ClipboardForTests.Clone());
            AddAssert("four objects after clone", () => editorChart.HitObjects.Count, () => Is.EqualTo(4));

            // --- Verify tab reports the missing-audio issue (no audio file set on this chart). ---
            AddStep("switch to verify", () => editor.Tab.Value = EditorTab.Verify);
            AddUntilStep("verify visible", () => verifyTab().State.Value == Visibility.Visible);
            AddStep("click Refresh", () => clickRefresh());
            AddUntilStep("audio-missing issue reported", () => issueMessages().Any(m => m.Contains("audio", System.StringComparison.OrdinalIgnoreCase)));

            // --- Switch through all four tabs without error. ---
            AddStep("→ Setup", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup visible", () => setupTab().State.Value == Visibility.Visible);
            AddStep("→ Compose", () => editor.Tab.Value = EditorTab.Compose);
            AddUntilStep("compose visible", () => composeTab().State.Value == Visibility.Visible);
            AddStep("→ Timing", () => editor.Tab.Value = EditorTab.Timing);
            AddUntilStep("timing visible", () => timingTab().State.Value == Visibility.Visible);
            AddStep("→ Verify", () => editor.Tab.Value = EditorTab.Verify);
            AddUntilStep("verify visible again", () => verifyTab().State.Value == Visibility.Visible);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void waitForEditor() =>
            AddUntilStep("editor loaded", () => editor.IsLoaded && editor.ChildrenOfType<ComposeTab>().Any());

        /// <summary>A slider whose head sits near the wrap seam and whose node sweeps across it.</summary>
        private static SliderBody makeSeamCrossingSlider(double startTime)
        {
            // The seam is the diagonal quadrant boundary (315°) opposite the grid's left edge (135°).
            // Head at 300° with a +90° node sweep carries the path across 315° (300 → 30).
            var path = new GarbusPath { ControlPoints = new osu.Framework.Bindables.BindableList<GarbusPathControlPoint>() };
            path.ControlPoints.Add(new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 90 });

            return new SliderBody
            {
                StartTime = startTime,
                AngleDeg = 300,
                Side = HorizontalDirection.Right,
                Path = path,
            };
        }

        private GarbusChart decodeSaved() => GarbusChartSerializer.Decode(File.ReadAllText(savePath));

        private SetupTab setupTab() => editor.ChildrenOfType<SetupTab>().Single();
        private ComposeTab composeTab() => editor.ChildrenOfType<ComposeTab>().Single();
        private TimingTab timingTab() => editor.ChildrenOfType<TimingTab>().Single();
        private VerifyTab verifyTab() => editor.ChildrenOfType<VerifyTab>().Single();

        /// <summary>The first FormRow in the Setup tab is the Title row.</summary>
        private FormRow titleFormRow() => setupTab().ChildrenOfType<FormRow>().First();

        private void clickRefresh()
        {
            var refresh = verifyTab().ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                                     .Single(b => b.Text == "Refresh");
            refresh.Action?.Invoke();
        }

        private System.Collections.Generic.IEnumerable<string> issueMessages() =>
            verifyTab().ChildrenOfType<SpriteText>().Select(t => t.Text.ToString());
    }
}
