using Garbus.Game.Gameplay.Audio;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneHitSoundContainer : GarbusTestScene
    {
        private HitSoundContainer container = null!;

        private static readonly HitSampleInfo real = new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT);
        private static readonly HitSampleInfo unloaded = new HitSampleInfo("does-not-exist");

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create container with the soft-hitnormal sample", () =>
            {
                Child = container = new HitSoundContainer { RelativeSizeAxes = Axes.Both };
                container.Samples = new[] { real };
            });
        }

        [Test]
        public void PlayingAMatchingInfoPlaysExactlyThatMember()
        {
            AddStep("play the loaded info", () => container.Play(real));
            AddAssert("play count is 1", () => container.PlayCount, () => Is.EqualTo(1));
            AddAssert("last played is the loaded info", () => container.LastPlayed, () => Is.EqualTo(real));
        }

        [Test]
        public void PlayingAnUnloadedInfoIsSilent()
        {
            AddStep("play an info that was never loaded", () => container.Play(unloaded));
            AddAssert("play count stays 0", () => container.PlayCount, () => Is.EqualTo(0));
            AddAssert("last played stays null", () => container.LastPlayed, () => Is.Null);
        }

        [Test]
        public void PlayingNullIsSilent()
        {
            AddStep("play null", () => container.Play((HitSampleInfo?)null));
            AddAssert("play count stays 0", () => container.PlayCount, () => Is.EqualTo(0));
        }
    }
}
