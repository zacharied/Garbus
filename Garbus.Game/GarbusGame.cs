using Garbus.Game.Screens;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;

namespace Garbus.Game
{
    public partial class GarbusGame : GarbusGameBase
    {
        private ScreenStack screenStack = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            // Add your top-level game components here.
            // A screen stack and sample screen has been provided for convenience, but you can replace it if you don't want to use screens.
            Child = screenStack = new ScreenStack { RelativeSizeAxes = Axes.Both };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Phase 4: boot into the main menu. PlayScreen and MainScreen remain reachable through
            // the test browser.
            screenStack.Push(new MainMenuScreen());
        }
    }
}
