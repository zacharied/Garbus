using Garbus.Game.Screens;
using Garbus.Game.Settings;
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
            Children = new Drawable[]
            {
                screenStack = new ScreenStack { RelativeSizeAxes = Axes.Both },
                new GlobalSettingsContainer(screenStack) { RelativeSizeAxes = Axes.Both },
                // Persistent build code, drawn on top of every screen (bottom-right corner).
                new BuildInfoOverlay(),
            };
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
