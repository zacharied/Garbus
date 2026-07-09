// TDD tests for Task 18: bottom bar + transport controls.
//
// Contract under test:
//   1. Space key toggles EditorClock.IsRunning (play/pause).
//   2. Speed 0.5 halves the AudioAdjustments aggregate tempo.
//   3. SummaryTimeline click seeks (raw/unsnapped).
//   4. Arrow keys move playhead by one beat-divisor step.
//   5. Z key seeks to start; X plays from start; C pause/resume; V seeks to end.
//   6. ↑/↓ keys change beatDivisor.
//   7. TimelineStrip drag seeks raw; snap-on-release is applied.
//
// Harness: wraps GarbusEditor in a ScreenStack inside a ManualInputManager so transport
// keys can be injected into the same input pipeline that GarbusEditor.OnKeyDown handles.

using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.BottomBar;
using Garbus.Game.Edit.Screens.Timeline;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneBottomBar : GarbusTestScene
    {
        private GarbusEditor editor = null!;
        private ManualInputManager input = null!;

        // Cached once waitForEditor() passes — safe because fields are set inside AddStep lambdas
        // which run on the game thread after SetUp.
        private EditorClock? editorClock;
        private BindableBeatDivisor? beatDivisor;

        [SetUp]
        public new void SetUp() => Schedule(() =>
        {
            editorClock = null;
            beatDivisor = null;

            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 }); // 120 BPM

            var chartFile = new ChartFile(chart);
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName() + ".garbus");
            chartFile.Save(tempPath);

            Child = input = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ScreenStack(editor = new GarbusEditor(chartFile)) { RelativeSizeAxes = Axes.Both },
            };
        });

        /// <summary>
        /// Wait for the editor and bottom bar to be fully loaded, then cache the clock and divisor.
        /// All tests call this before asserting clock state.
        /// </summary>
        private void waitForEditor()
        {
            AddUntilStep("editor + bottom bar loaded", () =>
                editor.IsLoaded && editor.ChildrenOfType<BottomBar>().Any());

            // Cache references inside a step so they execute after the editor is loaded.
            AddStep("cache clock + divisor", () =>
            {
                editorClock = editor.ChildrenOfType<EditorClock>().First();
                beatDivisor = editor.ChildrenOfType<TimelineStrip>().First().Dependencies.Get<BindableBeatDivisor>();
            });
        }

        // ------------------------------------------------------------------
        // 1. Space key toggles clock.IsRunning
        // ------------------------------------------------------------------

        [Test]
        public void TestSpaceTogglesPlayback()
        {
            waitForEditor();

            AddAssert("clock stopped initially", () => !editorClock!.IsRunning);

            AddStep("press space", () => input.Key(Key.Space));
            AddUntilStep("clock running", () => editorClock!.IsRunning);

            AddStep("press space again", () => input.Key(Key.Space));
            AddUntilStep("clock stopped", () => !editorClock!.IsRunning);
        }

        // ------------------------------------------------------------------
        // 2. Speed 0.5 halves AudioAdjustments aggregate tempo
        // ------------------------------------------------------------------

        [Test]
        public void TestSpeedControlHalvesTempo()
        {
            waitForEditor();

            AddStep("select speed 0.5 via tab control", () =>
            {
                var playback = editor.ChildrenOfType<PlaybackControl>().First();
                var tabControl = playback.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicTabControl<double>>().First();
                tabControl.Current.Value = 0.5;
            });

            AddAssert("aggregate tempo ≈ 0.5", () =>
                System.Math.Abs(editorClock!.AudioAdjustments.AggregateTempo.Value - 0.5) < 0.01);

            AddStep("reset speed to 1.0", () =>
            {
                var playback = editor.ChildrenOfType<PlaybackControl>().First();
                var tabControl = playback.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicTabControl<double>>().First();
                tabControl.Current.Value = 1.0;
            });

            AddAssert("aggregate tempo ≈ 1.0", () =>
                System.Math.Abs(editorClock!.AudioAdjustments.AggregateTempo.Value - 1.0) < 0.01);
        }

        // ------------------------------------------------------------------
        // 3. SummaryTimeline click seeks (raw, unsnapped)
        // ------------------------------------------------------------------

        [Test]
        public void TestSummaryTimelineClickSeeks()
        {
            waitForEditor();

            AddUntilStep("summary timeline loaded", () =>
                editor.ChildrenOfType<SummaryTimeline>().Any());

            AddStep("seek clock to 0", () => editorClock!.Seek(0));

            // Click the SummaryTimeline at ~50% width → should seek to ~half of track length.
            AddStep("click middle of summary timeline", () =>
            {
                var summary = editor.ChildrenOfType<SummaryTimeline>().First();
                var centre = summary.ToScreenSpace(new Vector2(summary.DrawWidth * 0.5f, summary.DrawHeight * 0.5f));
                input.MoveMouseTo(centre);
                input.Click(MouseButton.Left);
            });

            AddUntilStep("clock seeked to roughly half track", () =>
            {
                double halfTrack = editorClock!.TrackLength / 2;
                return System.Math.Abs(editorClock.CurrentTime - halfTrack) < halfTrack * 0.15 + 200;
            });
        }

        // ------------------------------------------------------------------
        // 4. Arrow keys move playhead by one beat-divisor step
        // ------------------------------------------------------------------

        [Test]
        public void TestArrowKeysSeekByDivisorStep()
        {
            waitForEditor();

            AddStep("seek to 0 and stop", () => { editorClock!.Stop(); editorClock.Seek(0); });

            double capturedTime = 0;
            AddStep("capture position", () => capturedTime = editorClock!.CurrentTime);

            // BeatLength=500ms, divisor=4 → one step = 125ms.
            AddStep("press right arrow", () => input.Key(Key.Right));

            AddUntilStep("moved forward by at least 50ms", () =>
                editorClock!.CurrentTime > capturedTime + 50);

            AddStep("capture new position", () => capturedTime = editorClock!.CurrentTime);
            AddStep("press left arrow", () => input.Key(Key.Left));

            AddUntilStep("moved backward by at least 50ms", () =>
                editorClock!.CurrentTime < capturedTime - 50);
        }

        // ------------------------------------------------------------------
        // 5. Z/X/C/V transport keys
        // ------------------------------------------------------------------

        [Test]
        public void TestZSeeksToStart()
        {
            waitForEditor();

            AddStep("seek to 5000", () => editorClock!.Seek(5000));
            AddStep("press Z", () => input.Key(Key.Z));
            AddUntilStep("at start (< 100ms)", () => editorClock!.CurrentTime < 100);
        }

        [Test]
        public void TestXPlaysFromStart()
        {
            waitForEditor();

            AddStep("seek to 5000", () => editorClock!.Seek(5000));
            AddStep("press X", () => input.Key(Key.X));
            AddUntilStep("clock running from near start", () => editorClock!.IsRunning && editorClock.CurrentTime < 2000);
            AddStep("stop clock", () => editorClock!.Stop());
        }

        [Test]
        public void TestCPausesAndResumes()
        {
            waitForEditor();

            AddAssert("stopped initially", () => !editorClock!.IsRunning);
            AddStep("press C to start", () => input.Key(Key.C));
            AddUntilStep("running", () => editorClock!.IsRunning);
            AddStep("press C to stop", () => input.Key(Key.C));
            AddUntilStep("stopped", () => !editorClock!.IsRunning);
        }

        [Test]
        public void TestVSeeksToEnd()
        {
            waitForEditor();

            AddStep("seek to start", () => editorClock!.Seek(0));
            AddStep("press V", () => input.Key(Key.V));
            AddUntilStep("at end (within 200ms)", () =>
                System.Math.Abs(editorClock!.CurrentTime - editorClock.TrackLength) < 200);
        }

        // ------------------------------------------------------------------
        // 6. ↑/↓ change beat divisor
        // ------------------------------------------------------------------

        [Test]
        public void TestUpDownChangesDivisor()
        {
            waitForEditor();

            int startDivisor = 0;
            AddStep("capture initial divisor", () => startDivisor = beatDivisor!.Value);

            AddStep("press down (increase divisor)", () => input.Key(Key.Down));
            AddUntilStep("divisor increased", () => beatDivisor!.Value > startDivisor);

            AddStep("capture new divisor", () => startDivisor = beatDivisor!.Value);
            AddStep("press up (decrease divisor)", () => input.Key(Key.Up));
            AddUntilStep("divisor decreased", () => beatDivisor!.Value < startDivisor);
        }

        // ------------------------------------------------------------------
        // 7. TimelineStrip raw-drag / snap-on-release (carried from Task 17)
        //
        // The TimelineStrip drag mechanism: scroll position → time via TimeAtPosition(Current).
        // We validate the fix by seeking to a non-snapped position, then simulating an end-of-drag
        // (releasing the mouse), verifying that the snap is applied via SeekSnapped on release.
        // ------------------------------------------------------------------

        [Test]
        public void TestTimelineStripSnapOnRelease()
        {
            waitForEditor();

            AddUntilStep("timeline strip loaded", () => editor.ChildrenOfType<TimelineStrip>().Any());

            // Seek to a non-snapped position (e.g., 333ms — not a multiple of 125ms).
            // This simulates where the raw-drag seek might leave the playhead.
            AddStep("seek to non-snapped position 333ms", () =>
            {
                editorClock!.Stop();
                editorClock.Seek(333);
            });

            AddWaitStep("wait one frame for seek to settle", 2);

            // Now simulate a drag end on the timeline strip. The endUserDrag() logic calls
            // SeekSnapped(editorClock.CurrentTime) which should snap 333ms → 375ms (3×125ms).
            AddStep("mouse-press then release on timeline strip (simulates drag end)", () =>
            {
                var strip = editor.ChildrenOfType<TimelineStrip>().First();
                // Position the mouse somewhere on the strip so OnMouseDown is triggered.
                float x = strip.DrawWidth * 0.10f;
                float y = strip.DrawHeight * 0.5f;
                input.MoveMouseTo(strip.ToScreenSpace(new Vector2(x, y)));
                input.PressButton(MouseButton.Left);
            });

            AddWaitStep("wait a frame", 2);

            AddStep("release mouse — snap should be applied", () =>
                input.ReleaseButton(MouseButton.Left));

            AddWaitStep("wait for snap to settle", 3);

            // After release, position should be snapped to a 125ms multiple.
            AddAssert("position snapped after release", () =>
            {
                const double snapMs = 500.0 / 4; // BeatLength / divisor
                double t = editorClock!.CurrentTime;
                double remainder = t % snapMs;
                return remainder < 15 || remainder > snapMs - 15;
            });
        }
    }
}
