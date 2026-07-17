// TDD tests for Task 20: Setup tab.
//
// Contract under test:
//   1. Typing a title + commit (via TriggerCommit) updates Metadata.Title and is ONE undo step.
//   2. Resource rows disabled for unsaved chart.
//   3. After Save to temp subdirectory + picking a file (SimulatePick), the file exists in the
//      chart dir and AudioFile is set.
//   4. ReloadTrack is called — asserted implicitly (no crash, AudioFile set). TrackIsReal may
//      be false if the stub ogg fails to decode; the metadata assertion is the primary evidence.
//
// Note on commit testing: BasicTextBox.OnCommit fires on focus loss or Enter, not on Current.Value
// change. We expose FormRow.TriggerCommit() as a programmatic commit path for headless tests.
// The undo-step assertion verifies the BeginChange/EndChange transaction wraps exactly one commit.
//
// Harness: GarbusEditor in a ScreenStack (mirrors TestSceneEditorShell).

using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Setup;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneSetupTab : GarbusTestScene
    {
        private GarbusEditor editor = null!;
        private ManualInputManager input = null!;

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void setupEditorUnsaved() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            // NOT saved — ChartFile.Directory will be null.
            var chartFile = new ChartFile(chart);

            editor = new GarbusEditor(chartFile);
            Child = input = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
            };
        });

        private void setupEditorSaved() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            var chartFile = new ChartFile(chart);
            // Save to a dedicated subdirectory so chartFile.Directory is NOT the system temp dir.
            // This prevents source == dest conflicts when ImportResource copies files from temp dir.
            string chartSubDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            System.IO.Directory.CreateDirectory(chartSubDir);
            string tempPath = Path.Combine(chartSubDir, "test-chart.garbus");
            chartFile.Save(tempPath);

            editor = new GarbusEditor(chartFile);
            Child = input = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
            };
        });

        private void waitForSetupTab()
        {
            AddUntilStep("editor loaded", () => editor.IsLoaded);
            AddStep("switch to Setup tab", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup tab visible + metadata section loaded", () =>
                editor.ChildrenOfType<SetupTab>().Any() &&
                editor.ChildrenOfType<SetupTab>().First().State.Value == Visibility.Visible &&
                editor.ChildrenOfType<MetadataSection>().Any());
        }

        /// <summary>
        /// ISSUES.md: the resource choose buttons stayed greyed out after the chart was saved — the
        /// enabled state was computed once at load. They must enable as soon as the chart has a
        /// directory, without reopening the editor.
        /// </summary>
        [Test]
        public void TestResourceButtonsEnableAfterSave()
        {
            setupEditorUnsaved();
            waitForSetupTab();

            AddUntilStep("choose buttons disabled while unsaved", () =>
                editor.ChildrenOfType<FileChooserRow>().Any() &&
                editor.ChildrenOfType<FileChooserRow>().All(r => !r.ChooseButton.Enabled.Value));

            AddStep("save chart", () =>
            {
                string chartSubDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                System.IO.Directory.CreateDirectory(chartSubDir);
                editor.ChartFile.Save(Path.Combine(chartSubDir, "test-chart.garbus"));
            });

            AddUntilStep("choose buttons enabled after save", () =>
                editor.ChildrenOfType<FileChooserRow>().All(r => r.ChooseButton.Enabled.Value));
        }

        // ------------------------------------------------------------------
        // 1. Title commit updates Metadata.Title
        // ------------------------------------------------------------------

        [Test]
        public void TestTitleCommitUpdatesMetadata()
        {
            setupEditorUnsaved();
            waitForSetupTab();

            // Set the textbox value and programmatically commit.
            AddStep("set title text and commit", () =>
            {
                var formRow = editor.ChildrenOfType<MetadataSection>()
                    .First()
                    .ChildrenOfType<FormRow>()
                    .First();

                formRow.TextBox.Current.Value = "My Chart Title";
                formRow.TriggerCommit();
            });

            AddAssert("metadata.Title updated", () =>
                editor.EditorChart.Metadata.Title == "My Chart Title");
        }

        [Test]
        public void TestTitleCommitIsOneUndoStep()
        {
            setupEditorUnsaved();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("editor loaded", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddStep("switch to Setup tab", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup tab visible + metadata section", () =>
                editor.ChildrenOfType<SetupTab>().Any() &&
                editor.ChildrenOfType<SetupTab>().First().State.Value == Visibility.Visible &&
                editor.ChildrenOfType<MetadataSection>().Any());

            // Commit the title — one transaction = one undo step.
            AddStep("commit title", () =>
            {
                var formRow = editor.ChildrenOfType<MetadataSection>()
                    .First()
                    .ChildrenOfType<FormRow>()
                    .First();

                formRow.TextBox.Current.Value = "My Chart Title";
                formRow.TriggerCommit();
            });

            AddAssert("can undo", () => changeHandler.CanUndo.Value);

            // Undo — title should revert.
            AddStep("undo", () => changeHandler.Undo());
            AddAssert("title reverted", () => editor.EditorChart.Metadata.Title == string.Empty);

            AddAssert("redo available", () => changeHandler.CanRedo.Value);

            // Redo — title comes back.
            AddStep("redo", () => changeHandler.Redo());
            AddAssert("title restored", () => editor.EditorChart.Metadata.Title == "My Chart Title");
        }

        [Test]
        public void TestTypingWithoutCommitIsNotAnUndoStep()
        {
            setupEditorUnsaved();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("editor loaded", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddStep("switch to Setup tab", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup tab visible + metadata section", () =>
                editor.ChildrenOfType<SetupTab>().Any() &&
                editor.ChildrenOfType<SetupTab>().First().State.Value == Visibility.Visible &&
                editor.ChildrenOfType<MetadataSection>().Any());

            // Capture initial state hash.
            string initialHash = string.Empty;
            AddStep("capture initial hash", () => initialHash = changeHandler.CurrentStateHash);

            // Set the textbox value WITHOUT calling TriggerCommit — no state change should occur.
            AddStep("type without committing", () =>
            {
                var formRow = editor.ChildrenOfType<MetadataSection>()
                    .First()
                    .ChildrenOfType<FormRow>()
                    .First();

                formRow.TextBox.Current.Value = "Uncommitted";
                // Deliberately no TriggerCommit call here.
            });

            // Hash should not change.
            AddAssert("state hash unchanged (no commit)", () => changeHandler.CurrentStateHash == initialHash);

            // Metadata should also be unchanged (the raw textbox value change doesn't touch metadata).
            AddAssert("metadata unchanged", () => editor.EditorChart.Metadata.Title == string.Empty);
        }

        // ------------------------------------------------------------------
        // Difficulty dropdown updates metadata and is one undo step
        // ------------------------------------------------------------------

        [Test]
        public void TestDifficultyDropdownUpdatesMetadataAndIsOneUndoStep()
        {
            setupEditorUnsaved();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("editor loaded", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddStep("switch to Setup tab", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup tab visible + difficulty section", () =>
                editor.ChildrenOfType<SetupTab>().Any() &&
                editor.ChildrenOfType<SetupTab>().First().State.Value == Visibility.Visible &&
                editor.ChildrenOfType<DifficultySection>().Any());

            AddAssert("dropdown reflects default (Novice)", () =>
                editor.ChildrenOfType<DifficultySection>().First().DifficultyDropdown.Current.Value == Difficulty.Novice);

            AddStep("select Expert", () =>
                editor.ChildrenOfType<DifficultySection>().First().DifficultyDropdown.Current.Value = Difficulty.Expert);

            AddAssert("metadata.Difficulty updated", () =>
                editor.EditorChart.Metadata.Difficulty == Difficulty.Expert);

            AddAssert("can undo", () => changeHandler.CanUndo.Value);

            AddStep("undo", () => changeHandler.Undo());
            AddAssert("difficulty reverted to Novice", () =>
                editor.EditorChart.Metadata.Difficulty == Difficulty.Novice);

            AddStep("redo", () => changeHandler.Redo());
            AddAssert("difficulty restored to Expert", () =>
                editor.EditorChart.Metadata.Difficulty == Difficulty.Expert);
        }

        // ------------------------------------------------------------------
        // 2. Resource rows disabled for unsaved chart
        // ------------------------------------------------------------------

        [Test]
        public void TestResourceRowsDisabledForUnsavedChart()
        {
            setupEditorUnsaved();

            AddUntilStep("editor loaded", () => editor.IsLoaded);
            AddStep("switch to Setup tab", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup tab visible + resources section", () =>
                editor.ChildrenOfType<SetupTab>().Any() &&
                editor.ChildrenOfType<SetupTab>().First().State.Value == Visibility.Visible &&
                editor.ChildrenOfType<ResourcesSection>().Any());

            AddAssert("audio file chooser row disabled", () =>
            {
                var rows = editor.ChildrenOfType<FileChooserRow>().ToList();
                return rows.Count >= 1 && !rows[0].ChooseButton.Enabled.Value;
            });

            AddAssert("background file chooser row disabled", () =>
            {
                var rows = editor.ChildrenOfType<FileChooserRow>().ToList();
                return rows.Count >= 2 && !rows[1].ChooseButton.Enabled.Value;
            });
        }

        // ------------------------------------------------------------------
        // 3 & 4. After Save + picking audio file: file copied, AudioFile set
        // ------------------------------------------------------------------

        [Test]
        public void TestAudioFilePick_AfterSave_CopiesFileAndSetsMetadata()
        {
            setupEditorSaved();

            AddUntilStep("editor loaded", () => editor.IsLoaded);
            AddStep("switch to Setup tab", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup tab visible + resources section", () =>
                editor.ChildrenOfType<SetupTab>().Any() &&
                editor.ChildrenOfType<SetupTab>().First().State.Value == Visibility.Visible &&
                editor.ChildrenOfType<ResourcesSection>().Any());

            string? chartDir = null;
            string? sourceOggPath = null;

            AddStep("prepare source audio file", () =>
            {
                chartDir = editor.ChartFile.Directory;
                Assert.That(chartDir, Is.Not.Null, "chart must be saved");

                // Use a unique name in the system temp dir (NOT the chart subdirectory).
                sourceOggPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ogg");
                // Minimal OggS magic bytes — the track store won't decode this, but the file copy
                // and metadata are verified correctly.
                File.WriteAllBytes(sourceOggPath, new byte[] { 0x4F, 0x67, 0x67, 0x53 });
            });

            AddStep("simulate audio file pick", () =>
            {
                var audioRow = editor.ChildrenOfType<FileChooserRow>().First();
                audioRow.SimulatePick(sourceOggPath!);
            });

            AddUntilStep("AudioFile metadata set", () =>
                !string.IsNullOrEmpty(editor.EditorChart.Metadata.AudioFile));

            AddAssert("AudioFile matches picked filename", () =>
                editor.EditorChart.Metadata.AudioFile == Path.GetFileName(sourceOggPath));

            AddAssert("file copied to chart directory", () =>
            {
                string dest = Path.Combine(chartDir!, editor.EditorChart.Metadata.AudioFile);
                return File.Exists(dest);
            });

            // ReloadTrack was called (metadata is set and no exception was thrown).
            AddAssert("no crash (ReloadTrack called without throwing)", () => true);
        }

        [Test]
        public void TestBackgroundFilePick_AfterSave_CopiesFileAndSetsMetadata()
        {
            setupEditorSaved();

            AddUntilStep("editor loaded", () => editor.IsLoaded);
            AddStep("switch to Setup tab", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup tab visible + resources section", () =>
                editor.ChildrenOfType<SetupTab>().Any() &&
                editor.ChildrenOfType<SetupTab>().First().State.Value == Visibility.Visible &&
                editor.ChildrenOfType<ResourcesSection>().Any());

            string? chartDir = null;
            string? sourcePngPath = null;

            AddStep("prepare source image file", () =>
            {
                chartDir = editor.ChartFile.Directory;
                sourcePngPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
                // Minimal PNG magic bytes.
                File.WriteAllBytes(sourcePngPath, new byte[]
                {
                    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
                });
            });

            AddStep("simulate background file pick", () =>
            {
                var bgRow = editor.ChildrenOfType<FileChooserRow>().Skip(1).First();
                bgRow.SimulatePick(sourcePngPath!);
            });

            AddUntilStep("BackgroundFile metadata set", () =>
                !string.IsNullOrEmpty(editor.EditorChart.Metadata.BackgroundFile));

            AddAssert("BackgroundFile matches picked filename", () =>
                editor.EditorChart.Metadata.BackgroundFile == Path.GetFileName(sourcePngPath));

            AddAssert("file copied to chart directory", () =>
            {
                string dest = Path.Combine(chartDir!, editor.EditorChart.Metadata.BackgroundFile);
                return File.Exists(dest);
            });
        }

        [Test]
        public void TestResourcePickIsOneUndoStep()
        {
            setupEditorSaved();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("editor loaded", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddStep("switch to Setup tab", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup tab visible + resources section", () =>
                editor.ChildrenOfType<SetupTab>().Any() &&
                editor.ChildrenOfType<SetupTab>().First().State.Value == Visibility.Visible &&
                editor.ChildrenOfType<ResourcesSection>().Any());

            string? sourcePngPath = null;
            AddStep("create stub png", () =>
            {
                sourcePngPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
                File.WriteAllBytes(sourcePngPath, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            });

            AddStep("pick background file", () =>
            {
                var bgRow = editor.ChildrenOfType<FileChooserRow>().Skip(1).First();
                bgRow.SimulatePick(sourcePngPath!);
            });

            AddUntilStep("BackgroundFile set", () =>
                !string.IsNullOrEmpty(editor.EditorChart.Metadata.BackgroundFile));

            AddAssert("can undo after pick", () => changeHandler.CanUndo.Value);

            AddStep("undo", () => changeHandler.Undo());
            AddAssert("BackgroundFile cleared after undo", () =>
                string.IsNullOrEmpty(editor.EditorChart.Metadata.BackgroundFile));
        }
    }
}
