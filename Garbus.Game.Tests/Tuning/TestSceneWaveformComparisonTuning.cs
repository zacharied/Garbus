// Interactive tuning scene for the Timing tab's waveform comparison display: the editor is opened on
// a song with real audio so the slices have something to draw, and every knob that shapes how the
// slices read — the per-row window, the BPM they step by, the playhead position, playback and the
// hover/lock interaction — is a live control in the test browser's step sidebar.
// [Explicit] so it never runs in a headless "run all"; pick it in the test browser.

using System.Linq;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Timing;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Tests.Editor;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Tuning
{
    [TestFixture]
    [Explicit]
    public partial class TestSceneWaveformComparisonTuning : GarbusTestScene
    {
        [Resolved]
        private Storage storage { get; set; } = null!;

        private GarbusEditor editor = null!;

        // Defaults mirror WaveformComparisonDisplay.VisibleWidth and the seeded song's 120 BPM.
        private float visibleWidth = 300;
        private float bpm = 120;

        public TestSceneWaveformComparisonTuning()
        {
            AddSliderStep("row window (ms)", 50f, 2000f, visibleWidth, v =>
            {
                visibleWidth = v;
                if (IsLoaded)
                    display().VisibleWidth = v;
            });

            AddSliderStep("bpm", 30f, 300f, bpm, v =>
            {
                bpm = v;
                if (IsLoaded)
                    setBpm(v);
            });

            AddSliderStep("seek (ms)", 0f, 60000f, 0f, v =>
            {
                if (IsLoaded)
                    clock().Seek(v);
            });
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("open editor on a song with audio", () =>
            {
                var songFile = TestSongWithAudio.Create(storage, "waveform-comparison-tuning", 60000.0 / bpm);
                editor = new GarbusEditor(songFile);
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both };
            });

            AddUntilStep("editor loaded", () => editor.IsLoaded);
            AddStep("switch to Timing tab", () => editor.Tab.Value = EditorTab.Timing);
            AddUntilStep("display loaded", () =>
                editor.ChildrenOfType<WaveformComparisonDisplay>().Any() && display().IsLoaded);

            AddStep("apply window", () => display().VisibleWidth = visibleWidth);

            AddToggleStep("play", playing =>
            {
                if (playing)
                    clock().Start();
                else
                    clock().Stop();
            });

            AddStep("toggle lock", () => display().TriggerClick());
        }

        private WaveformComparisonDisplay display() =>
            editor.ChildrenOfType<WaveformComparisonDisplay>().Single();

        private EditorClock clock() => editor.ChildrenOfType<EditorClock>().First();

        private void setBpm(double value)
        {
            var timingPoint = editor.EditorChart.ControlPointInfo.TimingPoints.FirstOrDefault();

            if (timingPoint != null)
                timingPoint.BeatLength = 60000.0 / value;
        }
    }
}
