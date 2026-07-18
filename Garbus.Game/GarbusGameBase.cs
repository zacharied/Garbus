using Garbus.Game.Charts;
using Garbus.Game.Configuration;
using Garbus.Resources;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            Resources.AddStore(new DllResourceStore(typeof(GarbusResources).Assembly));

            dependencies.Cache(LocalConfig = new GarbusConfigManager(storage));
            dependencies.Cache(new ChartStore(Resources));

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

            // Reduced master volume, pinned on every startup until the Phase 5 settings screen exposes
            // volume control (the framework would otherwise persist whatever value was last set).
            Audio.Volume.Value = 0.01;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            LocalConfig?.Dispose(); // performs a final save of any changed settings.
        }
    }
}
