// DesignOverlay drives the tutorial-message effect off a gameplay clock. Driven by a ManualClock so
// headless runs can seek deterministically (mirrors TestSceneGameplay's manual-clock harness).

using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Screens;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneDesignOverlay : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private DesignOverlay overlay = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create overlay with one tutorial message", () =>
            {
                manualClock = new ManualClock { Rate = 1 };

                var chart = new GarbusChart();
                chart.DesignPointInfo.Add(new TutorialMessage { StartTime = 2000, EndTime = 4000, Text = "Tutorial!" });

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = overlay = new DesignOverlay(chart) { RelativeSizeAxes = Axes.Both },
                };
            });

            AddUntilStep("overlay loaded", () => overlay.IsLoaded);
        }

        [Test]
        public void TestOverlayVisibleOnlyDuringWindow()
        {
            AddStep("seek before window (1000)", () => manualClock.CurrentTime = 1000);
            AddUntilStep("hidden before", () => !overlay.MessageVisibleForTests && overlay.DimAlphaForTests == 0);

            AddStep("seek into window (3000)", () => manualClock.CurrentTime = 3000);
            AddUntilStep("visible during", () =>
                overlay.MessageVisibleForTests
                && overlay.DimAlphaForTests == TutorialMessage.OVERLAY_OPACITY
                && overlay.MessageTextForTests == "Tutorial!");

            AddStep("seek after window (5000)", () => manualClock.CurrentTime = 5000);
            AddUntilStep("hidden after", () => !overlay.MessageVisibleForTests && overlay.DimAlphaForTests == 0);
        }
    }
}
