// Smoke test for the Phase 2 game loop screen: chart + clock stack + playfield wire up and the
// gameplay clock starts. Also the visual entry point for manually playing the vertical slice in the
// test browser.

using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Configuration;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.Screens;
using Garbus.Game.Settings;
using Garbus.Game.Timing;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
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

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

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

        [Test]
        public void TestUncaughtSliderHeadDoesNotBuildComboOrPlayAudio()
        {
            DrawableSliderBody sliderBody = null!;

            AddStep("replace with isolated slider", () =>
            {
                var chart = new GarbusChart
                {
                    HitObjects =
                    {
                        new SliderBody
                        {
                            StartTime = 0,
                            AngleDeg = 0,
                            Side = HorizontalDirection.Left,
                            Path = new GarbusPath { ControlPoints = new BindableList<GarbusPathControlPoint>() },
                        },
                    },
                };
                chart.ApplyDefaults();

                Child = new ScreenStack(playScreen = new PlayScreen(chart, new TrackVirtual(10_000)))
                {
                    RelativeSizeAxes = Axes.Both,
                };
            });

            AddUntilStep("isolated screen loaded", () => playScreen.IsLoaded);
            AddUntilStep("slider drawable loaded", () =>
            {
                sliderBody = this.ChildrenOfType<GarbusPlayfield>().Single().AllHitObjects
                                 .OfType<DrawableSliderBody>().SingleOrDefault()!;
                return sliderBody != null;
            });

            AddStep("seek beyond catch window", () => this.ChildrenOfType<MasterGameplayClockContainer>().Single().Seek(500));
            AddUntilStep("head missed", () => sliderBody.NestedHitObjects.OfType<DrawableSliderHead>().Single().Judged);

            AddAssert("head result is Miss", () => sliderBody.NestedHitObjects.OfType<DrawableSliderHead>().Single().Result.Type,
                () => Is.EqualTo(Gameplay.Scoring.HitResult.Miss));
            AddAssert("head did not increase combo", () => sliderBody.NestedHitObjects.OfType<DrawableSliderHead>().Single().Result.ComboAfterJudgement,
                () => Is.Zero);
            AddAssert("playfield combo remains zero", () => this.ChildrenOfType<GarbusPlayfield>().Single().Combo.Value, () => Is.Zero);
            AddAssert("missed head is silent", () => sliderBody.NestedHitObjects.OfType<DrawableSliderHead>().Single().SamplesPlayCount,
                () => Is.Zero);
            AddUntilStep("body sentinel judged", () => sliderBody.Judged);
            AddAssert("body sentinel is silent", () => sliderBody.SamplesPlayCount, () => Is.Zero);
        }

        [Test]
        public void TestJacketLayersPresentWhenJacketProvided()
        {
            AddStep("recreate with jacket", () =>
            {
                var chart = new GarbusChart();
                Child = new ScreenStack(playScreen = new PlayScreen(chart, new TrackVirtual(60000), jacket: renderer.WhitePixel))
                {
                    RelativeSizeAxes = Axes.Both,
                };
            });
            AddUntilStep("screen loaded", () => playScreen.IsLoaded);
            AddAssert("jacket background has layers", () =>
                playScreen.ChildrenOfType<JacketBackground>().Single().ChildrenOfType<Sprite>().Any());
        }

        [Test]
        public void TestNoJacketLayersOnBundledPath()
        {
            // The SetUpSteps screen is the bundled test chart, which has no jacket: the background
            // component is present but empty, leaving the flat base box visible.
            AddAssert("no jacket layers", () =>
                !playScreen.ChildrenOfType<JacketBackground>().Single().ChildrenOfType<Sprite>().Any());
        }
    }
}
