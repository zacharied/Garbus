// Interactive tuning scene for the gameplay jacket background: disc/wash dim, blur sigma, and
// framebuffer scale are sliders, over a generated multi-color blob jacket (never real song
// content) with an empty playfield on top for ring alignment. [Explicit] — eyeball scene, pick it
// in the visual test browser.

using System;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Garbus.Game.Tests.Tuning
{
    [TestFixture]
    [Explicit]
    public partial class TestSceneJacketBackgroundTuning : GarbusTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private Texture jacket = null!;

        // Defaults mirror JacketBackground's init-property defaults; tweak there once chosen here.
        private float discBrightness = 0.2f;
        private float washBrightness = 0.55f;
        private float blurSigma = 5;
        private float frameBufferScale = 0.05f;
        private bool showJacket = true;

        public TestSceneJacketBackgroundTuning()
        {
            AddSliderStep("disc brightness", 0f, 1f, discBrightness, v => { discBrightness = v; scheduleRebuild(); });
            AddSliderStep("wash brightness", 0f, 1f, washBrightness, v => { washBrightness = v; scheduleRebuild(); });
            AddSliderStep("wash blur sigma", 0f, 20f, blurSigma, v => { blurSigma = v; scheduleRebuild(); });
            AddSliderStep("wash framebuffer scale", 0.01f, 1f, frameBufferScale, v => { frameBufferScale = v; scheduleRebuild(); });
            AddToggleStep("jacket present", v => { showJacket = v; scheduleRebuild(); });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            jacket = createTestJacket(renderer);
            rebuild();
        }

        private void scheduleRebuild() => Scheduler.AddOnce(rebuild);

        private void rebuild()
        {
            if (jacket == null)
                return;

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // PlayScreen's base box, so dim levels read against the real backdrop.
                    new Box
                    {
                        Colour = new Colour4(18, 18, 26, 255),
                        RelativeSizeAxes = Axes.Both,
                    },
                    new JacketBackground(showJacket ? jacket : null)
                    {
                        DiscBrightness = discBrightness,
                        WashBrightness = washBrightness,
                        WashBlurSigma = new Vector2(blurSigma),
                        WashFrameBufferScale = frameBufferScale,
                    },
                    // Empty playfield on top: shows the ring so disc alignment can be eyeballed.
                    new GarbusPlayfield(interactive: false),
                },
            };
        }

        /// <summary>
        /// A colorful square stand-in jacket: four soft color blobs on a dark base, enough hue
        /// variety to judge how the wash dissolves art into component colors.
        /// </summary>
        private static Texture createTestJacket(IRenderer renderer)
        {
            const int size = 256;

            var blobs = new (float cx, float cy, Rgba32 colour)[]
            {
                (0.25f, 0.3f, new Rgba32(220, 60, 60)),
                (0.75f, 0.25f, new Rgba32(60, 120, 220)),
                (0.3f, 0.75f, new Rgba32(240, 200, 70)),
                (0.7f, 0.7f, new Rgba32(90, 200, 120)),
            };

            var image = new Image<Rgba32>(size, size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = 25, g = 25, b = 35;

                    foreach (var blob in blobs)
                    {
                        float dx = x / (float)size - blob.cx;
                        float dy = y / (float)size - blob.cy;
                        float w = MathF.Exp(-(dx * dx + dy * dy) / 0.03f);
                        r += blob.colour.R * w;
                        g += blob.colour.G * w;
                        b += blob.colour.B * w;
                    }

                    image[x, y] = new Rgba32((byte)Math.Min(255, r), (byte)Math.Min(255, g), (byte)Math.Min(255, b));
                }
            }

            var texture = renderer.CreateTexture(size, size);
            texture.SetData(new TextureUpload(image));
            return texture;
        }
    }
}
