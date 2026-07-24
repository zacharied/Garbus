using Garbus.Game;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class BuildInfoTest
    {
        [Test]
        public void CommitIsStampedFromGit()
        {
            // The EmbedGitCommit target in Directory.Build.props stamps the building commit's short hash
            // into the Garbus.Game assembly. A local or CI build always has git available, so a value of
            // "unknown" means the stamping pipeline broke.
            Assert.That(BuildInfo.Commit, Is.Not.Empty);
            Assert.That(BuildInfo.Commit, Is.Not.EqualTo("unknown"),
                "git commit was not embedded — check the EmbedGitCommit MSBuild target");
        }

        [Test]
        public void PlatformIsPopulated()
        {
            Assert.That(BuildInfo.Platform, Is.Not.Empty);
        }

        [Test]
        public void DisplayTextCombinesPlatformAndCommit()
        {
            Assert.That(BuildInfo.DisplayText, Is.EqualTo($"{BuildInfo.Platform} @ {BuildInfo.Commit}"));
        }
    }
}
