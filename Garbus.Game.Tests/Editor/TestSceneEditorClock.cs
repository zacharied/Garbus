using Garbus.Game.Charts.Timing;
using Garbus.Game.Configuration;
using Garbus.Game.Edit;
using Garbus.Game.Tests.Visual;
using Garbus.Game.Timing;
using NUnit.Framework;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Utils;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneEditorClock : GarbusTestScene
    {
        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        // Mirrors FramedChartClock.updatePlatformOffset: the experimental WASAPI path shifts the
        // platform offset, so read the live state rather than assuming the non-WASAPI value.
        private double expectedPlatformOffset
        {
            get
            {
                if (RuntimeInfo.OS != RuntimeInfo.Platform.Windows)
                    return 0;

                double offset = FramedChartClock.WINDOWS_BASE_AUDIO_OFFSET;
                if (audio.UseExperimentalWasapi.Value)
                    offset += FramedChartClock.WINDOWS_EXPERIMENTAL_AUDIO_OFFSET;
                return offset;
            }
        }

        [Test]
        public void TestPlatformOffsetApplied()
        {
            var cpi = new ControlPointInfo();
            EditorClock clock = null!;

            AddStep("reset user offset", () => config.GetBindable<double>(GarbusSetting.AudioOffset).Value = 0);
            AddStep("create clock", () => Child = clock = new EditorClock(cpi, 60000));
            AddStep("change source", () => clock.ChangeSource(new TrackVirtual(60000)));
            AddUntilStep("clock loaded", () => clock.IsLoaded);
            AddAssert("platform offset applied to editor clock", () => clock.TotalAppliedOffset, () => Is.EqualTo(expectedPlatformOffset));
        }

        [Test]
        public void TestUserOffsetApplied()
        {
            var cpi = new ControlPointInfo();
            EditorClock clock = null!;

            AddStep("reset user offset", () => config.GetBindable<double>(GarbusSetting.AudioOffset).Value = 0);
            AddStep("create clock", () => Child = clock = new EditorClock(cpi, 60000));
            AddStep("change source", () => clock.ChangeSource(new TrackVirtual(60000)));
            AddUntilStep("clock loaded", () => clock.IsLoaded);
            AddStep("set user offset to 30", () => config.GetBindable<double>(GarbusSetting.AudioOffset).Value = 30);
            AddAssert("editor clock includes user offset", () => clock.TotalAppliedOffset, () => Is.EqualTo(expectedPlatformOffset + 30));
        }

        [Test]
        public void TestSeekSnappedToDivisor()
        {
            var cpi = new ControlPointInfo();
            cpi.Add(0, new TimingControlPoint { BeatLength = 500 }); // 120 BPM
            var divisor = new BindableBeatDivisor(4);
            EditorClock clock = null!;

            AddStep("create clock", () => Child = clock = new EditorClock(cpi, 60000, divisor));
            AddStep("change source", () => clock.ChangeSource(new TrackVirtual(60000)));
            AddStep("seek snapped to 130", () => clock.SeekSnapped(130));
            AddAssert("snapped to 125 (1/4 of 500ms beat)", () => Precision.AlmostEquals(clock.CurrentTime, 125, 1));
            AddStep("seek forward snapped", () => clock.SeekForward(true, 1));
            AddAssert("advanced by one divisor step", () => clock.CurrentTime > 125);
        }
    }
}
