// TDD tests for Task 19: Test mode (editor → PlayScreen push and resume).
//
// Headless-track strategy: in headless tests there is no real audio file on disk,
// so ChartFile.GetTrackStore returns null and the editor normally shows a disabled
// Test button. For the push-path tests we inject a TrackVirtual via
// GarbusEditor.TrackFactoryOverride — an internal seam that bypasses the
// directory-store path without touching production logic.
//
// Contract under test:
//   1. F5 pushes PlayScreen; its chart has the same object count but zero shared
//      references (ReferenceEquals false on first object).
//   2. Start time ≈ editorClock.CurrentTime − 1500 (clamped ≥ 0).
//   3. Exiting PlayScreen seeks the editor clock to PlayScreen.ExitTime.
//   4. Test button disabled when the chart has no real track (TrackVirtual).

using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.BottomBar;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneTestMode : GarbusTestScene
    {
        private ScreenStack stack = null!;
        private GarbusEditor editor = null!;
        private ManualInputManager input = null!;

        // --- Helpers ---

        private static GarbusChart buildChart(int hitObjectCount = 3)
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            for (int i = 0; i < hitObjectCount; i++)
                chart.HitObjects.Add(new CardinalNote { StartTime = 1000 + i * 500.0, AngleDeg = i * 90 });

            return chart;
        }

        /// <summary>
        /// Create a saved ChartFile (temp path) and set the TrackFactoryOverride on the editor
        /// so that the push-path tests can proceed headlessly.
        /// </summary>
        private GarbusEditor buildEditorWithVirtualTrack(GarbusChart chart)
        {
            var chartFile = new ChartFile(chart);
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                System.IO.Path.GetRandomFileName() + ".garbus");
            chartFile.Save(tempPath);

            var ed = new GarbusEditor(chartFile);
            // Inject a fresh TrackVirtual for every test-mode push — bypasses the need for a real audio file.
            ed.TrackFactoryOverride = () => new TrackVirtual(60_000);
            return ed;
        }

        private void setupEditorWithVirtualTrack(int hitObjectCount = 3) => Schedule(() =>
        {
            var chart = buildChart(hitObjectCount);
            editor = buildEditorWithVirtualTrack(chart);

            Child = input = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = stack = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
            };
        });

        private void waitForEditor() =>
            AddUntilStep("editor loaded", () => editor.IsLoaded && editor.ChildrenOfType<BottomBar>().Any());

        // ------------------------------------------------------------------
        // 1. F5 pushes PlayScreen with a deep-cloned chart (no shared refs)
        // ------------------------------------------------------------------

        [Test]
        public void TestF5PushesPlayScreen()
        {
            setupEditorWithVirtualTrack(hitObjectCount: 3);
            waitForEditor();

            // Position the editor clock somewhere non-zero.
            AddStep("seek editor to 3000 ms", () => editor.ChildrenOfType<EditorClock>().First().Seek(3000));

            AddStep("press F5", () => input.Key(osuTK.Input.Key.F5));

            AddUntilStep("PlayScreen pushed", () => stack.CurrentScreen is PlayScreen);
        }

        [Test]
        public void TestPushedChartHasSameObjectCount()
        {
            setupEditorWithVirtualTrack(hitObjectCount: 3);
            waitForEditor();

            AddStep("press F5", () => input.Key(osuTK.Input.Key.F5));
            AddUntilStep("PlayScreen pushed", () => stack.CurrentScreen is PlayScreen);

            AddAssert("cloned chart has same object count", () =>
            {
                var ps = (PlayScreen)stack.CurrentScreen;
                return ps.Chart!.HitObjects.Count == 3;
            });
        }

        [Test]
        public void TestPushedChartHasNoSharedReferences()
        {
            setupEditorWithVirtualTrack(hitObjectCount: 3);
            waitForEditor();

            AddStep("press F5", () => input.Key(osuTK.Input.Key.F5));
            AddUntilStep("PlayScreen pushed", () => stack.CurrentScreen is PlayScreen);

            AddAssert("first object is a different reference", () =>
            {
                var ps = (PlayScreen)stack.CurrentScreen;
                var editorFirst = editor.EditorChart.HitObjects.First();
                var playFirst = ps.Chart!.HitObjects.First();
                return !ReferenceEquals(editorFirst, playFirst);
            });
        }

        /// <summary>
        /// ISSUES.md: "charts don't make it to the test play scene; 0 objects show up". The chart
        /// DATA reaches the PlayScreen (tests above), so pin the display path: with gameplay running
        /// from t=0 and objects at 1000/1500/2000, alive drawables must appear in the playfield.
        /// </summary>
        [Test]
        public void TestPushedPlayScreenShowsObjects()
        {
            setupEditorWithVirtualTrack(hitObjectCount: 3);
            waitForEditor();

            AddStep("press F5", () => input.Key(osuTK.Input.Key.F5));
            AddUntilStep("PlayScreen pushed and loaded", () => stack.CurrentScreen is PlayScreen ps && ps.IsLoaded);

            AddUntilStep("objects become visible", () =>
                ((PlayScreen)stack.CurrentScreen).ChildrenOfType<Game.UI.GarbusPlayfield>().SingleOrDefault()
                    ?.AllHitObjects.Any(d => d.IsAlive) == true);

            // The playfield must run on the gameplay clock, not the ambient (wall-time) clock —
            // otherwise object lifetimes compare against app-session time and nothing ever shows
            // once the app has been open longer than the chart.
            AddAssert("playfield runs on gameplay clock", () =>
            {
                var ps = (PlayScreen)stack.CurrentScreen;
                var pf = ps.ChildrenOfType<Game.UI.GarbusPlayfield>().Single();
                var clock = ps.ChildrenOfType<Timing.MasterGameplayClockContainer>().Single();
                return System.Math.Abs(pf.Time.Current - clock.CurrentTime) < 100;
            });
        }

        /// <summary>
        /// ISSUES.md: "no way to exit test play" — Escape must return to the editor — and the
        /// playtest's track must stop on exit rather than keep playing under the editor.
        /// </summary>
        [Test]
        public void TestEscapeExitsToEditorAndStopsAudio()
        {
            Track? pushedTrack = null;

            Schedule(() =>
            {
                var chart = buildChart(3);
                editor = buildEditorWithVirtualTrack(chart);
                editor.TrackFactoryOverride = () => pushedTrack = new TrackVirtual(60_000);

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = stack = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
                };
            });
            waitForEditor();

            AddStep("press F5", () => input.Key(osuTK.Input.Key.F5));
            AddUntilStep("PlayScreen pushed and loaded", () => stack.CurrentScreen is PlayScreen ps && ps.IsLoaded);
            AddUntilStep("track playing", () => pushedTrack?.IsRunning == true);

            AddStep("press Escape", () => input.Key(osuTK.Input.Key.Escape));
            AddUntilStep("back at editor", () => stack.CurrentScreen is GarbusEditor);
            AddUntilStep("track stopped", () => pushedTrack?.IsRunning == false);
        }

        // ------------------------------------------------------------------
        // 2. Start time ≈ editorClock.CurrentTime − 1500 (clamped ≥ 0)
        // ------------------------------------------------------------------

        [Test]
        public void TestStartTimeOffsetFromEditorClock()
        {
            setupEditorWithVirtualTrack(hitObjectCount: 3);
            waitForEditor();

            double capturedEditorTime = 0;

            AddStep("seek editor to 5000", () =>
            {
                var clock = editor.ChildrenOfType<EditorClock>().First();
                clock.Seek(5000);
                capturedEditorTime = clock.CurrentTime;
            });

            AddStep("press F5", () => input.Key(osuTK.Input.Key.F5));
            AddUntilStep("PlayScreen pushed", () => stack.CurrentScreen is PlayScreen);

            AddAssert("gameplay start time ≈ editorTime − 1500 (within 50ms)", () =>
            {
                var ps = (PlayScreen)stack.CurrentScreen;
                double expected = System.Math.Max(0, capturedEditorTime - 1500);
                return System.Math.Abs(ps.StartTime - expected) < 50;
            });
        }

        [Test]
        public void TestStartTimeClampsToZero()
        {
            setupEditorWithVirtualTrack(hitObjectCount: 3);
            waitForEditor();

            AddStep("seek editor to 0", () => editor.ChildrenOfType<EditorClock>().First().Seek(0));
            AddStep("press F5", () => input.Key(osuTK.Input.Key.F5));
            AddUntilStep("PlayScreen pushed", () => stack.CurrentScreen is PlayScreen);

            AddAssert("start time is 0 (clamped)", () => ((PlayScreen)stack.CurrentScreen).StartTime == 0);
        }

        // ------------------------------------------------------------------
        // 3. Exiting PlayScreen seeks editor clock to ExitTime
        // ------------------------------------------------------------------

        [Test]
        public void TestExitingPlayScreenSeeksEditorClock()
        {
            setupEditorWithVirtualTrack(hitObjectCount: 3);
            waitForEditor();

            AddStep("seek editor to 5000", () => editor.ChildrenOfType<EditorClock>().First().Seek(5000));
            AddStep("press F5", () => input.Key(osuTK.Input.Key.F5));
            AddUntilStep("PlayScreen pushed and loaded", () => stack.CurrentScreen is PlayScreen ps && ps.IsLoaded);

            PlayScreen? capturedPlayScreen = null;
            AddStep("capture PlayScreen and exit", () =>
            {
                capturedPlayScreen = (PlayScreen)stack.CurrentScreen;
                capturedPlayScreen.Exit();
                // ExitTime is set in OnExiting before the transition completes.
            });

            AddUntilStep("editor is current screen again", () => stack.CurrentScreen is GarbusEditor);

            AddAssert("editor clock seeked to PlayScreen.ExitTime", () =>
            {
                if (capturedPlayScreen == null || capturedPlayScreen.ExitTime == null) return false;
                var clock = editor.ChildrenOfType<EditorClock>().First();
                return System.Math.Abs(clock.CurrentTime - capturedPlayScreen.ExitTime!.Value) < 200;
            });
        }

        // ------------------------------------------------------------------
        // 4. Test button disabled when track is TrackVirtual (no real track)
        // ------------------------------------------------------------------

        [Test]
        public void TestButtonDisabledWhenTrackVirtual()
        {
            // Build an editor WITHOUT TrackFactoryOverride — the default unsaved ChartFile
            // will use TrackVirtual, so Test should be disabled.
            Schedule(() =>
            {
                var chart = buildChart(hitObjectCount: 1);
                var chartFile = new ChartFile(chart);   // no saved path → GetTrackStore returns null → TrackVirtual

                editor = new GarbusEditor(chartFile);   // no TrackFactoryOverride

                Child = stack = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both };
            });

            AddUntilStep("editor + bottom bar loaded",
                () => editor.IsLoaded && editor.ChildrenOfType<BottomBar>().Any());

            AddAssert("Test button is disabled", () =>
            {
                var testButton = editor.ChildrenOfType<TestButton>().FirstOrDefault();
                return testButton != null && !testButton.Enabled.Value;
            });
        }

        [Test]
        public void TestButtonEnabledWhenTrackOverrideSet()
        {
            setupEditorWithVirtualTrack(hitObjectCount: 1);
            waitForEditor();

            AddAssert("Test button is enabled", () =>
            {
                var testButton = editor.ChildrenOfType<TestButton>().FirstOrDefault();
                return testButton != null && testButton.Enabled.Value;
            });
        }
    }
}
