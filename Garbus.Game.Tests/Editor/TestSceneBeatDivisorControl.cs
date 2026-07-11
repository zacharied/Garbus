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
using osu.Framework.Testing;
using osuTK;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneBeatDivisorControl : GarbusTestScene
    {
        private Harness harness = null!;
        private GarbusBeatDivisorControl control => harness.Control;
        private BindableBeatDivisor beatDivisor => harness.BeatDivisor;

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

        private partial class Harness : Container
        {
            public BindableBeatDivisor BeatDivisor { get; } = new BindableBeatDivisor(4);
            public GarbusBeatDivisorControl Control { get; private set; } = null!;
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
                Child = new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = Control = new GarbusBeatDivisorControl
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(120, 90),
                    },
                };
            }
        }
    }
}
