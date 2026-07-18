// Visual + integration test for song select: the library scans bundled charts, the view toggle
// flips grouping (and persists), selecting a chart drives the audio preview, and launching pushes a
// PlayScreen for the chosen chart.

using System.Linq;
using Garbus.Game.Configuration;
using Garbus.Game.Screens;
using Garbus.Game.Screens.SongSelect;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSongSelect : GarbusTestScene
    {
        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private ScreenStack stack = null!;
        private SongSelectScreen songSelect = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("push song select", () =>
                Child = stack = new ScreenStack(songSelect = new SongSelectScreen()) { RelativeSizeAxes = Axes.Both });
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
    }
}
