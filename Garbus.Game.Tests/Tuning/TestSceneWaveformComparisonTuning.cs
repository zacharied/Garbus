// Interactive tuning scene for the Timing tab's waveform comparison display: the editor is opened on
// a song with real audio so the slices have something to draw, and the knobs that shape how they read
// — the per-row window, the BPM they step by, the playhead position and playback — are live controls
// in the test browser's step sidebar. The hover/lock interaction is driven by pointing at the display
// itself rather than by a step, because the browser runs every step on load and a blind lock toggle
// would leave the scene locked before it had been looked at.
// [Explicit] so it never runs in a headless "run all"; pick it in the test browser.
//
// Slider steps are added in the constructor, so they run BEFORE the setup steps that build the
// editor — every callback has to tolerate a scene with no editor (or no display inside it) yet, and
// gets re-applied from `applyAll` once there is one.

using System.Linq;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Timing;
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

        private GarbusEditor? editor;

        // Defaults mirror WaveformComparisonDisplay.VisibleWidth and the seeded song's 120 BPM.
        private float visibleWidth = 300;
        private float bpm = 120;
        private float seekTime;

        public TestSceneWaveformComparisonTuning()
        {
            AddSliderStep("row window (ms)", 50f, 2000f, visibleWidth, v =>
            {
                visibleWidth = v;
                applyWindow();
            });

            AddSliderStep("bpm", 30f, 300f, bpm, v =>
            {
                bpm = v;
                applyBpm();
            });

            AddSliderStep("seek (ms)", 0f, 60000f, seekTime, v =>
            {
                seekTime = v;
                applySeek();
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

            AddUntilStep("editor loaded", () => editor?.IsLoaded == true);
            AddStep("switch to Timing tab", () => editor!.Tab.Value = EditorTab.Timing);
            AddUntilStep("display loaded", () => display()?.IsLoaded == true);

            // The sliders ran against a scene with no display and did nothing; now there is one.
            AddStep("apply slider values", applyAll);

            AddToggleStep("play", playing =>
            {
                if (clock() is not { } editorClock)
                    return;

                if (playing)
                    editorClock.Start();
                else
                    editorClock.Stop();
            });
        }

        private void applyAll()
        {
            applyWindow();
            applyBpm();
            applySeek();
        }

        private void applyWindow()
        {
            if (display() is { } waveform)
                waveform.VisibleWidth = visibleWidth;
        }

        private void applyBpm()
        {
            var timingPoint = editor?.EditorChart.ControlPointInfo.TimingPoints.FirstOrDefault();

            if (timingPoint != null)
                timingPoint.BeatLength = 60000.0 / bpm;
        }

        private void applySeek() => clock()?.Seek(seekTime);

        private WaveformComparisonDisplay? display() =>
            editor?.ChildrenOfType<WaveformComparisonDisplay>().SingleOrDefault();

        private EditorClock? clock() =>
            editor?.ChildrenOfType<EditorClock>().FirstOrDefault();
    }
}
