using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Lines;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A geometric stand-in for a Gaussian-blurred stroke: a wide additive path whose cross-section
/// texture carries the blur's falloff profile, so no framebuffer or per-frame blur pass is needed.
///
/// The maths: blurring an opaque stroke of half-width <c>coreRadius</c> with a Gaussian of the
/// given <c>sigma</c> yields, at distance <c>d</c> from the stroke's centreline, alpha
/// <c>Φ((w−d)/σ) − Φ(−(w+d)/σ)</c> (the Gaussian CDF integrated across the stroke). The glow
/// effect this replaces then multiplied that alpha by <c>strength</c>, clipping at 1 — producing
/// a saturated core with a Gaussian skirt. That exact profile is baked into the path's 1-D
/// cross-section texture via <see cref="ColourAt"/>, making the rendered result a per-pixel match
/// for the blur along any straight or gently-curving stretch of the line.
///
/// The texture is white; tint comes from the drawable colour hierarchy, exactly as the blurred
/// framebuffer inherited the tint of the content it buffered.
///
/// Because the profile is baked rather than physically blurred, the skirt is freely shapeable:
/// the <c>falloff</c> exponent bends the visible fade (below 1 = gentler and longer, above 1 =
/// tighter) without touching the saturated core.
/// </summary>
public partial class GlowPath : SmoothPath
{
    private readonly float coreRadius;
    private readonly float sigma;
    private readonly float strength;
    private readonly float falloff;

    /// <summary>
    /// The construction-time parameters that fix this path's cross-section. Baked in at construction
    /// (they determine <see cref="Path.PathRadius"/> and the profile texture), so <see cref="SliderPathPool"/>
    /// buckets instances by this to avoid handing a body a path shaped for a different look.
    /// </summary>
    public readonly record struct Profile(float CoreRadius, float Sigma, float Strength, float Falloff);

    public Profile ProfileKey { get; }

    public GlowPath(Profile profile)
        : this(profile.CoreRadius, profile.Sigma, profile.Strength, profile.Falloff)
    {
    }

    public GlowPath(float coreRadius, float sigma, float strength, float falloff = 1)
    {
        this.coreRadius = coreRadius;
        this.sigma = Math.Max(sigma, 1e-3f);
        this.strength = strength;
        this.falloff = Math.Max(falloff, 1e-3f);

        ProfileKey = new Profile(coreRadius, sigma, strength, falloff);

        Blending = BlendingParameters.Additive;
        PathRadius = computeExtent();
    }

    /// <summary>
    /// The distance from the centreline at which the profile becomes invisible (below half an
    /// 8-bit alpha step), capped at 5 sigma past the core where a Gaussian tail can never matter.
    /// </summary>
    private float computeExtent()
    {
        float step = sigma / 8;
        float extent = coreRadius;

        while (extent < coreRadius + 5 * sigma && profileAlpha(extent + step) >= 1f / 512)
            extent += step;

        return Math.Min(extent + step, coreRadius + 5 * sigma);
    }

    protected override Color4 ColourAt(float position)
        => new Color4(1f, 1f, 1f, profileAlpha((1 - position) * PathRadius));

    // The falloff exponent reshapes the (clipped) profile's skirt: below 1 lifts the tail for a
    // gentler, longer fade; above 1 suppresses it for a tighter glow. 1 is the pure blur profile.
    // Applied after the clip, so it never affects the saturated core, only the visible falloff.
    private float profileAlpha(float distance)
        => MathF.Pow(Math.Clamp(strength * blurredStrokeAlpha(distance), 0f, 1f), falloff);

    /// <summary>
    /// Alpha of an opaque stroke of half-width <see cref="coreRadius"/> under a Gaussian blur of
    /// <see cref="sigma"/>, at <paramref name="distance"/> from the stroke's centreline.
    /// </summary>
    private float blurredStrokeAlpha(float distance)
        => (float)(phi((coreRadius - distance) / sigma) - phi(-(coreRadius + distance) / sigma));

    private static double phi(double x) => 0.5 * (1 + erf(x / Math.Sqrt(2)));

    // Abramowitz & Stegun 7.1.26 — max error ~1.5e-7, ample for an 8-bit texture.
    private static double erf(double x)
    {
        double sign = Math.Sign(x);
        x = Math.Abs(x);

        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }
}
