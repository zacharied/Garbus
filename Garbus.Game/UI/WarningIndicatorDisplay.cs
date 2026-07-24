using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using Garbus.Game.Core;
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
///
/// This is the default (gameplay) presentation. The shared reveal/breathe machinery lives here; the two
/// visual seams — <see cref="CreateSideArc"/> (how a side's drawable is built) and
/// <see cref="UpdateSideArcLayout"/> (per-frame layout) — are overridden by <see cref="MiniWarningIndicatorDisplay"/>
/// to draw a plain, unblurred arc directly on the ring for the editor mini preview.
/// </summary>
public partial class WarningIndicatorDisplay : CompositeDrawable
{
    /// <summary>How far before an indicated object's StartTime the warning appears, in ms. Tunable.</summary>
    public const double WARNING_TIME = 2000;

    // Shared breathe/fade tuning (used by the base reveal loop below, inherited by all styles). While
    // revealed the glow "breathes": its alpha pulses between BREATHE_MIN_ALPHA and full at ~2.7 cycles/sec
    // (one full in+out per BREATHE_PERIOD_MS). The final disappearance is still the plain FadeOut on the
    // hide edge, so it vanishes at exactly the same time as before.
    protected const double BREATHE_PERIOD_MS = 1000d / 2.7d;
    protected const float BREATHE_MIN_ALPHA = 0.35f;
    protected const double FADE_MS = 150;

    /// <summary>Half the arc's angular span, in degrees (90° total across the ring). Shared across styles.</summary>
    protected const float ARC_HALF_WIDTH_DEG = 45f;

    // Blurred-glow tuning. The arc centreline sits just outside the ring (radius_scale = 1.1); the inner half
    // is erased by the ring-circle mask, leaving the outward blur visible as an under-ring glow.
    private const float radius_scale = 1.1f;
    private const float thickness = 100f;
    private const float blur_sigma = 50f;

    private WarningIndicatorSchedule? schedule;

    private readonly Dictionary<HorizontalDirection, SideArc> sideArcs = new();

    // Ring-sized clip masks, one per side, kept a true circle each frame in UpdateSideArcLayout. Populated by
    // the blurred CreateSideArc; a style that draws no mask (mini) simply leaves this empty.
    private readonly Dictionary<HorizontalDirection, Circle> masks = new();

    public WarningIndicatorDisplay()
    {
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        foreach (var side in new[] { HorizontalDirection.Left, HorizontalDirection.Right })
        {
            var colour = side == HorizontalDirection.Left ? Constants.LeftColour : Constants.RightColour;
            var sideArc = CreateSideArc(side, colour);

            sideArcs[side] = sideArc;
            AddInternal(sideArc.FadeTarget);
        }
    }

    /// <summary>
    /// Builds the drawable subtree for one side's warning arc and returns it as a <see cref="SideArc"/>. The
    /// default builds the blurred, ring-clipped under-glow. The returned <see cref="SideArc.FadeTarget"/> is
    /// added to this display and is the drawable whose alpha breathes/fades; <see cref="SideArc.Arc"/> is the
    /// arc whose start/end radians are set to the revealed angle.
    /// </summary>
    protected virtual SideArc CreateSideArc(HorizontalDirection side, ColourInfo colour)
    {
        var arc = new Arc(thickness: thickness)
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both,
            Size = new Vector2(radius_scale),
            Colour = colour,
        };

        // Blur the glow first (base arc shape invisible: DrawOriginal stays false).
        var blurred = new BufferedContainer
        {
            RelativeSizeAxes = Axes.Both,
            BlurSigma = new Vector2(blur_sigma),
            Child = arc,
        };

        // A circle matching the ring; destination-out blending erases the disc interior from the
        // already-blurred glow, leaving a crisp inner clip at the ring edge. Sized each frame in
        // UpdateSideArcLayout so it stays a true circle (min dimension) even when the area is not square.
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
            RelativeSizeAxes = Axes.Both,
            Alpha = 0,
            Children = new Drawable[] { blurred, mask },
        };

        masks[side] = mask;
        return new SideArc(clipped, arc);
    }

    /// <summary>
    /// Per-frame layout of style-specific pieces. The default keeps each blurred style's clip mask a true
    /// ring-sized circle regardless of aspect ratio. <paramref name="diameter"/> is the ring diameter
    /// (min of draw width/height). Styles with no per-frame layout leave this a no-op.
    /// </summary>
    protected virtual void UpdateSideArcLayout(float diameter)
    {
        foreach (var mask in masks.Values)
            mask.Size = new Vector2(diameter);
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

        UpdateSideArcLayout(diameter);

        foreach (var (side, s) in sideArcs)
        {
            var revealed = schedule?.Revealed(side, Time.Current);

            if (revealed is { } x)
            {
                if (s.RevealedAngleDeg != x.AngleDeg)
                {
                    float centre = MathUtils.DegToRad(x.AngleDeg);
                    float half = MathUtils.DegToRad(ARC_HALF_WIDTH_DEG);
                    s.Arc.StartRadians.Value = centre - half;
                    s.Arc.EndRadians.Value = centre + half;
                }

                if (s.RevealedAngleDeg == null)
                    startBreathing(s.FadeTarget);

                s.RevealedAngleDeg = x.AngleDeg;
            }
            else
            {
                if (s.RevealedAngleDeg != null)
                {
                    // Drop the breathing loop, then fade from wherever the pulse currently sits.
                    s.FadeTarget.ClearTransforms();
                    s.FadeTarget.FadeOut(FADE_MS);
                }

                s.RevealedAngleDeg = null;
            }
        }
    }

    /// <summary>
    /// Starts the looping fade-in/fade-out "breathe" on a revealed side's fade target. The first half fades
    /// in from the target's current alpha (0 when freshly revealed), so the initial appearance still reads as
    /// a fade-in; the loop then oscillates between <see cref="BREATHE_MIN_ALPHA"/> and full indefinitely until
    /// the hide edge clears it.
    /// </summary>
    private static void startBreathing(Drawable target)
    {
        const double half = BREATHE_PERIOD_MS / 2;

        target.ClearTransforms();
        target.FadeTo(1f, half, Easing.InOutSine)
              .Then()
              .FadeTo(BREATHE_MIN_ALPHA, half, Easing.InOutSine)
              .Loop();
    }

    /// <summary>
    /// One side's warning arc. <see cref="FadeTarget"/> is added to the display and is the drawable whose
    /// alpha breathes/fades; <see cref="Arc"/> is the arc whose radians track the revealed angle. For the
    /// blurred style these differ (fade the composited buffer, set radians on the inner arc); for a plain
    /// arc they are the same drawable.
    /// </summary>
    protected sealed class SideArc
    {
        public readonly Drawable FadeTarget;
        public readonly Arc Arc;
        public int? RevealedAngleDeg;

        public SideArc(Drawable fadeTarget, Arc arc)
        {
            FadeTarget = fadeTarget;
            Arc = arc;
        }
    }
}
