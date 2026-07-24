using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A split, inward-pointing spike attached to a slider's moving contact point on the ring.
/// </summary>
public partial class SliderContactSpikes : Container
{
    private const float tip_radius_scale = 0.22f;
    private const float blade_half_width_deg = 1.15f;
    public const float GapHalfWidthDeg = 0.7f;
    private const float glow_opacity = 0.9f;

    public bool Active { get; private set; }
    public float ContactAngleRadians { get; private set; }
    public ColourInfo SideColour { get; }

    private SpikeBlade[] blades = null!;

    public IReadOnlyList<SpikeBlade> Blades => blades;

    public SliderContactSpikes(ColourInfo colour)
    {
        SideColour = colour;
        RelativeSizeAxes = Axes.Both;
        AlwaysPresent = true;
        Alpha = 0;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddRangeInternal(blades =
        [
            new SpikeBlade(SideColour),
            new SpikeBlade(SideColour),
        ]);
    }

    public void SetContact(float angleRadians, float ringRadius, bool active)
    {
        Active = active && ringRadius > 0;
        ContactAngleRadians = angleRadians;
        Alpha = Active ? 1 : 0;

        if (!Active)
            return;

        float angularOffset = toRadians(GapHalfWidthDeg + blade_half_width_deg);
        float tipRadius = ringRadius * tip_radius_scale;

        blades[0].SetGeometry(angleRadians - angularOffset, ringRadius, tipRadius, blade_half_width_deg, glow_opacity);
        blades[1].SetGeometry(angleRadians + angularOffset, ringRadius, tipRadius, blade_half_width_deg, glow_opacity);
    }

    private static float toRadians(float degrees) => degrees * MathF.PI / 180;
}

/// <summary>One half of the split contact spike.</summary>
public sealed partial class SpikeBlade : Container
{
    public float AngleRadians { get; private set; }
    public float BaseRadius { get; private set; }
    public float TipRadius { get; private set; }

    private readonly ColourInfo colour;
    private readonly Triangle triangle = new() { EdgeSmoothness = Vector2.One };
    private BufferedContainer glow = null!;

    public SpikeBlade(ColourInfo colour)
    {
        this.colour = colour;
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(glow = new GlowEffect
        {
            Colour = colour,
            BlurSigma = new Vector2(8),
            Strength = 1.5f,
            Placement = EffectPlacement.Behind,
            PadExtent = true,
        }.ApplyTo(triangle));

        glow.Anchor = Anchor.Centre;
        glow.Origin = Anchor.BottomCentre;
    }

    public void SetGeometry(float angleRadians, float baseRadius, float tipRadius, float halfWidthDeg, float opacity)
    {
        AngleRadians = angleRadians;
        BaseRadius = MathF.Max(0, baseRadius);
        TipRadius = MathF.Max(0, tipRadius);

        Vector2 outward = polarToCartesian(angleRadians, 1);
        Vector2 basePosition = outward * BaseRadius;
        Vector2 tipDirection = TipRadius >= BaseRadius ? outward : -outward;
        float length = MathF.Abs(TipRadius - BaseRadius);
        float widthReferenceRadius = MathF.Max(BaseRadius, TipRadius);

        glow.Position = basePosition - tipDirection * glow.Padding.Bottom;
        glow.Rotation = (TipRadius >= BaseRadius ? 90 : 270) - angleRadians * 180 / MathF.PI;
        triangle.Size = new Vector2(2 * widthReferenceRadius * MathF.Tan(toRadians(halfWidthDeg)), length);

        Colour4 baseColour = colour.TopLeft;
        triangle.Colour = ColourInfo.GradientVertical(
            Colour4.White.Opacity(0),
            baseColour.Opacity(opacity));
    }

    private static Vector2 polarToCartesian(float radians, float radius) =>
        new(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);

    private static float toRadians(float degrees) => degrees * MathF.PI / 180;
}
