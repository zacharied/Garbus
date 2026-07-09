// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/PlayfieldKeybeam.cs).

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using Garbus.Game.Core;
using Garbus.Game.Input;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Garbus.Game.UI;

/// <summary>
/// A "lane": a 90° quadrant sector centred on one <see cref="CardinalDirection"/>, drawn in the same
/// polar space as the rest of the playfield (<c>x = cos(θ)·r, y = -sin(θ)·r</c>). It is a
/// <see cref="CircularProgress"/> pie slice textured with a radial gradient that runs from transparent at
/// the centre to white where it meets the outer ring, blended additively so it reads as a glow.
///
/// While the matching button is held the beam flashes in to full brightness and settles to
/// <see cref="held_alpha"/>; on release it fades out. It observes input only — <see cref="OnPressed"/>
/// returns <c>false</c> so presses still reach the hit objects.
/// </summary>
public sealed partial class PlayfieldKeybeam : CompositeDrawable, IKeyBindingHandler<GarbusAction>
{
    private const float held_alpha = 0.7f;
    private const double glow_in = 60;
    private const double settle = 260;
    private const double fade_out = 200;

    /// <summary>
    /// How sharply the gradient ramps toward white. &gt;1 keeps the beam dim through the middle and
    /// concentrates the white near the ring; 1 is a straight linear fade.
    /// </summary>
    private const float gradient_exponent = 1.5f;

    private readonly CardinalDirection direction;

    private CircularProgress beam = null!;

    public PlayfieldKeybeam(CardinalDirection direction)
    {
        this.direction = direction;

        RelativeSizeAxes = Axes.Both;
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;

        // Hidden until the button is pressed, but kept present so it stays in the input queue while faded out.
        Alpha = 0;
        AlwaysPresent = true;
    }

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer)
    {
        // CircularProgress sweeps clockwise from the top; a Progress of 0.25 spans 90° centred on the
        // top-right diagonal (45° clockwise from up). Rotating by (45° − direction) re-centres that span
        // on the cardinal direction's on-screen angle (θ = 0 → right, increasing counter-clockwise).
        float angleDeg = direction.ToRadians() * 180f / MathF.PI;

        InternalChild = beam = new CircularProgress
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Progress = 0.25f, // 90° of the full circle
            InnerRadius = 1f, // fill all the way to the centre (the gradient fades the inner part out)
            Rotation = 45f - angleDeg,
            Texture = createGradientTexture(renderer),
            Blending = BlendingParameters.Additive,
        };
    }

    protected override void Update()
    {
        base.Update();

        // Keep the sector a circle (not an ellipse) matching the ring radius = min(width, height) / 2.
        float side = MathF.Min(DrawWidth, DrawHeight);
        beam.Size = new Vector2(side);
    }

    public bool OnPressed(KeyBindingPressEvent<GarbusAction> e)
    {
        if (e.Action.ToCardinalDirection() == direction)
        {
            this.FadeIn(glow_in, Easing.OutQuint)
                .Then()
                .FadeTo(held_alpha, settle, Easing.OutQuint);
        }

        // Observe only — let the press continue on to the hit objects.
        return false;
    }

    public void OnReleased(KeyBindingReleaseEvent<GarbusAction> e)
    {
        if (e.Action.ToCardinalDirection() == direction)
            this.FadeOut(fade_out, Easing.OutQuint);
    }

    /// <summary>
    /// Builds a square radial-gradient texture: transparent at the centre, white at the disk edge.
    /// <see cref="CircularProgress"/> samples this across its inscribed disk, so the white lands on the ring.
    /// </summary>
    private static Texture createGradientTexture(IRenderer renderer)
    {
        const int size = 256;

        var image = new Image<Rgba32>(size, size);
        float centre = (size - 1) / 2f;
        float edge = size / 2f; // pixel distance from centre to the disk edge

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - centre) / edge;
                float dy = (y - centre) / edge;
                float r = Math.Clamp(MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                float alpha = MathF.Pow(r, gradient_exponent);

                image[x, y] = new Rgba32(1f, 1f, 1f, alpha);
            }
        }

        var texture = renderer.CreateTexture(size, size);
        texture.SetData(new TextureUpload(image));
        return texture;
    }
}
