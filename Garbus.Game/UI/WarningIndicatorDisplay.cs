using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using Garbus.Game.Core;
using Garbus.Game.Gameplay;
using Garbus.Game.Objects;
using Garbus.Game.Utils;
using osuTK;

namespace Garbus.Game.UI;

/// <summary>
/// Draws a blurred colored glow around the outside of the playfield ring for an approaching slider head
/// (GAR-3). One glow per Side; the reveal logic lives in <see cref="WarningIndicatorSchedule"/>.
///
/// The glow is a wide (90°) blurred arc centred on the ring edge, then crisply clipped to the region
/// <em>outside</em> the ring circle so it reads as light emanating from under the ring. The clip is a
/// destination-out mask applied <em>after</em> the blur, so the inner edge stays a clean circle while the
/// outer edge remains soft.
/// </summary>
public sealed partial class WarningIndicatorDisplay : CompositeDrawable
{
    /// <summary>How far before an indicated object's StartTime the warning appears, in ms. Tunable.</summary>
    public const double WARNING_TIME = 2000;

    // Visual tuning. The arc centreline sits on the ring (radius_scale = 1.0); the inner half is erased by
    // the ring-circle mask, leaving the outward blur visible as an under-ring glow.
    private const float radius_scale = 1.1f;
    private const float thickness = 100f;
    private const float blur_sigma = 50f;
    private const float arc_half_width_deg = 45f; // 90° total span across the ring.
    private const double fade_ms = 150;

    // While revealed the glow "breathes": its alpha pulses between breathe_min_alpha and full at
    // ~2.7 cycles/sec (one full in+out per breathe_period_ms). The final disappearance is still the
    // plain FadeOut on the hide edge, so it vanishes at exactly the same time as before.
    private const double breathe_period_ms = 1000d / 2.7d;
    private const float breathe_min_alpha = 0.35f;

    private WarningIndicatorSchedule? schedule;

    private readonly Dictionary<HorizontalDirection, SideArc> sideArcs = new();

    [Resolved(CanBeNull = true)]
    private IGameplayPresentationPolicy? presentationPolicy { get; set; }

    [Resolved(CanBeNull = true)]
    private GarbusPlayfield? playfield { get; set; }

    public WarningIndicatorDisplay()
    {
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        foreach (var side in new[] { HorizontalDirection.Left, HorizontalDirection.Right })
        {
            var arc = new Arc(thickness: thickness)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Size = new Vector2(radius_scale),
                Colour = side == HorizontalDirection.Left ? Constants.LeftColour : Constants.RightColour
            };

            // Blur the glow first (base arc shape invisible: DrawOriginal stays false).
            var blurred = new BufferedContainer
            {
                RelativeSizeAxes = Axes.Both,
                BlurSigma = new Vector2(blur_sigma),
                Child = arc,
            };

            // A circle matching the ring; destination-out blending erases the disc interior from the
            // already-blurred glow, leaving a crisp inner clip at the ring edge. Sized each frame in Update
            // so it stays a true circle (min dimension) even when the playfield area is not square.
            var mask = new Circle
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Blending = new BlendingParameters
                {
                    Source = BlendingType.Zero,
                    Destination = BlendingType.OneMinusSrcAlpha,
                    SourceAlpha = BlendingType.Zero,
                    DestinationAlpha = BlendingType.OneMinusSrcAlpha,
                },
            };

            // Outer buffer composites the crisp mask over the blurred glow (no blur of its own).
            var clipped = new BufferedContainer
            {
                RelativeSizeAxes = presentationPolicy?.ExpandsWarningEffectBufferToPlayfield == true ? Axes.None : Axes.Both,
                Anchor = presentationPolicy?.ExpandsWarningEffectBufferToPlayfield == true ? Anchor.Centre : Anchor.TopLeft,
                Origin = presentationPolicy?.ExpandsWarningEffectBufferToPlayfield == true ? Anchor.Centre : Anchor.TopLeft,
                Alpha = 0,
                Children = new Drawable[] { blurred, mask },
            };

            sideArcs[side] = new SideArc(clipped, arc, mask);
            AddInternal(clipped);
        }
    }

    public void SetHitObjects(IEnumerable<GarbusHitObject> hitObjects)
        => schedule = new WarningIndicatorSchedule(hitObjects, WARNING_TIME);

    /// <summary>The angle (deg) currently revealed for <paramref name="side"/>, or null if hidden. Test-facing.</summary>
    public int? RevealedAngleDeg(HorizontalDirection side)
        => sideArcs.TryGetValue(side, out var s) ? s.RevealedAngleDeg : null;

    protected override void Update()
    {
        base.Update();

        float diameter = MathF.Min(DrawWidth, DrawHeight);
        Vector2 previewBufferSize = Vector2.Zero;

        if (presentationPolicy?.ExpandsWarningEffectBufferToPlayfield == true)
        {
            MarginPadding padding = playfield!.Padding;
            previewBufferSize = DrawSize + new Vector2(padding.TotalHorizontal, padding.TotalVertical);
        }

        foreach (var (side, s) in sideArcs)
        {
            if (presentationPolicy?.ExpandsWarningEffectBufferToPlayfield == true)
            {
                // Preview framebuffers consume the playfield's symmetric outer padding, while the arc
                // remains sized from the unchanged ring diameter rather than the enlarged buffer.
                s.Buffer.Size = previewBufferSize;
                s.Arc.Size = new Vector2(
                    diameter * radius_scale / previewBufferSize.X,
                    diameter * radius_scale / previewBufferSize.Y);
            }

            // Keep the mask a true ring-sized circle regardless of aspect ratio.
            s.Mask.Size = new Vector2(diameter);

            var revealed = schedule?.Revealed(side, Time.Current);

            if (revealed is { } x)
            {
                if (s.RevealedAngleDeg != x.AngleDeg)
                {
                    float centre = MathUtils.DegToRad(x.AngleDeg);
                    float half = MathUtils.DegToRad(arc_half_width_deg);
                    s.Arc.StartRadians.Value = centre - half;
                    s.Arc.EndRadians.Value = centre + half;
                }

                if (s.RevealedAngleDeg == null)
                    startBreathing(s.Buffer);

                s.RevealedAngleDeg = x.AngleDeg;
            }
            else
            {
                if (s.RevealedAngleDeg != null)
                {
                    // Drop the breathing loop, then fade from wherever the pulse currently sits.
                    s.Buffer.ClearTransforms();
                    s.Buffer.FadeOut(fade_ms);
                }

                s.RevealedAngleDeg = null;
            }
        }
    }

    /// <summary>
    /// Starts the looping fade-in/fade-out "breathe" on a revealed side's buffer. The first half fades in
    /// from the buffer's current alpha (0 when freshly revealed), so the initial appearance still reads as a
    /// fade-in; the loop then oscillates between <see cref="breathe_min_alpha"/> and full indefinitely until
    /// the hide edge clears it.
    /// </summary>
    private static void startBreathing(BufferedContainer buffer)
    {
        const double half = breathe_period_ms / 2;

        buffer.ClearTransforms();
        buffer.FadeTo(1f, half, Easing.InOutSine)
              .Then()
              .FadeTo(breathe_min_alpha, half, Easing.InOutSine)
              .Loop();
    }

    private sealed class SideArc
    {
        public readonly BufferedContainer Buffer;
        public readonly Arc Arc;
        public readonly Circle Mask;
        public int? RevealedAngleDeg;

        public SideArc(BufferedContainer buffer, Arc arc, Circle mask)
        {
            Buffer = buffer;
            Arc = arc;
            Mask = mask;
        }
    }
}
