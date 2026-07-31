// Interactive tuning scene for the gameplay jacket background: disc/wash dim, blur sigma, and
// framebuffer scale are sliders, with a dropdown to pick any jacket bundled in the game resources
// (resolved dynamically, so the scene never depends on specific song content existing) or a
// generated multi-color blob stand-in, and an empty playfield on top for ring alignment.
// [Explicit] — eyeball scene, pick it in the visual test browser.

using System;
using System.IO;
using System.Linq;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using Garbus.Resources;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.IO.Stores;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Garbus.Game.Tests.Tuning
{
    [TestFixture]
    [Explicit]
    public partial class TestSceneJacketBackgroundTuning : GarbusTestScene
    {
        private const string generated_option = "(generated blobs)";

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        // The game-level texture store (rooted at Textures/), the same store song select's
        // ResourceChartSource loads bundled jackets from.
        [Resolved]
        private TextureStore textures { get; set; } = null!;

        private Texture generatedJacket = null!;
        private Container content = null!;
        private BasicDropdown<string> jacketDropdown = null!;

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
            generatedJacket = createTestJacket(renderer);

            // The dropdown lives beside the rebuilt content, not inside it, so the selection
            // control survives every slider-driven rebuild.
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    content = new Container { RelativeSizeAxes = Axes.Both },
                    jacketDropdown = new BasicDropdown<string>
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Margin = new MarginPadding(10),
                        Width = 260,
                    },
                },
            };

            jacketDropdown.Items = new[] { generated_option }
                                   .Concat(new DllResourceStore(typeof(GarbusResources).Assembly).GetAvailableResources()
                                                                                                 .Where(n => n.StartsWith("Textures/Jackets/", StringComparison.OrdinalIgnoreCase))
                                                                                                 .Select(Path.GetFileName)
                                                                                                 .Where(n => !string.IsNullOrEmpty(n))
                                                                                                 .Select(n => n!)
                                                                                                 .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                                   .ToArray();
            jacketDropdown.Current.Value = generated_option;
            jacketDropdown.Current.BindValueChanged(_ => scheduleRebuild());

            rebuild();
        }

        private void scheduleRebuild() => Scheduler.AddOnce(rebuild);

        private Texture? currentJacket()
        {
            if (!showJacket)
                return null;

            if (jacketDropdown.Current.Value == generated_option)
                return generatedJacket;

            // Same name shape ResourceChartSource uses: Jackets/<file with extension>.
            return textures.Get($"Jackets/{jacketDropdown.Current.Value}");
        }

        private void rebuild()
        {
            if (generatedJacket == null)
                return;

            content.Children = new Drawable[]
            {
                // PlayScreen's base box, so dim levels read against the real backdrop.
                new Box
                {
                    Colour = new Colour4(18, 18, 26, 255),
                    RelativeSizeAxes = Axes.Both,
                },
                new JacketBackground(currentJacket())
                {
                    DiscBrightness = discBrightness,
                    WashBrightness = washBrightness,
                    WashBlurSigma = new Vector2(blurSigma),
                    WashFrameBufferScale = frameBufferScale,
                },
                // Empty playfield on top: shows the ring so disc alignment can be eyeballed.
                new GarbusPlayfield(interactive: false),
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
