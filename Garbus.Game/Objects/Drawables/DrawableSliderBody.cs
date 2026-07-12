// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableSliderBody.cs).

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Input;
using Garbus.Game.UI;
using osuTK;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// Draws a <see cref="GarbusPath"/> as a connected polyline in the playfield's polar coordinate system.
///
/// The path is made up of a start node (which fixes the initial direction and time) followed by a
/// number of child control points. Every node maps to a point <c>(θ, r)</c> where <c>θ</c> is the
/// node's angle and <c>r</c> is its distance from the centre. The radius is driven by the scrolling
/// algorithm, so as time advances every node "comes out" from the centre towards the surrounding arc,
/// giving the whole path the look of a durationed object emerging from the middle of the screen.
///
/// Rendering is delegated to <see cref="SmoothPath"/> (osu!framework's line renderer), which handles
/// thickness, rounded joints and anti-aliasing for us. This drawable's job is purely to produce the
/// list of cartesian vertices each frame — subdividing every link in polar space so constant-radius
/// links render as arcs, and clipping each link to the visible band <c>[0, ScrollLength]</c>.
/// </summary>
public partial class DrawableSliderBody : DrawableGarbusHitObject<SliderBody>, ISelfPosition
{
    /// <summary>
    /// Full width of the rendered line, in pixels. Half of this becomes the <see cref="Path.PathRadius"/>.
    /// </summary>
    public float Thickness { get; init; } = 15;

    /// <summary>
    /// Colour of the additive glow rendered behind the path. White gives a neutral halo that brightens
    /// whatever colour the path itself is tinted.
    /// </summary>
    public ColourInfo GlowColour { get; init; } = Colour4.White;

    /// <summary>
    /// Blur radius (sigma, in pixels) of the glow — larger values spread the halo further out.
    /// </summary>
    public float GlowBlurSigma { get; init; } = 30f;

    /// <summary>
    /// Intensity multiplier applied to the glow.
    /// </summary>
    public float GlowStrength { get; init; } = 30f;

    /// <summary>
    /// Number of straight sub-segments used to approximate the link between two consecutive nodes.
    /// Because interpolation happens in polar space, a link whose endpoints share a radius renders as
    /// an arc rather than a chord; more sub-segments make that arc smoother.
    /// </summary>
    // Straight sub-segments per link — shared with the editor polyline via SliderSweep.
    private const int segments_per_link = SliderSweep.SegmentsPerLink;

    // The stick-catcher arc sits this far outside the ring (mirrors StickIndicator.RadiusScale). The
    // body is allowed to draw out to here so it can be seen escaping past / being consumed at the edge.
    private const float catcher_radius_scale = 1.06f;

    // Fake an alpha gradient for an escaping (uncaught) tip over the ring -> catcher band: SmoothPath has
    // only a single uniform colour, so we stack this many translucent layers that all start at the ring
    // and reach progressively further out. Composited source-over, inner radii (covered by every layer)
    // end up near-opaque while the rim (one layer) stays faint — a fade whose only caps are the graduated
    // rounded tips, not lumpy mid-band caps from short disjoint slices.
    private const int escape_bands = 8;
    private const float escape_layer_alpha = 0.28f;

    // Leading-tip "consumed" marker shown while the catcher is eating the body.
    private const float tip_box_size = 46f;
    private const float tip_spin_deg_per_ms = 0.4f;
    private const float tip_pulse_speed = 0.012f; // pulse-sine radians per ms
    private const float tip_pulse_amplitude = 0.18f;

    [Resolved]
    private GarbusScrollingHitObjectContainer scrollingContainer { get; set; } = null!;

    [Resolved]
    private AnalogInputManager analogInput { get; set; } = null!;

    // Tinted/faded as a unit (fade-in, red-on-miss). SmoothPath forces its own draw colour to white,
    // so colour/alpha applied here is what tints the path via the framebuffer blit.
    private readonly Container<SmoothPath> pathContainer = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    // The portion of the body beyond the ring (ring -> catcher radius). Kept separate from pathContainer
    // so each pooled slice can carry its own alpha for the escape fade, and so it can be faded/tinted as
    // a unit on hit/miss independently of the glow-wrapped main body.
    private readonly Container<SmoothPath> escapeContainer = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    // Pool of SmoothPaths for the main body [0, ring]. Normally the visible portion is a single
    // contiguous run, but if the run breaks (e.g. non-monotonic node times leave a gap) we start a fresh
    // path so the two spans are not joined by a stray line. Grown lazily, mirroring the old box pool.
    private readonly List<SmoothPath> bodyPaths = new();

    // Pool of SmoothPaths for the escape band [ring, catcher]. When escaping, one slice per sub-band.
    private readonly List<SmoothPath> escapePaths = new();

    // Pulsating, spinning marker riding the leading tip while it is being consumed by the catcher.
    private readonly Box tipBox = new()
    {
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        Size = new Vector2(tip_box_size),
        Alpha = 0,
    };

    // Nested child hit objects live here so they receive a clock and are updated/judged like any
    // other DrawableHitObject. They draw nothing (the path visuals come entirely from pathContainer);
    // without a real parent in the tree, OnKilled would dereference a null clock.
    private readonly Container<DrawableHitObject> nestedContainer = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    // Per-node data, rebuilt whenever a new hit object is applied.
    private float[] nodeRadians = Array.Empty<float>();
    private double[] nodeTimes = Array.Empty<double>();
    private float[] nodeRadii = Array.Empty<float>();

    // Angular sweep rate (dθ/dtime, radians per ms) at each node — the Catmull-Rom tangents used to
    // smooth the angle interpolation so the sweep velocity is continuous through nodes.
    private float[] nodeThetaSlopes = Array.Empty<float>();

    // Per-link interpolation, taken from the control point at each link's end node (a control point
    // governs the segment leading into it). Off / Easing.None by default, so a link keeps exact linear
    // geometry unless its control point opts in. Indexed by link i = node[i] -> node[i + 1].
    private bool[] linkSmooth = Array.Empty<bool>();
    private Easing[] linkEasing = Array.Empty<Easing>();

    // Reused each frame to accumulate the vertices of the contiguous run currently being built.
    private readonly List<Vector2> scratchVertices = new();

    public DrawableSliderBody(SliderBody hitObject)
        : base(hitObject)
    {
        RelativeSizeAxes = Axes.Both;

        var sideColour = HitObject.Side == HorizontalDirection.Left ? Constants.LeftColour : Constants.RightColour;
        pathContainer.Colour = sideColour;
        escapeContainer.Colour = sideColour;
        tipBox.Colour = sideColour;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        // Wrap the whole path pool in an additive glow. GlowEffect buffers pathContainer's rendered
        // alpha and blurs it, so the halo hugs the actual line shape (not a bounding box) and a single
        // buffer covers every pooled path. The crisp paths are drawn in front of the halo. Fading or
        // recolouring pathContainer flows through to the glow, since it is regenerated from that content.
        AddInternal(new GlowEffect
        {
            Colour = GlowColour,
            BlurSigma = new Vector2(GlowBlurSigma),
            Strength = GlowStrength,
            // PadExtent would inset (and misalign) the relatively-sized pathContainer, so leave it off.
            PadExtent = false,
        }.ApplyTo(pathContainer));

        // Escape band and tip marker draw in front of the main body's glow.
        AddInternal(escapeContainer);
        AddInternal(tipBox);

        AddInternal(nestedContainer);
    }

    protected override void OnApply()
    {
        base.OnApply();
        rebuildNodes();
    }

    protected override void PrepareForUse()
    {
        base.PrepareForUse();
        pathContainer.FadeInFromZero(100, Easing.In);
        escapeContainer.FadeInFromZero(100, Easing.In);
    }

    protected override void Update()
    {
        base.Update();
        updatePath();
    }

    /// <summary>
    /// Precomputes the constant angle/time of every node from the applied hit object.
    /// </summary>
    private void rebuildNodes()
    {
        var start = HitObject;
        var controlPoints = start.Path.ControlPoints;
        int linkCount = controlPoints.Count;
        int count = 1 + linkCount;

        nodeRadians = new float[count];
        nodeTimes = new double[count];
        nodeRadii = new float[count];
        nodeThetaSlopes = new float[count];
        linkSmooth = new bool[linkCount];
        linkEasing = new Easing[linkCount];

        // Node 0 is the start node itself.
        nodeRadians[0] = toRadians(start.AngleDeg);
        nodeTimes[0] = start.StartTime;

        for (int i = 0; i < linkCount; i++)
        {
            var controlPoint = controlPoints[i];

            nodeRadians[i + 1] = toRadians(start.AngleDeg + controlPoint.RotationOffset);
            nodeTimes[i + 1] = start.StartTime + controlPoint.TimeOffset;

            // A control point governs the segment leading into it: link[i] ends at node[i + 1] = CP[i].
            linkSmooth[i] = controlPoint.Smooth;
            linkEasing[i] = controlPoint.SweepEasing;
        }

        // Catmull-Rom tangents for the smoothed-angle Hermite interpolation (shared with the editor).
        nodeThetaSlopes = SliderSweep.ComputeSlopes(nodeRadians, nodeTimes);
    }

    /// <summary>
    /// Recomputes the line geometry for the current frame. Each node's radius is resolved through the
    /// scrolling algorithm, then the body is rendered in two zones: the main body inside the ring, and
    /// the band between the ring and the catcher radius, which either fades out (escaping) or draws solid
    /// and spawns the tip marker (being consumed), depending on whether the leading edge is caught.
    /// </summary>
    private void updatePath()
    {
        int bodyIndex = 0;
        int escapeIndex = 0;

        if (nodeTimes.Length >= 2)
        {
            float ringRadius = scrollingContainer.ScrollLength;
            float catcherRadius = ringRadius * catcher_radius_scale;

            for (int i = 0; i < nodeTimes.Length; i++)
                // Raw, unclamped distance from the centre. Negative means the node has not yet emerged
                // from the centre; greater than the ring radius means it has already been consumed by
                // the outer edge. Both ends are handled by clipping each link to the visible band
                // below — NOT by clamping the node. Clamping a not-yet-emerged node to the centre would
                // draw the whole link from the emerged node straight to the centre at once, instead of
                // letting the curve creep outward from the middle a little at a time.
                nodeRadii[i] = scrollingContainer.DistanceFromCentreAtTime(nodeTimes[i]);

            // Main body: the emergence front (radius 0) out to the ring, at full alpha.
            bodyIndex = renderBand(0f, ringRadius, 1f, bodyPaths, pathContainer, bodyIndex);

            // Beyond the ring: whether the leading edge is being caught right now decides the look.
            bool caught = isLeadingEdgeCaught();
            bool hasTip = tryGetLeadingTip(ringRadius, catcherRadius, out Vector2 tip);

            if (caught && hasTip)
            {
                // Consumed by the catcher: draw solid out to the catcher radius, and ride the tip marker.
                escapeIndex = renderBand(ringRadius, catcherRadius, 1f, escapePaths, escapeContainer, escapeIndex);
            }
            else
            {
                // Escaping (or idle): fade out over the ring -> catcher band. Each layer starts at the
                // ring and reaches a bit further; overlapping them source-over yields a radial fade
                // (opaque at the ring, faint at the rim) without lumpy mid-band caps.
                float h = (catcherRadius - ringRadius) / escape_bands;

                for (int b = 0; b < escape_bands; b++)
                {
                    float outer = ringRadius + (b + 1) * h;
                    escapeIndex = renderBand(ringRadius, outer, escape_layer_alpha, escapePaths, escapeContainer, escapeIndex);
                }
            }

            updateTipBox(caught && hasTip, tip);
        }
        else
        {
            updateTipBox(false, Vector2.Zero);
        }

        // Hide pooled paths used by a longer path on a previous frame / hit object.
        for (int i = bodyIndex; i < bodyPaths.Count; i++)
            bodyPaths[i].Vertices = Array.Empty<Vector2>();
        for (int i = escapeIndex; i < escapePaths.Count; i++)
            escapePaths[i].Vertices = Array.Empty<Vector2>();
    }

    /// <summary>
    /// Walks every link, clipping it to the radial band [<paramref name="innerRadius"/>,
    /// <paramref name="outerRadius"/>] and emitting the visible runs into <paramref name="pool"/> /
    /// <paramref name="container"/> at <paramref name="alpha"/>, starting at <paramref name="poolIndex"/>.
    /// Returns the next free pool index.
    /// </summary>
    private int renderBand(float innerRadius, float outerRadius, float alpha, List<SmoothPath> pool, Container<SmoothPath> container, int poolIndex)
    {
        scratchVertices.Clear();

        for (int i = 0; i < nodeTimes.Length - 1; i++)
        {
            float rA = nodeRadii[i];
            float rB = nodeRadii[i + 1];

            // Draw only the part of the link whose radius lies within the band. Radius varies linearly
            // along the link, so this is a plain 1-D clip of the parameter range.
            if (!clipToBand(rA, rB, innerRadius, outerRadius, out float tLo, out float tHi))
            {
                // This link is entirely outside the band; the run cannot continue past it.
                poolIndex = flushRun(pool, container, alpha, poolIndex);
                continue;
            }

            Vector2 startPoint = pointAt(i, rA, rB, tLo);

            // Continue the current run only if this link's visible portion begins exactly where the last
            // one ended (a shared, fully-visible node). Otherwise there is a gap — flush and start a
            // fresh path so the two spans are not bridged by a stray line.
            if (scratchVertices.Count > 0 && !approxEqual(scratchVertices[^1], startPoint))
                poolIndex = flushRun(pool, container, alpha, poolIndex);

            if (scratchVertices.Count == 0)
                scratchVertices.Add(startPoint);

            for (int k = 1; k <= segments_per_link; k++)
            {
                float t = tLo + (tHi - tLo) * ((float)k / segments_per_link);
                scratchVertices.Add(pointAt(i, rA, rB, t));
            }

            // Clipped before reaching its end node: the curve leaves the band here, so the run ends.
            if (tHi < 1f)
                poolIndex = flushRun(pool, container, alpha, poolIndex);
        }

        return flushRun(pool, container, alpha, poolIndex);
    }

    /// <summary>
    /// Whether the catcher is currently pointing at the body's leading edge. The point at the ring has
    /// node-time == now (the time→radius mapping puts now at <see cref="GarbusScrollingHitObjectContainer.ScrollLength"/>),
    /// so its angle is <see cref="AngleDegAt"/> at the current time.
    /// </summary>
    private bool isLeadingEdgeCaught()
    {
        int angleDeg = (int)MathF.Round(AngleDegAt(Time.Current));
        return analogInput.SliderCatchers[HitObject.Side].IsCatchingAt(angleDeg);
    }

    /// <summary>
    /// Finds the outermost visible point of the body within the [ring, catcher] band — where the catcher
    /// meets the body. Radius is monotonic along each link, so the clipped sub-range endpoints are the
    /// per-link radial extremes; the global maximum among them is the tip. Returns false if nothing in
    /// the body currently reaches past the ring.
    /// </summary>
    private bool tryGetLeadingTip(float ringRadius, float catcherRadius, out Vector2 tip)
    {
        tip = Vector2.Zero;
        float bestRadiusSq = -1f;

        for (int i = 0; i < nodeTimes.Length - 1; i++)
        {
            float rA = nodeRadii[i];
            float rB = nodeRadii[i + 1];

            if (!clipToBand(rA, rB, ringRadius, catcherRadius, out float tLo, out float tHi))
                continue;

            Vector2 lo = pointAt(i, rA, rB, tLo);
            Vector2 hi = pointAt(i, rA, rB, tHi);

            if (lo.LengthSquared > bestRadiusSq)
            {
                bestRadiusSq = lo.LengthSquared;
                tip = lo;
            }

            if (hi.LengthSquared > bestRadiusSq)
            {
                bestRadiusSq = hi.LengthSquared;
                tip = hi;
            }
        }

        return bestRadiusSq >= 0f;
    }

    /// <summary>
    /// Positions and animates (or hides) the pulsating, spinning tip marker.
    /// </summary>
    private void updateTipBox(bool show, Vector2 tip)
    {
        if (!show)
        {
            tipBox.Alpha = 0;
            return;
        }

        tipBox.Alpha = 1;
        tipBox.Position = tip;

        // Drive spin/pulse from absolute time so the animation is stable across frames.
        double now = Time.Current;
        tipBox.Rotation = (float)(now * tip_spin_deg_per_ms % 360.0);
        tipBox.Scale = new Vector2(1f + tip_pulse_amplitude * MathF.Sin((float)(now * tip_pulse_speed)));
    }

    /// <summary>
    /// Commits the vertices accumulated in <see cref="scratchVertices"/> (if it forms a drawable run of
    /// at least two points) to the next pooled path at <paramref name="alpha"/>, then clears the scratch
    /// buffer. Returns the updated index into <paramref name="pool"/>.
    /// </summary>
    private int flushRun(List<SmoothPath> pool, Container<SmoothPath> container, float alpha, int poolIndex)
    {
        if (scratchVertices.Count >= 2)
        {
            var path = getPath(pool, container, poolIndex++);

            // Vertices setter copies the list, so reusing the scratch buffer afterwards is safe.
            path.Vertices = scratchVertices;
            path.Alpha = alpha;

            // Path auto-sizes to its vertex bounds and offsets content by vertexBounds.TopLeft; undo that
            // offset so a vertex at the polar origin (0,0) lands on the playfield centre (our anchor).
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);
        }

        scratchVertices.Clear();
        return poolIndex;
    }

    /// <summary>
    /// Lazily grows the given path pool/container, returning the path at <paramref name="index"/>.
    /// </summary>
    private SmoothPath getPath(List<SmoothPath> pool, Container<SmoothPath> container, int index)
    {
        while (pool.Count <= index)
        {
            var path = new SmoothPath
            {
                // Anchor the polar origin (vertex 0,0) at the playfield centre. Position (set per frame)
                // compensates for the auto-size bounding-box offset.
                Anchor = Anchor.Centre,
                PathRadius = Thickness / 2,
            };

            pool.Add(path);
            container.Add(path);
        }

        return pool[index];
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
//        ApplyResult(HitResult.IgnoreHit);
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        const double duration = 1000;

        switch (state)
        {
            case ArmedState.Hit:
                escapeContainer.FadeOut(350, Easing.OutQuint);
                tipBox.FadeOut(350, Easing.OutQuint);
                pathContainer.FadeOut(350, Easing.OutQuint).OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                pathContainer.FadeColour(Colour4.Red, duration);
                escapeContainer.FadeColour(Colour4.Red, duration);
                escapeContainer.FadeOut(duration, Easing.InQuint);
                tipBox.FadeOut(duration, Easing.InQuint);
                pathContainer.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
        }
    }

    private static Vector2 polarToCartesian(float radians, float radius)
        => new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);

    // Point at parameter t along a link, in polar-origin-centred space (vertex (0,0) is the centre).
    // Angle is smoothed/eased per the link's control point; radius stays linear so the time→radius
    // mapping the clip relies on is exact.
    private Vector2 pointAt(int link, float rA, float rB, float t)
        => polarToCartesian(thetaAt(link, t), lerp(rA, rB, t));

    /// <summary>
    /// The angle of the slider body at the given <paramref name="time"/>, in degrees, matching the
    /// swept geometry the body is rendered with (same per-link easing / smoothing). This is the angle a
    /// <see cref="Input.AnalogInputManager.SliderCatcher"/> must be pointing at to be catching the body there.
    ///
    /// Because node radius is linear in time, the link parameter <c>t</c> is just the fraction of the
    /// link's time span elapsed. Times before the start node or after the last node clamp to the
    /// respective end node's angle. The result may fall outside [0, 360) — callers that compare against
    /// a catcher angle should wrap (see <see cref="Input.AnalogInputManager.SliderCatcher.IsCatchingAt"/>).
    /// </summary>
    public float AngleDegAt(double time)
    {
        // No links: the body is a single node, so its angle is constant.
        if (nodeTimes.Length < 2)
            return HitObject.AngleDeg;

        if (time <= nodeTimes[0])
            return toDegrees(nodeRadians[0]);
        if (time >= nodeTimes[^1])
            return toDegrees(nodeRadians[^1]);

        // Find the link this time falls in: link i spans [nodeTimes[i], nodeTimes[i + 1]].
        int link = 0;
        while (link < nodeTimes.Length - 2 && time > nodeTimes[link + 1])
            link++;

        double span = nodeTimes[link + 1] - nodeTimes[link];
        float t = span > 0 ? (float)((time - nodeTimes[link]) / span) : 0f;

        return toDegrees(thetaAt(link, t));
    }

    /// <summary>
    /// Evaluates the smoothed angle at parameter <paramref name="t"/> (0..1) along the given
    /// <paramref name="link"/>, using cubic Hermite interpolation with the precomputed Catmull-Rom
    /// tangents at the two surrounding nodes.
    /// </summary>
    private float thetaAt(int link, float t)
        => SliderSweep.ValueAt(nodeRadians, nodeThetaSlopes, nodeTimes, linkEasing[link], linkSmooth[link], link, t);

    private static float toRadians(float degrees) => degrees * MathF.PI / 180f;

    private static float toDegrees(float radians) => radians * 180f / MathF.PI;

    private static float lerp(float a, float b, float t) => a + (b - a) * t;

    private static bool approxEqual(Vector2 a, Vector2 b) => (a - b).LengthSquared < 0.0001f;

    /// <summary>
    /// Clips a link's parameter range [0, 1] to the sub-range whose linearly-interpolated radius lies
    /// within [<paramref name="innerRadius"/>, <paramref name="outerRadius"/>], using Liang–Barsky. The
    /// lower crossing is where the curve enters the band from within; the upper crossing is where it
    /// leaves. Returns false if no part of the link falls in the band.
    /// </summary>
    private static bool clipToBand(float rA, float rB, float innerRadius, float outerRadius, out float tLo, out float tHi)
    {
        tLo = 0f;
        tHi = 1f;

        // radius(t) = rA + (rB - rA) * t; keep innerRadius <= radius(t) <= outerRadius.
        float d = rB - rA;

        return clipEdge(-d, rA - innerRadius, ref tLo, ref tHi) // radius(t) >= innerRadius
               && clipEdge(d, outerRadius - rA, ref tLo, ref tHi); // radius(t) <= outerRadius
    }

    private static bool clipEdge(float p, float q, ref float tLo, ref float tHi)
    {
        if (p == 0)
            return q >= 0; // link runs parallel to this boundary: visible only if already inside it

        float r = q / p;

        if (p < 0)
        {
            if (r > tHi) return false;

            if (r > tLo) tLo = r;
        }
        else
        {
            if (r < tLo) return false;

            if (r < tHi) tHi = r;
        }

        return true;
    }

    protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
    {
        return hitObject switch
        {
            SliderChild child => new DrawableSliderChild(child),
            SliderHead head => new DrawableSliderHead(head),
            _ => throw new InvalidOperationException($"cannot create nested hit object for type {hitObject.GetType().Name}")
        };
    }

    protected override void AddNestedHitObject(DrawableHitObject hitObject)
    {
        if (hitObject is not (DrawableSliderChild or DrawableSliderHead))
            throw new InvalidOperationException($"cannot add child of type {hitObject.GetType()}");

        nestedContainer.Add(hitObject);
    }

    protected override void ClearNestedHitObjects()
    {
        nestedContainer.Clear(false);
    }
}
