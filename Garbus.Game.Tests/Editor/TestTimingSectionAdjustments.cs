// TDD tests for TimingSectionAdjustments + EditorChart.PerformOnRange.
//
// Contract under test (osu's timing-section semantics):
//   - A timing point's section spans [its time, next timing point's time). The FIRST timing point's
//     section extends back to the start of time (objects before it belong to it).
//   - AdjustHitObjectOffset shifts StartTime of in-section objects by the adjustment; others untouched.
//   - SetHitObjectBPM keeps objects on the same beat: StartTime rescales around the point's time by
//     newBeatLength/oldBeatLength; CardinalHoldNote.Duration scales; SliderBody scales its path TimeOffsets
//     (its Duration is derived from the path — the setter is a no-op).

using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens.Timing;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Bindables;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestTimingSectionAdjustments
    {
        private EditorChart editorChart = null!;
        private TimingControlPoint firstPoint = null!;
        private TimingControlPoint secondPoint = null!;

        [SetUp]
        public void SetUp()
        {
            var chart = new GarbusChart();
            firstPoint = new TimingControlPoint { BeatLength = 500 };  // 120 BPM
            secondPoint = new TimingControlPoint { BeatLength = 400 }; // 150 BPM
            chart.ControlPointInfo.Add(0, firstPoint);
            chart.ControlPointInfo.Add(4000, secondPoint);
            editorChart = new EditorChart(chart);
        }

        [Test]
        public void TestAdjustOffsetMovesOnlyObjectsInSection()
        {
            var early = addNote(1000);
            var late = addNote(5000);

            TimingSectionAdjustments.AdjustHitObjectOffset(editorChart, secondPoint, 100);

            Assert.That(early.StartTime, Is.EqualTo(1000).Within(0.001));
            Assert.That(late.StartTime, Is.EqualTo(5100).Within(0.001));
        }

        [Test]
        public void TestFirstSectionExtendsToStartOfTime()
        {
            var beforePoint = addNote(-500);
            var inSection = addNote(1000);
            var nextSection = addNote(5000);

            TimingSectionAdjustments.AdjustHitObjectOffset(editorChart, firstPoint, 50);

            Assert.That(beforePoint.StartTime, Is.EqualTo(-450).Within(0.001));
            Assert.That(inSection.StartTime, Is.EqualTo(1050).Within(0.001));
            Assert.That(nextSection.StartTime, Is.EqualTo(5000).Within(0.001));
        }

        [Test]
        public void TestSetHitObjectBPMRescalesPositions()
        {
            var note = addNote(5000); // 2.5 beats after the 4000ms point at BeatLength 400
            var earlier = addNote(1000);

            double oldBeatLength = secondPoint.BeatLength;
            secondPoint.BeatLength = 200; // 300 BPM
            TimingSectionAdjustments.SetHitObjectBPM(editorChart, secondPoint, oldBeatLength);

            Assert.That(note.StartTime, Is.EqualTo(4500).Within(0.001)); // 4000 + 2.5 * 200
            Assert.That(earlier.StartTime, Is.EqualTo(1000).Within(0.001));
        }

        [Test]
        public void TestSetHitObjectBPMScalesHoldDuration()
        {
            var hold = new CardinalHoldNote { StartTime = 4400, Duration = 800, AngleDeg = 0 };
            editorChart.Add(hold);

            double oldBeatLength = secondPoint.BeatLength;
            secondPoint.BeatLength = 200;
            TimingSectionAdjustments.SetHitObjectBPM(editorChart, secondPoint, oldBeatLength);

            Assert.That(hold.StartTime, Is.EqualTo(4200).Within(0.001));
            Assert.That(hold.Duration, Is.EqualTo(400).Within(0.001));
        }

        [Test]
        public void TestSetHitObjectBPMScalesSliderPath()
        {
            var slider = new SliderBody
            {
                StartTime = 4000,
                AngleDeg = 0,
                Side = HorizontalDirection.Right,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = 400, RotationOffset = 90 },
                        new GarbusPathControlPoint { TimeOffset = 800, RotationOffset = 180 },
                    },
                },
            };
            editorChart.Add(slider);

            double oldBeatLength = secondPoint.BeatLength;
            secondPoint.BeatLength = 200;
            TimingSectionAdjustments.SetHitObjectBPM(editorChart, secondPoint, oldBeatLength);

            Assert.That(slider.Path.ControlPoints[0].TimeOffset, Is.EqualTo(200).Within(0.001));
            Assert.That(slider.Path.ControlPoints[1].TimeOffset, Is.EqualTo(400).Within(0.001));
            Assert.That(slider.Duration, Is.EqualTo(400).Within(0.001)); // derived from the path
        }

        private CardinalNote addNote(double time)
        {
            var note = new CardinalNote { StartTime = time, AngleDeg = 0 };
            editorChart.Add(note);
            return note;
        }
    }
}
