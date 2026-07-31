using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Lines;
using osuTK;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A shared free-list of <see cref="SmoothPath"/> and <see cref="GlowPath"/> instances for slider bodies.
///
/// Every <see cref="osu.Framework.Graphics.Lines.Path"/> draws through a buffered draw node — its own
/// framebuffer — and on first draw allocates a GPU vertex batch of 9000 quads across up to 10 buffers.
/// Hit objects are not pooled, so each <see cref="DrawableSliderBody"/> would otherwise construct its own
/// set of paths the first time it renders and hold them for the rest of the chart. The live instance count
/// then tracks how many sliders the chart contains rather than how many are on screen, and the driver pays
/// for every one of them: creating those textures and buffers mid-frame blocks the draw thread.
///
/// Bodies rent while alive and return in <see cref="DrawableSliderBody.OnKilled"/>, capping live instances
/// at what is simultaneously visible. A rented path is parented into the borrowing body's own container, so
/// per-body colour, fades and band alpha continue to apply exactly as before.
///
/// Paths are bucketed by the geometry that is baked in at construction — <see cref="Path.PathRadius"/> for
/// crisp paths, the profile for glow twins — so a body never receives one shaped for a different look.
/// </summary>
public class SliderPathPool
{
    private readonly Dictionary<float, Stack<SmoothPath>> freePaths = new();
    private readonly Dictionary<GlowPath.Profile, Stack<GlowPath>> freeGlows = new();

    /// <summary>Total instances ever constructed — the figure this pool exists to keep flat.</summary>
    public int ConstructedPaths { get; private set; }

    public int ConstructedGlows { get; private set; }

    public SmoothPath RentPath(float pathRadius)
    {
        if (freePaths.TryGetValue(pathRadius, out var free) && free.Count > 0)
            return free.Pop();

        ConstructedPaths++;

        return new SmoothPath
        {
            // Anchor the polar origin (vertex 0,0) at the playfield centre. Position (set per frame)
            // compensates for the auto-size bounding-box offset.
            Anchor = Anchor.Centre,
            PathRadius = pathRadius,
        };
    }

    public void Return(SmoothPath path)
    {
        path.Vertices = Array.Empty<Vector2>();
        path.Alpha = 1;
        path.Position = Vector2.Zero;

        if (!freePaths.TryGetValue(path.PathRadius, out var free))
            freePaths[path.PathRadius] = free = new Stack<SmoothPath>();

        free.Push(path);
    }

    public GlowPath RentGlow(GlowPath.Profile profile)
    {
        if (freeGlows.TryGetValue(profile, out var free) && free.Count > 0)
            return free.Pop();

        ConstructedGlows++;

        return new GlowPath(profile)
        {
            Anchor = Anchor.Centre,
        };
    }

    public void Return(GlowPath glow)
    {
        glow.Vertices = Array.Empty<Vector2>();
        glow.Alpha = 1;
        glow.Position = Vector2.Zero;

        if (!freeGlows.TryGetValue(glow.ProfileKey, out var free))
            freeGlows[glow.ProfileKey] = free = new Stack<GlowPath>();

        free.Push(glow);
    }
}
