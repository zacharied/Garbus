// Headless pins for the gameplay jacket background (spec:
// docs/superpowers/specs/2026-07-30-jacket-background-design.md): the jacket disc is inscribed in
// the same padded area as the judgement ring (alignment relation, not a styling pin), and a null
// jacket produces no layers at all.

using System;
using System.Linq;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    public partial class TestSceneJacketBackground : GarbusTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private JacketBackground background = null!;
        private GarbusPlayfield playfield = null!;

        [Test]
        public void TestDiscAlignsWithRingCircle()
        {
            // Host the background and a playfield in the same non-square area, as PlayScreen does.
            AddStep("create background and playfield", () => Child = new Container
            {
                Size = new Vector2(800, 600),
                Children = new Drawable[]
                {
                    background = new JacketBackground(renderer.WhitePixel),
                    playfield = new GarbusPlayfield(interactive: false),
                },
            });
            AddUntilStep("loaded", () => background.IsLoaded && playfield.IsLoaded);

            // The ring's Arc inscribes its circle in min(width, height) of the playfield's padded
            // area; the disc must match it exactly. Calibration anchor (hand-derived): with the
            // playfield padding of 30 on every side, min(800 − 60, 600 − 60) = 540.
            AddAssert("disc diameter equals ring diameter", () =>
            {
                var disc = background.ChildrenOfType<CircularContainer>().Single();
                var ring = playfield.ChildrenOfType<Ring>().Single();
                float ringDiameter = Math.Min(ring.DrawSize.X, ring.DrawSize.Y);
                return Precision.AlmostEquals(disc.DrawSize.X, ringDiameter, 0.5f)
                       && Precision.AlmostEquals(disc.DrawSize.Y, ringDiameter, 0.5f);
            });
        }

        [Test]
        public void TestNullJacketAddsNoLayers()
        {
            AddStep("create with null jacket", () => Child = background = new JacketBackground(null)
            {
                RelativeSizeAxes = Axes.Both,
            });
            AddUntilStep("loaded", () => background.IsLoaded);
            AddAssert("no sprite layers", () => !background.ChildrenOfType<Sprite>().Any());
        }
    }
}
