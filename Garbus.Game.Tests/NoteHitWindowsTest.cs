using Garbus.Game.Core;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Judgement;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class NoteHitWindowsTest
    {
        [Test]
        public void CardinalTableMatchesSpec()
        {
            var windows = new CardinalNoteHitWindows();

            Assert.That(windows.ResultFor(0), Is.EqualTo(HitResult.CriticalPerfect));
            Assert.That(windows.ResultFor(-32), Is.EqualTo(HitResult.CriticalPerfect));
            Assert.That(windows.ResultFor(33), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(-64), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(65), Is.EqualTo(HitResult.Near));
            Assert.That(windows.ResultFor(110), Is.EqualTo(HitResult.Near));
            Assert.That(windows.ResultFor(111), Is.EqualTo(HitResult.None)); // no late Miss window
            Assert.That(windows.ResultFor(-111), Is.EqualTo(HitResult.Miss)); // early-miss
            Assert.That(windows.ResultFor(-200), Is.EqualTo(HitResult.Miss));
            Assert.That(windows.ResultFor(-201), Is.EqualTo(HitResult.None));
            Assert.That(windows.LateEligibilityEdge, Is.EqualTo(110));
            Assert.That(windows.EarliestInteractionEdge, Is.EqualTo(200));
            Assert.That(windows.IsHitResultAllowed(HitResult.Bad), Is.False);
        }

        [Test]
        public void ShoulderTableMatchesSpec()
        {
            var windows = new ShoulderNoteHitWindows();

            Assert.That(windows.ResultFor(-40), Is.EqualTo(HitResult.CriticalPerfect));
            Assert.That(windows.ResultFor(41), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(80), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(-81), Is.EqualTo(HitResult.Near));
            Assert.That(windows.ResultFor(150), Is.EqualTo(HitResult.Near));
            Assert.That(windows.ResultFor(151), Is.EqualTo(HitResult.None));
            Assert.That(windows.ResultFor(-151), Is.EqualTo(HitResult.Miss));
            Assert.That(windows.ResultFor(-200), Is.EqualTo(HitResult.Miss));
            Assert.That(windows.LateEligibilityEdge, Is.EqualTo(150));
            Assert.That(windows.EarliestInteractionEdge, Is.EqualTo(200));
            Assert.That(windows.IsHitResultAllowed(HitResult.Bad), Is.False);
        }

        [Test]
        public void NoteTypesWireTheirWindows()
        {
            var cardinal = new CardinalNote { AngleDeg = 90 };
            cardinal.ApplyDefaults();
            Assert.That(cardinal.HitWindows, Is.InstanceOf<CardinalNoteHitWindows>());

            var shoulder = new ShoulderNote { Side = HorizontalDirection.Left };
            shoulder.ApplyDefaults();
            Assert.That(shoulder.HitWindows, Is.InstanceOf<ShoulderNoteHitWindows>());

            var cardinalHold = new CardinalHoldNote { AngleDeg = 90, Duration = 500 };
            cardinalHold.ApplyDefaults();
            Assert.That(cardinalHold.HitWindows, Is.InstanceOf<CardinalNoteHitWindows>());
        }

        [Test]
        public void HoldHeadInheritsParentWindows()
        {
            var hold = new ShoulderHoldNote { Side = HorizontalDirection.Right, Duration = 500 };
            hold.ApplyDefaults();

            Assert.That(hold.HitWindows, Is.InstanceOf<ShoulderNoteHitWindows>());
            Assert.That(hold.Head.HitWindows, Is.SameAs(hold.HitWindows));
        }

        [Test]
        public void SlamsWireEarlyPermissiveWindows()
        {
            var slam = new GarbusSlamCentered { AngleDeg = 0 };
            slam.ApplyDefaults();

            Assert.That(slam.HitWindows, Is.InstanceOf<SlamHitWindows>());
            Assert.That(slam.MaximumJudgementOffset, Is.EqualTo(300));
            Assert.That(slam.Judgement.MaxResult, Is.EqualTo(HitResult.Perfect));
        }

        [Test]
        public void EmptyWindowsHaveNoEarliestInteractionEdge()
        {
            Assert.That(HitWindows.Empty.EarliestInteractionEdge, Is.EqualTo(0));
        }
    }
}
