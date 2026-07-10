// Smoke test for the Phase 2 game loop screen: chart + clock stack + playfield wire up and the
// gameplay clock starts. Also the visual entry point for manually playing the vertical slice in the
// test browser.

using System.Linq;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestScenePlayScreen : GarbusTestScene
    {
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
    }
}
