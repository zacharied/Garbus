// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/PlayfieldRadialLines.cs).

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Layout;
using osuTK;

namespace Garbus.Game.UI;

/// <summary>
/// Four decorative radial lines (spokes) at the 45° diagonals — the directions between the four
/// <see cref="Core.CardinalDirection"/>s. Each spoke runs from the playfield centre out to the ring,
/// using a horizontal gradient so it fades to transparent before it reaches the centre. Follows the
/// same polar convention as <see cref="Arc"/>: <c>x = cos(θ)·r</c>, <c>y = -sin(θ)·r</c>.
/// </summary>
public sealed partial class PlayfieldRadialLines : Container
{
    // The diagonals between the four cardinal directions (multiples of 45° that are not multiples of 90°).
    private static readonly float[] angles_radians =
    {
        MathF.PI / 4, // 45°
        3 * MathF.PI / 4, // 135°
        5 * MathF.PI / 4, // 225°
        7 * MathF.PI / 4 // 315°
    };

    /// <summary>
    /// Fraction of the radius each spoke spans, measured inward from the ring. The remaining inner
    /// portion is left empty, so a spoke is only visible over the outer 30% of the way to the centre.
    /// </summary>
    private const float visible_fraction = 0.3f;

    public BindableFloat Thickness { get; } = new BindableFloat(3);

    private readonly LayoutValue layoutCache = new LayoutValue(Invalidation.DrawSize);
    private readonly List<Box> lines = new List<Box>(angles_radians.Length);

    public PlayfieldRadialLines()
    {
        AddLayout(layoutCache);

        RelativeSizeAxes = Axes.Both;
        Size = Vector2.One;

        Thickness.ValueChanged += _ => layoutCache.Invalidate();
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        foreach (var _ in angles_radians)
        {
            var line = new Box
            {
                // Origin at the centre-left so the box grows outward from the playfield centre,
                // matching how Arc positions its segments.
                Origin = Anchor.CentreLeft,
                // Fade from fully transparent at the inner (centre) end to opaque at the ring. The
                // gradient is authored in the box's local space and rotates with it.
                Colour = ColourInfo.GradientHorizontal(Colour4.White.Opacity(0f), Colour4.White),
            };

            lines.Add(line);
            AddInternal(line);
        }
    }

    protected override void Update()
    {
        base.Update();
        if (layoutCache.IsValid)
            return;

        var centre = ChildSize / 2;
        float radius = MathF.Min(ChildSize.X, ChildSize.Y) / 2;
        float innerRadius = radius * (1 - visible_fraction);
        float length = radius - innerRadius;

        for (int i = 0; i < lines.Count; i++)
        {
            float theta = angles_radians[i];
            var inner = positionAt(theta, innerRadius);
            var direction = positionAt(theta, radius) - inner;

            var line = lines[i];
            line.Alpha = length > 0 && Thickness.Value > 0 ? 1 : 0;
            line.Position = centre + inner;
            line.Size = new Vector2(length, Thickness.Value);
            line.Rotation = MathF.Atan2(direction.Y, direction.X) * 180 / MathF.PI;
        }

        layoutCache.Validate();
    }

    private static Vector2 positionAt(float radians, float radius) => new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);
}
