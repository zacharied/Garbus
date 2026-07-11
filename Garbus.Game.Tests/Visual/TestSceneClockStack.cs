using Garbus.Game.Configuration;
using Garbus.Game.Timing;
using NUnit.Framework;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneClockStack : GarbusTestScene
    {
        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private MasterGameplayClockContainer gameplayClock = null!;

        private static double expectedPlatformOffset =>
            RuntimeInfo.OS == RuntimeInfo.Platform.Windows ? FramedChartClock.WINDOWS_BASE_AUDIO_OFFSET : 0;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset user offset", () => config.GetBindable<double>(GarbusSetting.AudioOffset).Value = 0);
            AddStep("create gameplay clock", () => Child = gameplayClock = new MasterGameplayClockContainer(new TrackVirtual(60000), 0)
            {
                RelativeSizeAxes = Axes.Both,
            });
            AddUntilStep("clock loaded", () => gameplayClock.IsLoaded);
        }

        [Test]
        public void TestPlatformOffsetApplied()
        {
            AddAssert("platform offset applied", () => gameplayClock.TotalAppliedOffset, () => Is.EqualTo(expectedPlatformOffset));
        }

        [Test]
        public void TestUserOffsetApplied()
        {
            AddStep("set user offset to 30", () => config.GetBindable<double>(GarbusSetting.AudioOffset).Value = 30);
            AddAssert("total offset includes user offset", () => gameplayClock.TotalAppliedOffset, () => Is.EqualTo(expectedPlatformOffset + 30));
        }

        [Test]
        public void TestClockStartsAndAdvances()
        {
            AddStep("start clock", () => gameplayClock.Start());
            AddUntilStep("gameplay time advances", () => gameplayClock.CurrentTime, () => Is.GreaterThan(100));
        }

        [Test]
        public void TestSeekTargetsChartTime()
        {
            AddStep("seek to 5000", () => gameplayClock.Seek(5000));
            AddAssert("gameplay time at seek target", () => gameplayClock.CurrentTime, () => Is.EqualTo(5000).Within(1));
        }

        [Test]
        public void TestResetWhileRunningKeepsClockRunning()
        {
            // Reproduces GAR-8: the restart button (PlayScreen.restart) calls Reset(startClock: true)
            // while the clock is running. Reset must leave the clock actually running afterwards — not
            // stopped-but-reporting-unpaused, which forced the user to press Space twice to resume.
            AddStep("start clock", () => gameplayClock.Start());
            AddUntilStep("clock advancing", () => gameplayClock.CurrentTime, () => Is.GreaterThan(100));

            AddStep("reset while running", () => gameplayClock.Reset(startClock: true));

            AddAssert("not paused after reset", () => gameplayClock.IsPaused.Value, () => Is.False);
            AddUntilStep("clock advances again after reset", () => gameplayClock.CurrentTime, () => Is.GreaterThan(100));
        }
    }
}
