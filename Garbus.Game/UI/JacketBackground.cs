// The gameplay jacket background (spec: docs/superpowers/specs/2026-07-30-jacket-background-design.md).
// Two static layers under the playfield: a screen-filling color wash — the jacket dissolved by a
// one-shot cached downscale+blur framebuffer — and the un-blurred jacket circle-clipped to the
// judgement ring's disc. Receives its texture; performs no store lookups. Null texture → no layers
// (the screen's flat base box shows through).

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace Garbus.Game.UI;

public partial class JacketBackground : CompositeDrawable
{
    /// <summary>Brightness of the circle-clipped jacket disc (spec: 80% dim).</summary>
    public float DiscBrightness { get; init; } = 0.2f;

    /// <summary>Brightness of the blurred wash outside the ring (spec starting point).</summary>
    public float WashBrightness { get; init; } = 0.55f;

    /// <summary>Gaussian sigma applied in the wash framebuffer's (downscaled) pixel space.</summary>
    public Vector2 WashBlurSigma { get; init; } = new Vector2(5);

    /// <summary>Wash framebuffer scale — the downscale factor that dissolves the jacket into colors.</summary>
    public float WashFrameBufferScale { get; init; } = 0.05f;

    private readonly Texture? jacket;

    public JacketBackground(Texture? jacket)
    {
        this.jacket = jacket;
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        if (jacket == null)
            return;

        InternalChildren = new Drawable[]
        {
            // Wash: rendered once into a small cached framebuffer and blurred there, then reused
            // every frame. RedrawOnScale off — a window resize re-stretching the cached wash is
            // invisible at this blur level and skips a re-render.
            new BufferedContainer(cachedFrameBuffer: true)
            {
                RelativeSizeAxes = Axes.Both,
                FrameBufferScale = new Vector2(WashFrameBufferScale),
                BlurSigma = WashBlurSigma,
                RedrawOnScale = false,
                Colour = new Colour4(WashBrightness, WashBrightness, WashBrightness, 1),
                Child = new Sprite
                {
                    // Texture must come after RelativeSizeAxes: Sprite.Texture fills a zero Size
                    // with the texture's pixel size, which Axes.Both would then treat as a
                    // (texture-sized) relative factor.
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = jacket,
                },
            },
            // Disc: the jacket clipped to the playfield circle. Mirrors the playfield's geometry —
            // same padding, circle inscribed in min(width, height) — so it aligns with the ring.
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(GarbusPlayfield.SCREEN_PADDING),
                Child = new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    Child = new Sprite
                    {
                        // Texture after RelativeSizeAxes — same sizing trap as the wash sprite above.
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = new Colour4(DiscBrightness, DiscBrightness, DiscBrightness, 1),
                        Texture = jacket,
                    },
                },
            },
        };
    }
}
