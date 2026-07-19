using Garbus.Game.Configuration;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Utils;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneScrollSpeed : GarbusTestScene
    {
        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private GarbusScrollingHitObjectContainer container = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset speed", () => config.SetValue(GarbusSetting.ScrollSpeed, 10.0));
            AddStep("create container", () => Child = container = new GarbusScrollingHitObjectContainer { RelativeSizeAxes = Axes.Both });
        }

        [Test]
        public void TestConfigDrivesTimeRange()
        {
            AddAssert("default 700ms", () => Precision.AlmostEquals(container.CurrentTimeRange, 700, 0.001));
            AddStep("speed 20", () => config.SetValue(GarbusSetting.ScrollSpeed, 20.0));
            AddAssert("timerange 350ms", () => Precision.AlmostEquals(container.CurrentTimeRange, 350, 0.001));
        }

        [Test]
        public void TestConfigSnapsToTenths()
        {
            AddStep("set 4.04", () => config.SetValue(GarbusSetting.ScrollSpeed, 4.04));
            AddAssert("snaps down to 4.0", () => Precision.AlmostEquals(config.Get<double>(GarbusSetting.ScrollSpeed), 4.0, 1e-6));
            AddStep("set 4.06", () => config.SetValue(GarbusSetting.ScrollSpeed, 4.06));
            AddAssert("snaps up to 4.1", () => Precision.AlmostEquals(config.Get<double>(GarbusSetting.ScrollSpeed), 4.1, 1e-6));
        }
    }
}
