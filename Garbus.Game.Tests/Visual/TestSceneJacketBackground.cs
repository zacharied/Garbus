// Headless pins for the gameplay jacket background (spec:
// docs/superpowers/specs/2026-07-30-jacket-background-design.md): the jacket disc is inscribed in
// the same padded area as the judgement ring (alignment relation, not a styling pin), a null
// jacket produces no layers at all, and the jacket sprites size to their layers rather than to the
// texture's pixel dimensions.

using System;
using System.Linq;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
        public void TestSpritesSizeToLayersNotTexture()
        {
            // A texture larger than 1x1 exposes osu-framework's sizing trap: Sprite.Texture fills a
            // zero Size with the texture's pixel size, which a later RelativeSizeAxes.Both assignment
            // reinterprets as a relative factor, blowing the sprite up to texture-size × its layer.
            // (renderer.WhitePixel is 1x1, so it cannot catch this.)
            AddStep("create with 64x64 jacket", () => Child = new Container
            {
                Size = new Vector2(800, 600),
                Child = background = new JacketBackground(createTexture(renderer)),
            });
            AddUntilStep("loaded", () => background.IsLoaded);

            AddAssert("disc sprite fills the disc", () =>
            {
                var disc = background.ChildrenOfType<CircularContainer>().Single();
                var sprite = disc.ChildrenOfType<Sprite>().Single();
                return Precision.AlmostEquals(sprite.DrawSize.X, disc.DrawSize.X, 0.5f)
                       && Precision.AlmostEquals(sprite.DrawSize.Y, disc.DrawSize.Y, 0.5f);
            });

            // Square texture + FillMode.Fill in the 800x600 host → an 800x800 square (hand-derived:
            // fill scales the shorter axis up to the longer one at aspect ratio 1).
            AddAssert("wash sprite fills the screen square", () =>
            {
                var wash = background.ChildrenOfType<BufferedContainer>().Single();
                var sprite = wash.ChildrenOfType<Sprite>().Single();
                return Precision.AlmostEquals(sprite.DrawSize.X, 800, 0.5f)
                       && Precision.AlmostEquals(sprite.DrawSize.Y, 800, 0.5f);
            });
        }

        private static Texture createTexture(IRenderer renderer)
        {
            // No using: the queued TextureUpload owns and disposes the image after upload.
            var image = new Image<Rgba32>(64, 64);
            var texture = renderer.CreateTexture(64, 64);
            texture.SetData(new TextureUpload(image));
            return texture;
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
