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

        private DependencyContainer dependencies = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent) =>
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        [BackgroundDependencyLoader]
        private void load()
        {
            GlobalSettingsContainer settings;

            Children = new Drawable[]
            {
                screenStack = new ScreenStack { RelativeSizeAxes = Axes.Both },
                settings = new GlobalSettingsContainer(screenStack) { RelativeSizeAxes = Axes.Both },
                // Persistent build code, drawn on top of every screen (bottom-right corner).
                new BuildInfoOverlay(),
            };

            // Let screens (e.g. the editor's File → Game settings) open the overlay without holding a
            // direct reference to the container. Cached before any screen is pushed in LoadComplete.
            dependencies.CacheAs<ISettingsOverlayControl>(settings);
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
