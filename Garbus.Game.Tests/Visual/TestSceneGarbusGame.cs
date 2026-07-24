using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Testing;
using NUnit.Framework;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneGarbusGame : GarbusTestScene
    {
        // Add visual tests to ensure correct behaviour of your game: https://github.com/ppy/osu-framework/wiki/Development-and-Testing
        // You can make changes to classes associated with the tests and they will recompile and update immediately.

        private TestGarbusGame game = null!;
        private readonly TemporaryNativeStorage frameworkStorage = new($"garbus-main-framework-{Guid.NewGuid():N}");
        private readonly FrameworkConfigManager frameworkConfig;
        private ConfineMouseMode persistedConfineMouseMode;

        public TestSceneGarbusGame()
        {
            frameworkConfig = new FrameworkConfigManager(frameworkStorage, new Dictionary<FrameworkSetting, object>
            {
                { FrameworkSetting.VolumeUniversal, 0.25 },
                { FrameworkSetting.ConfineMouseMode, ConfineMouseMode.Never },
            });
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("persist fullscreen confinement", () => seedFrameworkConfig(ConfineMouseMode.Fullscreen));
            AddStep("load main game", () => AddGame(game = new TestGarbusGame()));
            AddUntilStep("main game loaded", () => game.IsLoaded);
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("dispose main game", () =>
            {
                if (game.Parent is Container<Drawable> parent)
                    parent.Remove(game, true);
            });
        }

        [OneTimeTearDown]
        public void DisposeFrameworkConfig()
        {
            frameworkConfig.Dispose();
            frameworkStorage.Dispose();
        }

        [Test]
        public void TestMainFrameworkDefaultKeepsCursorFree()
        {
            AddAssert("cursor confinement defaults to never",
                () => game.FrameworkDefaults[FrameworkSetting.ConfineMouseMode],
                () => Is.EqualTo(ConfineMouseMode.Never));
        }

        [Test]
        public void TestPersistedFullscreenConfinementMigratesToNever()
        {
            AddAssert("persisted fullscreen value loaded",
                () => persistedConfineMouseMode,
                () => Is.EqualTo(ConfineMouseMode.Fullscreen));
            AddAssert("live confinement migrated to never",
                () => frameworkConfig.Get<ConfineMouseMode>(FrameworkSetting.ConfineMouseMode),
                () => Is.EqualTo(ConfineMouseMode.Never));
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(frameworkConfig);
            return dependencies;
        }

        private partial class TestGarbusGame : GarbusGame
        {
            public IReadOnlyDictionary<FrameworkSetting, object> FrameworkDefaults =>
                (IReadOnlyDictionary<FrameworkSetting, object>)GetFrameworkConfigDefaults();
        }

        private void seedFrameworkConfig(ConfineMouseMode confineMouseMode)
        {
            frameworkConfig.Save();

            using (var persistedConfig = new FrameworkConfigManager(frameworkStorage))
                persistedConfig.SetValue(FrameworkSetting.ConfineMouseMode, confineMouseMode);

            frameworkConfig.Load();
            frameworkConfig.Save();
            persistedConfineMouseMode = frameworkConfig.Get<ConfineMouseMode>(FrameworkSetting.ConfineMouseMode);
        }
    }
}
