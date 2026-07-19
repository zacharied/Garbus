using System.Collections.Generic;
using Garbus.Game.Charts;
using Garbus.Game.Configuration;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Settings;
using Garbus.Resources;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
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
            };

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            Resources.AddStore(new DllResourceStore(typeof(GarbusResources).Assembly));

            dependencies.Cache(LocalConfig = new GarbusConfigManager(storage));
            dependencies.Cache(new ChartStore(Resources));

            // Scroll speed -> gameplay TimeRange. Cached here so the gameplay scrolling container
            // resolves a config-driven GarbusScrollingInfo. Speed 10 reproduces the historical 700 ms.
            var scrollingInfo = new GarbusScrollingInfo();
            scrollSpeed = LocalConfig.GetBindable<double>(GarbusSetting.ScrollSpeed);
            scrollSpeed.BindValueChanged(v => scrollingInfo.TimeRange.Value = ScrollSpeedMapping.ToTimeRange(v.NewValue), true);
            dependencies.Cache(scrollingInfo);

            // Low-latency audio: drive the framework's experimental WASAPI output from a config toggle.
            // Roughly halves output latency on Windows (needed for editor hitsound feedback to sit on
            // the beat); the chart clock's platform offset auto-recalibrates when this changes. The
            // framework falls back to the normal output path when the device can't do WASAPI.
            //
            // Skip this under headless NUnit runs. The framework forces the "No sound" BASS device in
            // tests, but that guard does NOT cover the WASAPI path — enabling WASAPI opens the real
            // default output device (device -1) and makes the whole test suite audible. Leaving the
            // toggle unbound keeps Audio.UseExperimentalWasapi at its silent framework default.
            if (!DebugUtils.IsNUnitRunning)
                LocalConfig.BindWith(GarbusSetting.UseExperimentalWasapi, Audio.UseExperimentalWasapi);

            // Custom font setup.
            initialiseFonts();
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
