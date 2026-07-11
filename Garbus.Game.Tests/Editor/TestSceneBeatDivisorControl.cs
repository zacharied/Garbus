// Visual/headless tests for the compose beat-divisor control.
using System.Linq;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osuTK;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneBeatDivisorControl : GarbusTestScene
    {
        private Harness harness = null!;
        private GarbusBeatDivisorControl control => harness.Control;
        private BindableBeatDivisor beatDivisor => harness.BeatDivisor;
        private ManualInputManager InputManager => harness.InputManager;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Child = harness = new Harness { RelativeSizeAxes = Axes.Both };
        });

        private void waitForControl() => AddUntilStep("control loaded", () => control?.IsLoaded == true);

        private int tickCount() => control.ChildrenOfType<Circle>().Count();

        [Test]
        public void TestTickCountMatchesLargestPreset()
        {
            waitForControl();
            // COMMON presets {1,2,4,8,16} -> largest 16 -> 17 ticks (indices 0..16 inclusive).
            AddUntilStep("17 ticks for COMMON", () => tickCount() == 17);

            AddStep("switch to TRIPLETS", () => beatDivisor.SetArbitraryDivisor(6, true));
            // TRIPLETS presets {1,3,6,12} -> largest 12 -> 13 ticks.
            AddUntilStep("13 ticks for TRIPLETS", () => tickCount() == 13);
        }

        [Test]
        public void TestMarkerMovesWithDivisor()
        {
            waitForControl();

            float xAt(int divisor)
            {
                beatDivisor.SetArbitraryDivisor(divisor, true);
                return control.ChildrenOfType<EquilateralTriangle>().Single().ScreenSpaceDrawQuad.Centre.X;
            }

            float xLow = 0, xHigh = 0;
            AddStep("marker at 1/2", () => xLow = xAt(2));
            AddStep("marker at 1/16", () => xHigh = xAt(16));
            AddAssert("finer divisor pushes marker right", () => xHigh > xLow);
        }

        private osu.Framework.Graphics.UserInterface.BasicButton button(string name)
            => control.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>().Single(b => b.Name == name);

        private bool hasLabel(string text)
            => control.ChildrenOfType<osu.Framework.Graphics.Sprites.SpriteText>().Any(t => t.Text.ToString() == text);

        [Test]
        public void TestDivisorChevronsCycleWithinCollection()
        {
            waitForControl();
            AddAssert("starts at 1/4", () => hasLabel("1/4"));

            AddStep("click next", () => button("divisor-next").Action?.Invoke());
            AddAssert("advances to 1/8", () => beatDivisor.Value == 8 && hasLabel("1/8"));

            AddStep("click prev", () => button("divisor-prev").Action?.Invoke());
            AddAssert("back to 1/4", () => beatDivisor.Value == 4 && hasLabel("1/4"));
        }

        [Test]
        public void TestTypeChevronCyclesCommonTriplets()
        {
            waitForControl();
            AddAssert("type is common", () => hasLabel("common"));

            AddStep("cycle type forward", () => button("type-next").Action?.Invoke());
            AddAssert("triplets, landing on 1/6",
                () => beatDivisor.ValidDivisors.Value.Type == BeatDivisorType.Triplets && beatDivisor.Value == 6 && hasLabel("triplets"));

            AddStep("cycle type forward again", () => button("type-next").Action?.Invoke());
            AddAssert("skips custom back to common, landing on 1/4",
                () => beatDivisor.ValidDivisors.Value.Type == BeatDivisorType.Common && beatDivisor.Value == 4 && hasLabel("common"));
        }

        [Test]
        public void TestShiftNumberSetsDivisor()
        {
            waitForControl();
            AddStep("press Shift+3", () =>
            {
                InputManager.PressKey(osuTK.Input.Key.ShiftLeft);
                InputManager.PressKey(osuTK.Input.Key.Number3);
                InputManager.ReleaseKey(osuTK.Input.Key.Number3);
                InputManager.ReleaseKey(osuTK.Input.Key.ShiftLeft);
            });
            AddAssert("divisor is 3", () => beatDivisor.Value == 3);
        }

        private void openPopover()
        {
            AddStep("open divisor popover", () =>
                control.ChildrenOfType<GarbusBeatDivisorControl.DivisorDisplayButton>().Single().Action?.Invoke());
            AddUntilStep("popover shown", () => harness.ChildrenOfType<GarbusBeatDivisorControl.CustomDivisorPopover>().Any());
        }

        [Test]
        public void TestCustomDivisorEntry()
        {
            waitForControl();
            GarbusBeatDivisorControl.CustomDivisorPopover popover = null!;
            openPopover();
            AddStep("grab popover", () => popover = harness.ChildrenOfType<GarbusBeatDivisorControl.CustomDivisorPopover>().Single());
            AddStep("commit 5", () => popover.Commit("5"));
            AddAssert("collection is custom, value 5",
                () => beatDivisor.ValidDivisors.Value.Type == BeatDivisorType.Custom && beatDivisor.Value == 5);
            AddAssert("type label shows custom", () => hasLabel("custom"));
        }

        [Test]
        public void TestInvalidCustomEntryIgnored()
        {
            waitForControl();
            GarbusBeatDivisorControl.CustomDivisorPopover popover = null!;
            openPopover();
            AddStep("grab popover", () => popover = harness.ChildrenOfType<GarbusBeatDivisorControl.CustomDivisorPopover>().Single());
            AddAssert("out-of-range rejected", () => popover.Commit("999") == false);
            AddAssert("divisor unchanged", () => beatDivisor.Value == 4);
        }

        private partial class Harness : Container
        {
            public BindableBeatDivisor BeatDivisor { get; } = new BindableBeatDivisor(4);
            public GarbusBeatDivisorControl Control { get; private set; } = null!;
            public ManualInputManager InputManager { get; private set; } = null!;
            private DependencyContainer dependencies = null!;

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.Cache(BeatDivisor);
                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = InputManager = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = new PopoverContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = Control = new GarbusBeatDivisorControl
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(120, 90),
                        },
                    },
                };
            }
        }
    }
}
