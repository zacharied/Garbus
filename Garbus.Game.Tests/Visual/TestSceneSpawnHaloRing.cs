// Pins the spawn halo ring's radius against the halo formula in
// docs/presentation-specs/Playfield.md ("Spawn halo and spawn phase").
//
// Calibration anchor — a 460x460 playfield less its 30px padding gives the ring 400x400, so
// ScrollLength is 200. Hand-derived from haloRadius = ScrollLength * SpawnHaloFraction:
//   fraction 0.25 on ScrollLength 200 -> radius 50
//   fraction 0.10 on ScrollLength 200 -> radius 20
//   fraction 0.25 on ScrollLength 100 -> radius 25   (playfield 260 -> content 200)

using System.Linq;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Input;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSpawnHaloRing : GarbusTestScene
    {
        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        private GarbusPlayfield playfield = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("set halo fraction", () => scrollingInfo.SpawnHaloFraction.Value = 0.25);

            AddStep("build playfield", () => Child = new GarbusInputManager
            {
                Child = playfield = new GarbusPlayfield
                {
                    RelativeSizeAxes = Axes.None,
                    Size = new Vector2(460),
                },
            });

            AddUntilStep("scroll length 200", () =>
                Precision.AlmostEquals(scrollingContainer().ScrollLength, 200, 0.001));
        }

        private GarbusScrollingHitObjectContainer scrollingContainer()
            => playfield.ChildrenOfType<GarbusScrollingHitObjectContainer>().First();

        // The ring's drawn radius is half its own draw width — a public framework property, so this
        // asserts real geometry without any test-only member on the production type.
        private float drawnRadius() => playfield.ChildrenOfType<SpawnHaloRing>().Single().DrawSize.X / 2;

        [Test]
        public void TestRadiusIsHaloFractionOfScrollLength()
        {
            AddUntilStep("radius 50", () => Precision.AlmostEquals(drawnRadius(), 50, 0.5));
        }

        [Test]
        public void TestLiveFractionChangeMovesTheRing()
        {
            AddUntilStep("radius 50", () => Precision.AlmostEquals(drawnRadius(), 50, 0.5));

            AddStep("shrink halo fraction", () => scrollingInfo.SpawnHaloFraction.Value = 0.1);
            AddUntilStep("radius 20", () => Precision.AlmostEquals(drawnRadius(), 20, 0.5));
        }

        [Test]
        public void TestRingTracksPlayfieldResize()
        {
            AddUntilStep("radius 50", () => Precision.AlmostEquals(drawnRadius(), 50, 0.5));

            // 260 less the 30px padding each side leaves 200, halving ScrollLength to 100.
            AddStep("halve the playfield content", () => playfield.Size = new Vector2(260));
            AddUntilStep("radius 25", () => Precision.AlmostEquals(drawnRadius(), 25, 0.5));
        }

        [Test]
        public void TestZeroFractionDrawsNoRing()
        {
            AddStep("zero the halo fraction", () => scrollingInfo.SpawnHaloFraction.Value = 0);
            AddUntilStep("ring has no extent", () => Precision.AlmostEquals(drawnRadius(), 0, 0.001));
        }
    }
}
