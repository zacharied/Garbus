using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Utils;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneEditorClock : GarbusTestScene
    {
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
