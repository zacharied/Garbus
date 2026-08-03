using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Scoring;
using NUnit.Framework;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneComboBreakSound : GarbusTestScene
    {
        private ComboBreakSound comboBreak = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create combo break sound", () => Child = comboBreak = new ComboBreakSound());
        }

        [Test]
        public void TestSamplePreloadedOnLoad()
        {
            AddAssert("sample resolved from the store", () => comboBreak.Preloaded);
            AddAssert("nothing played yet", () => comboBreak.PlayCount, () => Is.Zero);
        }

        [Test]
        public void TestMissAboveThresholdPlays()
        {
            AddStep("miss at combo 51", () => comboBreak.PlayIfComboBroken(HitResult.Miss, ComboBreakSound.MINIMUM_BROKEN_COMBO + 1));
            AddAssert("played once", () => comboBreak.PlayCount, () => Is.EqualTo(1));
        }

        [Test]
        public void TestMissAtOrBelowThresholdIsSilent()
        {
            AddStep("miss at combo 50", () => comboBreak.PlayIfComboBroken(HitResult.Miss, ComboBreakSound.MINIMUM_BROKEN_COMBO));
            AddStep("miss at combo 0", () => comboBreak.PlayIfComboBroken(HitResult.Miss, 0));
            AddAssert("stayed silent", () => comboBreak.PlayCount, () => Is.Zero);
        }

        [Test]
        public void TestNonMissJudgementIsSilent()
        {
            AddStep("perfect at combo 200", () => comboBreak.PlayIfComboBroken(HitResult.Perfect, 200));
            AddStep("bad at combo 200", () => comboBreak.PlayIfComboBroken(HitResult.Bad, 200));
            AddStep("ignored miss at combo 200", () => comboBreak.PlayIfComboBroken(HitResult.IgnoreMiss, 200));
            AddAssert("stayed silent", () => comboBreak.PlayCount, () => Is.Zero);
        }
    }
}
