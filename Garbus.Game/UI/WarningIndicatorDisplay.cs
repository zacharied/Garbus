using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using Garbus.Game.Utils;
using osuTK;

namespace Garbus.Game.UI;

/// <summary>
/// Draws a blurred colored arc around the outside of the playfield for an approaching slider head or
/// SlamCentered (GAR-3). One arc per Side; the reveal logic lives in <see cref="WarningIndicatorSchedule"/>.
/// </summary>
public sealed partial class WarningIndicatorDisplay : CompositeDrawable
{
    /// <summary>How far before an indicated object's StartTime the warning appears, in ms. Tunable.</summary>
    public const double WARNING_TIME = 600;

    // Visual tuning. radius_scale sits just inside 1.0 so the outward blur has headroom inside the
    // BufferedContainer framebuffer (the arc renders around the outside of the ring).
    private const float radius_scale = 0.94f;
    private const float thickness = 16f;
    private const float blur_sigma = 8f;
    private const float arc_half_width_deg = 15f;
    private const double fade_ms = 150;

    private WarningIndicatorSchedule? schedule;

    private readonly Dictionary<HorizontalDirection, SideArc> sideArcs = new();

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
                Colour = side == HorizontalDirection.Left ? Colour4.Blue : Colour4.Red,
            };

            var buffer = new BufferedContainer
            {
                RelativeSizeAxes = Axes.Both,
                BlurSigma = new Vector2(blur_sigma),
                Alpha = 0,
                Child = arc,
            };

            sideArcs[side] = new SideArc(buffer, arc);
            AddInternal(buffer);
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

        foreach (var (side, s) in sideArcs)
        {
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
                    s.Buffer.FadeIn(fade_ms);

                s.RevealedAngleDeg = x.AngleDeg;
            }
            else
            {
                if (s.RevealedAngleDeg != null)
                    s.Buffer.FadeOut(fade_ms);

                s.RevealedAngleDeg = null;
            }
        }
    }

    private sealed class SideArc
    {
        public readonly BufferedContainer Buffer;
        public readonly Arc Arc;
        public int? RevealedAngleDeg;

        public SideArc(BufferedContainer buffer, Arc arc)
        {
            Buffer = buffer;
            Arc = arc;
        }
    }
}
