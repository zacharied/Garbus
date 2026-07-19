// Clicking a row and pressing a gamepad button rebinds that action through the store; Reset restores it.

using System.IO;
using System.Linq;
using Garbus.Game.Input;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osuTK.Input;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneControlsPanel : GarbusTestScene
    {
        private string tempDir = null!;
        private KeyBindingStore store = null!;
        private ControlsPanel panel = null!;
        private ManualInputManager manual = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create panel", () =>
            {
                tempDir = Directory.CreateTempSubdirectory("garbus-kb-").FullName;
                store = new KeyBindingStore(new NativeStorage(tempDir));
                Child = manual = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = panel = new ControlsPanel(store, () => { }),
                };
            });
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("cleanup", () => Directory.Delete(tempDir, true));

        private KeyBindingRow rowFor(GarbusAction action) =>
            panel.ChildrenOfType<KeyBindingRow>().Single(r => r.Action == action);

        [Test]
        public void TestClickAndPressRebinds()
        {
            AddStep("click E1 row", () =>
            {
                manual.MoveMouseTo(rowFor(GarbusAction.ButtonE1));
                manual.Click(MouseButton.Left);
            });
            AddStep("press joystick button 2", () => manual.PressJoystickButton(JoystickButton.Button2));
            AddStep("release", () => manual.ReleaseJoystickButton(JoystickButton.Button2));

            AddAssert("store E1 = Joystick2", () =>
                store.GetBinding(GarbusAction.ButtonE1) == InputKey.Joystick2);
        }

        [Test]
        public void TestResetRestoresDefaults()
        {
            AddStep("rebind E1", () => store.Rebind(GarbusAction.ButtonE1, InputKey.Joystick2));
            AddStep("click reset", () =>
            {
                var reset = panel.ChildrenOfType<SpriteText>().First(t => t.Text.ToString() == "Reset to defaults");
                manual.MoveMouseTo(reset);
                manual.Click(MouseButton.Left);
            });
            AddAssert("store E1 back to default", () =>
                store.GetBinding(GarbusAction.ButtonE1) == InputKey.JoystickHat1Right);
        }
    }
}
