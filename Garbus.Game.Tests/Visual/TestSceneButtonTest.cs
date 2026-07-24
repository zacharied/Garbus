// The button-test panel reflects bound actions: pressing a bound gamepad button lights the matching
// NESW/shoulder cell, and moving an analog stick drives its dot. A temp store is cached before the
// panel's embedded GarbusInputManager resolves it, so the assertions run against known defaults.

using System.IO;
using Garbus.Game.Core;
using Garbus.Game.Input;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osuTK.Input;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneButtonTest : GarbusTestScene
    {
        private string tempDir = null!;
        private ButtonTestPanel panel = null!;
        private ManualInputManager manual = null!;
        private JoystickSentinel sentinel = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create panel", () =>
            {
                tempDir = Directory.CreateTempSubdirectory("garbus-bt-").FullName;
                var store = new KeyBindingStore(new NativeStorage(tempDir));

                Child = new StoreProvidingContainer(store)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = manual = new ManualInputManager
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                sentinel = new JoystickSentinel(),
                                panel = new ButtonTestPanel
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                },
                            },
                        },
                    },
                };
            });
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("cleanup", () => Directory.Delete(tempDir, true));

        [Test]
        public void TestBoundButtonLightsCell()
        {
            // Default binding: ButtonN1 = JoystickHat1Up (D-Pad North).
            AddStep("press d-pad up", () => manual.PressJoystickButton(JoystickButton.Hat1Up));
            AddAssert("N1 lit", () => panel.IsLit(GarbusAction.ButtonN1));
            AddAssert("N2 not lit", () => !panel.IsLit(GarbusAction.ButtonN2));
            AddStep("release", () => manual.ReleaseJoystickButton(JoystickButton.Hat1Up));
            AddAssert("N1 unlit", () => !panel.IsLit(GarbusAction.ButtonN1));
        }

        [Test]
        public void TestBoundShoulderLightsCell()
        {
            // Default binding: ButtonR = Joystick6.
            AddStep("press right shoulder", () => manual.PressJoystickButton(JoystickButton.Button6));
            AddAssert("R lit", () => panel.IsLit(GarbusAction.ButtonR));
            AddStep("release", () => manual.ReleaseJoystickButton(JoystickButton.Button6));
            AddAssert("R unlit", () => !panel.IsLit(GarbusAction.ButtonR));
        }

        [Test]
        public void TestStickMovesDot()
        {
            AddAssert("left dot centred", () => panel.StickDot(HorizontalDirection.Left).X == 0);

            AddStep("push left stick right", () => manual.Input(new JoystickAxisInput(new[]
            {
                new JoystickAxis(JoystickAxisSource.GamePadLeftStickX, 1f),
                new JoystickAxis(JoystickAxisSource.GamePadLeftStickY, 0f),
            })));
            AddUntilStep("left dot moved right", () => panel.StickDot(HorizontalDirection.Left).X > 0);
            AddAssert("right dot still centred", () => panel.StickDot(HorizontalDirection.Right).X == 0);

            AddStep("release left stick", () => manual.Input(new JoystickAxisInput(new[]
            {
                new JoystickAxis(JoystickAxisSource.GamePadLeftStickX, 0f),
                new JoystickAxis(JoystickAxisSource.GamePadLeftStickY, 0f),
            })));
            AddUntilStep("left dot recentred", () => panel.StickDot(HorizontalDirection.Left).X == 0);
        }

        // Records any raw joystick press it receives. Placed behind the panel so it only fires
        // if a press propagated past the panel instead of being consumed there.
        private partial class JoystickSentinel : Drawable
        {
            public bool Fired { get; private set; }

            public JoystickSentinel() => RelativeSizeAxes = Axes.Both;

            protected override bool OnJoystickPress(JoystickPressEvent e)
            {
                Fired = true;
                return true;
            }
        }

        [Test]
        public void TestBoundButtonConsumesInput()
        {
            // Default binding: ButtonN1 = JoystickHat1Up. Pressing it must light N1 AND stop at the panel.
            AddStep("press d-pad up", () => manual.PressJoystickButton(JoystickButton.Hat1Up));
            AddAssert("N1 lit", () => panel.IsLit(GarbusAction.ButtonN1));
            AddAssert("sentinel behind panel never fired", () => !sentinel.Fired);
            AddStep("release", () => manual.ReleaseJoystickButton(JoystickButton.Hat1Up));
        }

        // Caches the store before its consumer (the panel's embedded input manager) resolves dependencies.
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
