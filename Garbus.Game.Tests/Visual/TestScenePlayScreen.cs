// Smoke test for the Phase 2 game loop screen: chart + clock stack + playfield wire up and the
// gameplay clock starts. Also the visual entry point for manually playing the vertical slice in the
// test browser.

using System.Linq;
using Garbus.Game.Configuration;
using Garbus.Game.Screens;
using Garbus.Game.Settings;
using Garbus.Game.Timing;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Utils;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestScenePlayScreen : GarbusTestScene
    {
        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private PlayScreen playScreen = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create play screen", () => Child = new ScreenStack(playScreen = new PlayScreen()) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("screen loaded", () => playScreen.IsLoaded);
        }

        [Test]
        public void TestGameplayStarts()
        {
            AddUntilStep("playfield created", () => this.ChildrenOfType<GarbusPlayfield>().Any());
            AddUntilStep("gameplay time advances", () => this.ChildrenOfType<GarbusPlayfield>().Single().Time.Current > 0);
        }

        [Test]
        public void TestObjectsBecomeVisible()
        {
            AddUntilStep("playfield created", () => this.ChildrenOfType<GarbusPlayfield>().Any());
            AddUntilStep("objects become alive", () =>
                this.ChildrenOfType<GarbusPlayfield>().Single().AllHitObjects.Any(d => d.IsAlive));
        }

        [Test]
        public void TestScrollSpeedSliderBoundToConfig()
        {
            AddUntilStep("scroll speed slider present", () => this.ChildrenOfType<SettingsSlider>().Any());

            AddStep("set speed 7", () => config.SetValue(GarbusSetting.ScrollSpeed, 7.0));
            AddAssert("slider reflects config", () =>
                Precision.AlmostEquals(this.ChildrenOfType<BasicSliderBar<double>>().Single().Current.Value, 7.0, 1e-6));

            AddStep("drag slider to 12", () => this.ChildrenOfType<BasicSliderBar<double>>().Single().Current.Value = 12.0);
            AddAssert("moving slider retunes config", () =>
                Precision.AlmostEquals(config.Get<double>(GarbusSetting.ScrollSpeed), 12.0, 1e-6));
        }

        [Test]
        public void TestLeadInBeginsBeforeGameplayStart()
        {
            AddUntilStep("clock created", () => this.ChildrenOfType<MasterGameplayClockContainer>().Any());

            AddAssert("gameplay start time is zero (normal play)", () =>
                this.ChildrenOfType<MasterGameplayClockContainer>().Single().GameplayStartTime == 0);

            AddAssert("clock starts one lead-in before gameplay", () =>
                this.ChildrenOfType<MasterGameplayClockContainer>().Single().StartTime
                    == -MasterGameplayClockContainer.LEAD_IN_TIME);
        }
    }
}
