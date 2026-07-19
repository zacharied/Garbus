// A GarbusInputManager resolves the cached KeyBindingStore and reflects its overrides. The store is
// cached by a wrapper container (in CreateChildDependencies, before the input manager loads).

using System.IO;
using System.Linq;
using Garbus.Game.Input;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneKeyBindingInput : GarbusTestScene
    {
        private string tempDir = null!;

        [Test]
        public void TestInputManagerReflectsStoreOverride()
        {
            GarbusInputManager inputManager = null!;

            AddStep("cache overriding store + create input manager", () =>
            {
                tempDir = Directory.CreateTempSubdirectory("garbus-kb-").FullName;
                var store = new KeyBindingStore(new NativeStorage(tempDir));
                store.Rebind(GarbusAction.ButtonE1, InputKey.Joystick2);

                Child = new StoreProvidingContainer(store)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = inputManager = new GarbusInputManager { RelativeSizeAxes = Axes.Both },
                };
            });

            AddUntilStep("E1 bound to Joystick2", () =>
                inputManager.ActiveKeyBindings.Single(b => (GarbusAction)b.Action == GarbusAction.ButtonE1)
                    .KeyCombination.Keys.Single() == InputKey.Joystick2);

            AddStep("cleanup", () => Directory.Delete(tempDir, true));
        }

        // Caches the store before its child (the input manager) resolves dependencies.
        private partial class StoreProvidingContainer : Container
        {
            private readonly KeyBindingStore store;

            public StoreProvidingContainer(KeyBindingStore store) => this.store = store;

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var deps = new DependencyContainer(base.CreateChildDependencies(parent));
                deps.CacheAs(store);
                return deps;
            }
        }
    }
}
