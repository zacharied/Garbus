// Visual + integration test for song select: the library scans bundled charts, the view toggle
// flips grouping (and persists), selecting a chart drives the audio preview, and launching pushes a
// PlayScreen for the chosen chart.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using Garbus.Game.Configuration;
using Garbus.Game.Screens;
using Garbus.Game.Screens.SongSelect;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osuTK.Input;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSongSelect : GarbusTestScene
    {
        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        private ManualInputManager input = null!;
        private ScreenStack stack = null!;
        private SongSelectScreen songSelect = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            // The bundled resource chart ("test track") is the only chart guaranteed to exist. The
            // arrow-key navigation test needs at least two to prove index movement, so seed a couple
            // of directory-backed charts the screen's own DirectoryChartSource will pick up before it
            // scans (this step runs — and completes its synchronous file IO — before the "push song
            // select" step below constructs the screen and triggers its BDL scan). Titled "ZZ ..." so
            // they sort (OrdinalIgnoreCase) after "test track" and never become the first/selected
            // group in the other tests below (which assume the bundled chart is first). Their audio
            // files don't exist on disk, which is fine — Select()/startPreview already swallow that;
            // only Launch() would care, and no test launches one of these seeded charts.
            AddStep("seed extra charts", () =>
            {
                string root = storage.GetStorageForDirectory("charts").GetFullPath(string.Empty);

                for (int i = 1; i <= 2; i++)
                {
                    string dir = Path.Combine(root, $"zz-extra-song-{i}");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, "chart.garbus");

                    if (!File.Exists(path))
                    {
                        var chart = new GarbusChart { Metadata = { Title = $"ZZ Extra Song {i}", Artist = "Test", Level = i, AudioFile = "missing.ogg" } };
                        File.WriteAllText(path, GarbusChartSerializer.Encode(chart));
                    }
                }
            });

            AddStep("push song select", () =>
                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = stack = new ScreenStack(songSelect = new SongSelectScreen()) { RelativeSizeAxes = Axes.Both },
                });
            AddUntilStep("loaded", () => songSelect.IsLoaded && songSelect.Groups != null);
        }

        [Test]
        public void TestBundledChartAppears()
        {
            // The bundled test-chart.garbus must show up as at least one card.
            AddAssert("has at least one chart", () => songSelect.Groups.SelectMany(g => g.Charts).Any());
        }

        [Test]
        public void TestViewTogglePersists()
        {
            AddStep("set flat", () => songSelect.Grouped = false);
            AddAssert("config updated", () => config.Get<bool>(GarbusSetting.SongSelectGrouped) == false);
            AddStep("set grouped", () => songSelect.Grouped = true);
            AddAssert("config updated", () => config.Get<bool>(GarbusSetting.SongSelectGrouped) == true);
        }

        [Test]
        public void TestSelectThenLaunchPushesPlayScreen()
        {
            AddStep("select first chart", () => songSelect.Select(songSelect.Groups.SelectMany(g => g.Charts).First()));
            AddAssert("selection set", () => songSelect.SelectedChart != null);
            AddStep("launch", () => songSelect.Launch());
            AddUntilStep("play screen pushed", () => stack.CurrentScreen is PlayScreen);
            AddAssert("play screen has the selected chart", () =>
                ((PlayScreen)stack.CurrentScreen).Chart != null);
        }

        [Test]
        public void TestLaunchStopsPreviewTrack()
        {
            ITrack? preview = null;

            AddStep("select first chart", () => songSelect.Select(songSelect.Groups.SelectMany(g => g.Charts).First()));
            AddUntilStep("preview playing", () => songSelect.PreviewTrack?.IsRunning == true);

            // Capture the actual track instance before Launch() clears SongSelectScreen.PreviewTrack —
            // that field is nulled synchronously the moment a stop is requested, regardless of whether
            // the underlying track ever actually stops, so it can't be used to observe the real bug.
            AddStep("capture preview track", () => preview = songSelect.PreviewTrack);

            AddStep("launch", () => songSelect.Launch());
            AddUntilStep("play screen pushed", () => stack.CurrentScreen is PlayScreen);

            // Regression: SongSelectScreen is suspended (and stops updating) synchronously as part of
            // Push(), which happens right after Launch() kicks off the animated fade-then-stop. The
            // fade's OnComplete (which calls Track.Stop()) never gets to run, so the preview keeps
            // playing forever underneath PlayScreen.
            AddUntilStep("captured preview track stopped", () => preview?.IsRunning == false);
        }

        [Test]
        public void TestHighlightPersistsAcrossViewToggle()
        {
            ChartCard? selected = null;

            AddStep("select first chart", () =>
            {
                selected = songSelect.Groups.SelectMany(g => g.Charts).First();
                songSelect.Select(selected);
            });

            AddStep("toggle to flat view", () => songSelect.Grouped = false);

            // ChartRow doesn't expose which ChartCard it renders, so correspondence to the still-selected
            // card is established via the row<->SelectedChart sync invariant (Select()/rebuild() only ever
            // mark the row registered for SelectedChart): assert both that exactly one row is highlighted
            // and that SelectedChart itself didn't change across the toggle.
            AddAssert("selection unchanged", () => songSelect.SelectedChart == selected);
            AddAssert("exactly one row is selected", () =>
                this.ChildrenOfType<ChartRow>().Count(r => r.Selected) == 1);

            AddStep("toggle back to grouped view", () => songSelect.Grouped = true);

            AddAssert("selection still unchanged", () => songSelect.SelectedChart == selected);
            AddAssert("still exactly one row selected", () =>
                this.ChildrenOfType<ChartRow>().Count(r => r.Selected) == 1);
        }

        [Test]
        public void TestArrowKeysMoveSelection()
        {
            IReadOnlyList<ChartCard> ordered = null!;

            AddStep("go flat (deterministic single-list order)", () => songSelect.Grouped = false);
            AddStep("capture flat order + select first", () =>
            {
                ordered = songSelect.Groups.SelectMany(g => g.Charts).OrderBy(c => c.Level).ToList();
                songSelect.Select(ordered[0]);
            });

            AddAssert("at least two charts to navigate between", () => ordered.Count >= 2);

            AddStep("press down", () => input.Key(Key.Down));
            AddAssert("selection advanced to second card", () => songSelect.SelectedChart == ordered[1]);

            AddStep("press up", () => input.Key(Key.Up));
            AddAssert("selection moved back to first card", () => songSelect.SelectedChart == ordered[0]);
        }

        [Test]
        public void TestSelectPopulatesDetailPanel()
        {
            ChartCard? first = null;

            AddStep("select first chart", () =>
            {
                first = songSelect.Groups.SelectMany(g => g.Charts).First();
                songSelect.Select(first);
            });

            AddAssert("panel shows selected card", () =>
                this.ChildrenOfType<ChartDetailPanel>().Single().DisplayedCard == first);

            // The bundled chart has no background file → the placeholder path.
            AddAssert("panel shows placeholder (no background)", () =>
                !this.ChildrenOfType<ChartDetailPanel>().Single().HasBackground);
        }

        [Test]
        public void TestPlayButtonLaunches()
        {
            AddStep("select first chart", () => songSelect.Select(songSelect.Groups.SelectMany(g => g.Charts).First()));

            AddStep("click play button", () =>
            {
                var button = this.ChildrenOfType<BasicButton>().Single(b => b.Text.ToString() == "Press X to play!");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddUntilStep("play screen pushed", () => stack.CurrentScreen is PlayScreen);
        }
    }
}
