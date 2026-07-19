// Plain NUnit over a temp NativeStorage: defaults, partial-file overrides, rebind persistence, reset.

using System.IO;
using System.Linq;
using Garbus.Game.Input;
using NUnit.Framework;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;

namespace Garbus.Game.Tests.Input
{
    [TestFixture]
    public class TestKeyBindingStore
    {
        private string tempDir = null!;
        private NativeStorage storage = null!;

        [SetUp]
        public void SetUp()
        {
            tempDir = Directory.CreateTempSubdirectory("garbus-kb-").FullName;
            storage = new NativeStorage(tempDir);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(tempDir, true);

        [Test]
        public void TestNoFileUsesDefaults()
        {
            var store = new KeyBindingStore(storage);
            Assert.That(store.GetBinding(GarbusAction.ButtonE2), Is.EqualTo(InputKey.Joystick3));
            Assert.That(store.GetBinding(GarbusAction.ButtonL), Is.EqualTo(InputKey.Joystick5));
        }

        [Test]
        public void TestPartialFileOverridesOnlyListedActions()
        {
            File.WriteAllText(Path.Combine(tempDir, "keybindings.json"),
                "{ \"ButtonE2\": \"Joystick1\" }");

            var store = new KeyBindingStore(storage);
            Assert.That(store.GetBinding(GarbusAction.ButtonE2), Is.EqualTo(InputKey.Joystick1));
            // Everything else stays default.
            Assert.That(store.GetBinding(GarbusAction.ButtonL), Is.EqualTo(InputKey.Joystick5));
        }

        [Test]
        public void TestRebindPersistsAndReloads()
        {
            var store = new KeyBindingStore(storage);
            store.Rebind(GarbusAction.ButtonN1, InputKey.Joystick2);

            var reloaded = new KeyBindingStore(storage);
            Assert.That(reloaded.GetBinding(GarbusAction.ButtonN1), Is.EqualTo(InputKey.Joystick2));
        }

        [Test]
        public void TestResetToDefaultsClearsOverrides()
        {
            var store = new KeyBindingStore(storage);
            store.Rebind(GarbusAction.ButtonN1, InputKey.Joystick2);
            store.ResetToDefaults();

            Assert.That(store.GetBinding(GarbusAction.ButtonN1), Is.EqualTo(InputKey.JoystickHat1Up));

            var reloaded = new KeyBindingStore(storage);
            Assert.That(reloaded.GetBinding(GarbusAction.ButtonN1), Is.EqualTo(InputKey.JoystickHat1Up));
        }

        [Test]
        public void TestGetKeyBindingsReflectsOverride()
        {
            var store = new KeyBindingStore(storage);
            store.Rebind(GarbusAction.ButtonE1, InputKey.Joystick2);

            var binding = store.GetKeyBindings()
                .Single(b => (GarbusAction)b.Action == GarbusAction.ButtonE1);
            Assert.That(binding.KeyCombination.Keys.Single(), Is.EqualTo(InputKey.Joystick2));
        }
    }
}
