using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Configuration;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Input;
using Garbus.Game.Settings;
using Garbus.Resources;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input;
using osu.Framework.Input.Handlers.Joystick;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osuTK;

namespace Garbus.Game
{
    public partial class GarbusGameBase : osu.Framework.Game
    {
        // Anything in this class is shared between the test browser and the game implementation.
        // It allows for caching global dependencies that should be accessible to tests, or changing
        // the screen scaling for all components including the test browser and framework overlays.

        protected override Container<Drawable> Content { get; }

        protected GarbusConfigManager LocalConfig { get; private set; } = null!;

        private DependencyContainer dependencies = null!;

        private Bindable<double> scrollSpeed = null!;

        protected GarbusGameBase()
        {
            // Ensure game and tests scale with window size and screen DPI.
            base.Content.Add(Content = new DrawSizePreservingFillContainer
            {
                // You may want to change TargetDrawSize to your "default" resolution, which will decide how things scale and position when using absolute coordinates.
                TargetDrawSize = new Vector2(1366, 768)
            });
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent) =>
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        // Fresh-install volume default: 25% master. This is applied as a config *default*, so a value
        // the user has since saved to framework.ini still wins — it only affects first run.
        protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults() =>
            new Dictionary<FrameworkSetting, object>
            {
                { FrameworkSetting.VolumeUniversal, 0.25 },
                { FrameworkSetting.ConfineMouseMode, ConfineMouseMode.Never },
            };

        [BackgroundDependencyLoader]
        private void load(Storage storage, FrameworkConfigManager frameworkConfig)
        {
            // Garbus never requires relative mouse input, so the cursor should remain free to leave
            // the window even in fullscreen mode. Override persisted framework configuration too.
            frameworkConfig.SetValue(FrameworkSetting.ConfineMouseMode, ConfineMouseMode.Never);

            // Replace the framework's per-axis joystick deadzone with a radial (stick-vector) one.
            // The per-axis clamp snaps shallow diagonals onto the cardinals; setting it to 0 instead
            // makes a drifting stick emit a permanently-held phantom axis-button that pollutes every
            // global key-combination and silently disables the framework debug key-bindings (Ctrl+F11,
            // etc.). See RadialJoystickHandler. The platform GameHosts have internal constructors so we
            // can't inject via a custom host; UserInputManager reads Host.AvailableInputHandlers live,
            // so we swap the handler in place instead.
            swapInRadialJoystickHandler();

            Resources.AddStore(new DllResourceStore(typeof(GarbusResources).Assembly));

            dependencies.Cache(LocalConfig = new GarbusConfigManager(storage));
            dependencies.Cache(new SongStore(Resources));
            dependencies.Cache(new KeyBindingStore(storage));

            // Scroll speed -> gameplay TimeRange. Cached here so the gameplay scrolling container
            // resolves a config-driven GarbusScrollingInfo. Speed 10 reproduces the historical 700 ms.
            var scrollingInfo = new GarbusScrollingInfo();
            scrollSpeed = LocalConfig.GetBindable<double>(GarbusSetting.ScrollSpeed);
            scrollSpeed.BindValueChanged(v => scrollingInfo.TimeRange.Value = ScrollSpeedMapping.ToTimeRange(v.NewValue), true);
            dependencies.Cache(scrollingInfo);

            // Prevent audio output in unit tests.
            if (!DebugUtils.IsNUnitRunning)
                LocalConfig.BindWith(GarbusSetting.UseExperimentalWasapi, Audio.UseExperimentalWasapi);

            // Custom font setup.
            initialiseFonts();
        }

        // Swaps the stock JoystickHandler out of the live handler list for a RadialJoystickHandler.
        // No-op when there's no joystick handler (headless) or no window to bind to.
        private void swapInRadialJoystickHandler()
        {
            var stock = Host.AvailableInputHandlers.OfType<JoystickHandler>().SingleOrDefault();
            if (stock == null)
                return;

            var radial = new RadialJoystickHandler();
            if (!radial.Initialize(Host))
            {
                radial.Dispose();
                return;
            }

            ImmutableArray<osu.Framework.Input.Handlers.InputHandler> swapped =
                Host.AvailableInputHandlers.Replace(stock, radial);

            // AvailableInputHandlers has a private setter; UserInputManager reads it live, so replacing
            // the array is enough for the swap to take effect on the next input collection.
            var setter = typeof(GameHost).GetProperty(nameof(GameHost.AvailableInputHandlers))?.GetSetMethod(nonPublic: true);
            if (setter == null)
                throw new InvalidOperationException("Could not access GameHost.AvailableInputHandlers setter; the osu-framework API may have changed.");

            setter.Invoke(Host, new object[] { swapped });

            // Unsubscribe the stock handler from the window (otherwise it keeps enqueuing drift events
            // into a queue that is no longer collected) and drop it.
            stock.Enabled.Value = false;
            stock.Dispose();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            LocalConfig?.Dispose(); // performs a final save of any changed settings.
        }

        private void initialiseFonts()
        {
            AddFont(Resources, @"Fonts/Torus/Torus-Regular");
            AddFont(Resources, @"Fonts/Torus/Torus-Light");
            AddFont(Resources, @"Fonts/Torus/Torus-SemiBold");
            AddFont(Resources, @"Fonts/Torus/Torus-Bold");

            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-Regular");
            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-Light");
            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-SemiBold");
            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-Bold");

            AddFont(Resources, @"Fonts/Inter/Inter-Regular");
            AddFont(Resources, @"Fonts/Inter/Inter-RegularItalic");
            AddFont(Resources, @"Fonts/Inter/Inter-Light");
            AddFont(Resources, @"Fonts/Inter/Inter-LightItalic");
            AddFont(Resources, @"Fonts/Inter/Inter-SemiBold");
            AddFont(Resources, @"Fonts/Inter/Inter-SemiBoldItalic");
            AddFont(Resources, @"Fonts/Inter/Inter-Bold");
            AddFont(Resources, @"Fonts/Inter/Inter-BoldItalic");

            AddFont(Resources, @"Fonts/Noto/Noto-Basic");
            AddFont(Resources, @"Fonts/Noto/Noto-Bopomofo");
            AddFont(Resources, @"Fonts/Noto/Noto-CJK-Basic");
            AddFont(Resources, @"Fonts/Noto/Noto-CJK-Compatibility");
            AddFont(Resources, @"Fonts/Noto/Noto-Hangul");
            AddFont(Resources, @"Fonts/Noto/Noto-Thai");

            AddFont(Resources, @"Fonts/Venera/Venera-Light");
            AddFont(Resources, @"Fonts/Venera/Venera-Bold");
            AddFont(Resources, @"Fonts/Venera/Venera-Black");

            // TODO
//            Fonts.AddStore(new OsuIcon.OsuIconStore(Textures));
        }
    }
}
