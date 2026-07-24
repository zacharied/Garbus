using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Layout;
using osuTK;

namespace Garbus.Game.UI;

/// <summary>
/// Draws a circular arc — or, at a full <c>2π</c> span, the playfield's outer ring — in the polar
/// coordinate system used throughout the playfield (<c>x = cos(θ)·r, y = -sin(θ)·r</c>, so θ = 0 points
/// right and θ increases counter-clockwise).
///
/// Rendering is delegated to osu!framework's <see cref="SmoothPath"/>: the arc is tessellated into
/// <see cref="Resolution"/> straight sub-segments and handed over as a vertex list, so thickness,
/// rounded joints and anti-aliasing come for free. A full circle simply has coincident first/last
/// vertices, and the path's rounded end caps close the seam.
/// </summary>
public sealed partial class Arc : Container
{
    /// <summary>
    /// Number of straight sub-segments the arc is tessellated into across its whole span.
    /// </summary>
    public int Resolution { get; init; } = 32;

    public BindableFloat StartRadians { get; }
    public BindableFloat EndRadians { get; }
    public BindableFloat Thickness { get; }

    private readonly LayoutValue layoutCache = new LayoutValue(Invalidation.DrawSize);

    // SmoothPath forces its own draw colour to white, so this Arc's Colour/Alpha tints the path via the
    // framebuffer blit. Anchored at the container centre; Position (set per regen) compensates for the
    // auto-size bounding-box offset so the polar origin (0,0) lands there.
    private readonly SmoothPath path = new SmoothPath
    {
        Anchor = Anchor.Centre,
    };

    // Reused each regeneration to avoid per-frame allocation while StickIndicator sweeps the arc.
    private readonly List<Vector2> vertices = new();

    public Arc(float startRadians = 0, float endRadians = 0, float thickness = 5)
    {
        AddLayout(layoutCache);

        RelativeSizeAxes = Axes.Both;
        Size = Vector2.One;

        StartRadians = new BindableFloat(startRadians);
        EndRadians = new BindableFloat(endRadians);
        Thickness = new BindableFloat(thickness);

        StartRadians.ValueChanged += _ => layoutCache.Invalidate();
        EndRadians.ValueChanged += _ => layoutCache.Invalidate();
        Thickness.ValueChanged += _ => layoutCache.Invalidate();
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(path);
    }

    protected override void Update()
    {
        base.Update();
        if (layoutCache.IsValid)
            return;

        regeneratePath();
        layoutCache.Validate();
    }

    private void regeneratePath()
    {
        path.PathRadius = Thickness.Value / 2;

        vertices.Clear();

        // Inset the centreline by half the thickness so the stroke's outer edge sits on the bounding box
        // (mirrors the old box placement, where the line was centred on this radius).
        float radius = (MathF.Min(ChildSize.X, ChildSize.Y)) / 2;
        float angleSpan = EndRadians.Value - StartRadians.Value;

        if (radius > 0 && Thickness.Value > 0 && MathF.Abs(angleSpan) > 0.0001f)
        {
            float step = angleSpan / Resolution;

            // Resolution + 1 vertices span [Start, End]; at a full 2π the last coincides with the first.
            for (int i = 0; i <= Resolution; i++)
            {
                float angle = StartRadians.Value + step * i;
                vertices.Add(positionAt(angle, radius));
            }
        }

        // Vertices setter copies the list, so reusing the buffer afterwards is safe.
        path.Vertices = vertices;

        // Undo Path's auto-size bounding-box offset so vertex (0,0) — the polar origin — lands on the
        // container centre (where the SmoothPath is anchored).
        path.Position = -path.PositionInBoundingBox(Vector2.Zero);
    }

    private static Vector2 positionAt(float radians, float radius) => new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);
}
