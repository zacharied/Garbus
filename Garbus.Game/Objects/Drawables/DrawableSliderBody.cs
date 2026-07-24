// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableSliderBody.cs).

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Scoring;
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
    // The body carries only an unscored IgnoreHit lifetime sentinel. Slider audio belongs to a caught
    // head; neither that sentinel nor duration children should emit additional hitsounds.
    public override void PlaySamples()
    {
    }

    /// <summary>
    /// Full width of the rendered line, in pixels. Half of this becomes the <see cref="Path.PathRadius"/>.
    /// </summary>
    public float Thickness { get; init; } = 16f;

    /// <summary>
    /// Colour of the additive glow rendered around the path. White gives a neutral halo that brightens
    /// whatever colour the path itself is tinted.
    /// </summary>
    public ColourInfo GlowColour { get; init; } = Colour4.White;

    /// <summary>
    /// Gaussian sigma (in pixels) of the glow's falloff profile — larger values spread the halo
    /// further out.
    /// </summary>
    public float GlowBlurSigma { get; init; } = 1.64f;

    /// <summary>
    /// Intensity multiplier applied to the glow profile before it clips at full alpha. Values
    /// well above 1 grow the saturated "hot" band around the line; near 1 the halo never
    /// saturates and the whole visible glow is the smooth falloff.
    /// </summary>
    public float GlowStrength { get; init; } = 10.6f;

    /// <summary>
    /// Falloff-shape exponent applied to the glow's skirt. 1 is the pure Gaussian profile; below
    /// 1 lifts the tail (gentler, longer fade); above 1 tightens it. Never affects the saturated
    /// core, only the visible falloff.
    /// </summary>
    public float GlowFalloff { get; init; } = 0.45f;

    /// <summary>
    /// Whether to draw the crisp line on top of the glow. Off by default: the glow's saturated
    /// core already reads as a solid neon tube, so the body is rendered by the glow alone. The
    /// line geometry is still computed either way (the glow twins share its vertices).
    /// </summary>
    public bool ShowLine { get; init; } = true;

    /// <summary>
    /// How far past the ring (as a multiple of the ring radius) an escaping, uncaught body fades
    /// out. The caught body is still consumed hard at the catcher radius (1.06); the escaping fade
    /// gets a wider runway so the tube reads as flying off and dissolving rather than popping.
    /// </summary>
    public float EscapeFadeScale { get; init; } = 1.3f;

    /// <summary>
    /// Alpha the whole body dims to while it is past its start time but not being caught.
    /// </summary>
    public float UncaughtDimAlpha { get; init; } = 0.4f;

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

    // Tinted/faded as a unit (fade-in, red-on-miss): holds the crisp paths and their glow twins.
    // SmoothPath forces its own draw colour to white, so colour applied here is what tints the
    // paths via their framebuffer blits.
    private readonly Container bodyVisual = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    private readonly Container<SmoothPath> pathContainer = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    // The additive glow twins draw in front of the crisp paths (additive never occludes), matching
    // the in-front placement of the GlowEffect this replaces, which brightened the line core.
    private readonly Container<GlowPath> glowContainer = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    // The portion of the body beyond the ring. Kept separate from bodyVisual so each pooled slice can
    // carry its own alpha for the escape fade; tinted/faded as a unit on hit independently of the body.
    // Mirrors bodyVisual's structure: crisp slices (gated by ShowLine) plus glow twins, so the tube
    // keeps its look as it crosses the ring instead of degrading to a thin crisp line.
    private readonly Container escapeVisual = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    private readonly Container<SmoothPath> escapeContainer = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    private readonly Container<GlowPath> escapeGlowContainer = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    // Pool of SmoothPaths for the main body [0, ring]. Normally the visible portion is a single
    // contiguous run, but if the run breaks (e.g. non-monotonic node times leave a gap) we start a fresh
    // path so the two spans are not joined by a stray line. Grown lazily, mirroring the old box pool.
    private readonly List<SmoothPath> bodyPaths = new();

    // Pool of SmoothPaths for the escape band [ring, catcher]. When escaping, one slice per sub-band.
    private readonly List<SmoothPath> escapePaths = new();

    // Pools of additive glow twins, one per crisp path in the corresponding band.
    private readonly List<GlowPath> glowPaths = new();
    private readonly List<GlowPath> escapeGlowPaths = new();

    internal IReadOnlyList<SmoothPath> BodyPaths => bodyPaths;

    internal IReadOnlyList<GlowPath> GlowPaths => glowPaths;

    internal IReadOnlyList<SmoothPath> EscapePaths => escapePaths;

    internal IReadOnlyList<GlowPath> EscapeGlowPaths => escapeGlowPaths;

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

    // A head-only slider (no control points) has no line to draw; render its single node as a filled
    // circle of the body's own line radius so it stays visible. Wrapped in a fade-managed container
    // (like pathContainer) so it fades/tints as a unit, while the circle carries per-frame band alpha.
    private readonly Container headContainer = new()
    {
        RelativeSizeAxes = Axes.Both,
    };

    private readonly Circle headCircle = new()
    {
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        Alpha = 0,
    };

    // Glow disc for head-only sliders: a dot-length GlowPath renders as a filled circle with the
    // tube's full cross-section profile, so a lone head is as "full" as a slice of real tube.
    private GlowPath headGlow = null!;

    internal GlowPath HeadGlow => headGlow;

    // Reused two-element buffer for the disc's degenerate dot-length path.
    private readonly Vector2[] headVertices = new Vector2[2];

    private readonly SliderContactSpikes contactSpikes;

    internal SliderContactSpikes ContactSpikes => contactSpikes;

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
        contactSpikes = new SliderContactSpikes(sideColour);
        bodyVisual.Colour = sideColour;
        escapeVisual.Colour = sideColour;
        tipBox.Colour = sideColour;

        headContainer.Colour = sideColour;
        headContainer.Add(headCircle);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        // The glow is geometric: each body path gets an additive GlowPath twin sharing its vertices,
        // whose cross-section texture bakes the exact falloff a Gaussian blur of the crisp line would
        // produce (see GlowPath). This replaces the previous GlowEffect, whose playfield-sized
        // framebuffer was fully re-rendered and blurred (two ~129-tap passes at sigma 30) every frame
        // per slider — the main gameplay render bottleneck. Fading or recolouring bodyVisual flows
        // through to crisp paths and glow twins alike.
        glowContainer.Colour = GlowColour;
        pathContainer.Alpha = ShowLine ? 1 : 0;
        bodyVisual.AddRange(new Drawable[] { pathContainer, glowContainer });
        AddInternal(bodyVisual);

        // Escape band and tip marker draw in front of the main body's glow.
        escapeContainer.Alpha = ShowLine ? 1 : 0;
        escapeVisual.AddRange(new Drawable[] { escapeContainer, escapeGlowContainer });
        AddInternal(escapeVisual);
        AddInternal(contactSpikes);
        AddInternal(tipBox);

        headCircle.Size = new Vector2(Thickness);
        headContainer.Add(headGlow = new GlowPath(Thickness / 2, GlowBlurSigma, GlowStrength, GlowFalloff)
        {
            Anchor = Anchor.Centre,
            Alpha = 0,
        });
        AddInternal(headContainer);

        AddInternal(nestedContainer);
    }

    protected override void OnApply()
    {
        base.OnApply();
        rebuildNodes();

        // Reset the eased uncaught-dim state for (re)use.
        dimTarget = 1;
        Alpha = 1;
    }

    protected override void PrepareForUse()
    {
        base.PrepareForUse();
        bodyVisual.FadeInFromZero(100, Easing.In);
        escapeVisual.FadeInFromZero(100, Easing.In);
        headContainer.FadeInFromZero(100, Easing.In);
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
        updateHeadCircle();

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

            // Main body: the emergence front (radius 0) out to the ring, at full alpha, with glow.
            bodyIndex = renderBand(0f, ringRadius, 1f, bodyPaths, pathContainer, bodyIndex, glowPaths, glowContainer);

            bool hasRingContact = State.Value == ArmedState.Idle &&
                                  Time.Current >= nodeTimes[0] && Time.Current <= nodeTimes[^1];
            contactSpikes.SetContact(toRadians(AngleDegAt(Time.Current)), ringRadius, hasRingContact);

            // Beyond the ring: whether the leading edge is being caught right now decides the look.
            bool caught = isLeadingEdgeCaught();
            bool hasTip = tryGetLeadingTip(ringRadius, catcherRadius, out Vector2 tip);

            if (caught && hasTip)
            {
                // Consumed by the catcher: draw solid out to the catcher radius, and ride the tip marker.
                escapeIndex = renderBand(ringRadius, catcherRadius, 1f, escapePaths, escapeContainer, escapeIndex, escapeGlowPaths, escapeGlowContainer);
            }
            else
            {
                // Escaping (or idle): dissolve over the wider ring -> EscapeFadeScale runway. Each layer
                // starts at the ring and reaches a bit further, so coverage (and thus brightness) ramps
                // down with distance without lumpy mid-band caps. The crisp slices composite source-over
                // (escape_layer_alpha each); the glow twins composite additively, so their per-layer
                // alpha is 1/N — the N-deep inner edge then sums back to the tube's full brightness and
                // stays continuous across the ring.
                float fadeOuter = ringRadius * EscapeFadeScale;
                float h = (fadeOuter - ringRadius) / escape_bands;

                for (int b = 0; b < escape_bands; b++)
                {
                    float outer = ringRadius + (b + 1) * h;
                    escapeIndex = renderBand(ringRadius, outer, escape_layer_alpha, escapePaths, escapeContainer, escapeIndex,
                        escapeGlowPaths, escapeGlowContainer, 1f / escape_bands);
                }
            }

            updateTipBox(caught && hasTip, tip);

            updateBodyVisual(caught);
        }
        else
        {
            contactSpikes.SetContact(0, 0, false);
            updateTipBox(false, Vector2.Zero);
        }

        // Hide pooled paths used by a longer path on a previous frame / hit object.
        for (int i = bodyIndex; i < bodyPaths.Count; i++)
            bodyPaths[i].Vertices = Array.Empty<Vector2>();
        for (int i = bodyIndex; i < glowPaths.Count; i++)
            glowPaths[i].Vertices = Array.Empty<Vector2>();
        for (int i = escapeIndex; i < escapePaths.Count; i++)
            escapePaths[i].Vertices = Array.Empty<Vector2>();
        for (int i = escapeIndex; i < escapeGlowPaths.Count; i++)
            escapeGlowPaths[i].Vertices = Array.Empty<Vector2>();
    }

    private const double dim_fade_duration = 150;

    // Target of the eased uncaught dim; tracked so the fade fires only when the state flips rather
    // than restarting every frame.
    private float dimTarget = 1;

    private void updateBodyVisual(bool caught)
    {
        float target = Time.Current < HitObject.StartTime || caught ? 1f : UncaughtDimAlpha;

        if (target != dimTarget)
        {
            dimTarget = target;
            this.FadeTo(target, dim_fade_duration, Easing.OutQuint);
        }
    }

    /// <summary>
    /// A head-only slider (no control points) has no path to render; show its single node as a glow
    /// disc of the tube's full cross-section (plus the crisp circle when <see cref="ShowLine"/> is on),
    /// travelling centre→ring like a body head would, then dissolving over the same
    /// <see cref="EscapeFadeScale"/> runway as an escaping tube. Hidden for any slider that has a real
    /// path (its line already draws the head).
    /// </summary>
    private void updateHeadCircle()
    {
        if (nodeTimes.Length >= 2)
        {
            headCircle.Alpha = 0;
            headGlow.Alpha = 0;
            return;
        }

        float ringRadius = scrollingContainer.ScrollLength;
        float fadeOuter = ringRadius * EscapeFadeScale;
        float r = scrollingContainer.DistanceFromCentreAtTime(nodeTimes[0]);

        float alpha = r < 0 || r > fadeOuter ? 0
            : r <= ringRadius ? 1
            : 1 - (r - ringRadius) / (fadeOuter - ringRadius);

        headCircle.Alpha = ShowLine ? alpha : 0;
        headGlow.Alpha = alpha;

        if (alpha <= 0)
        {
            headGlow.Vertices = Array.Empty<Vector2>();
            return;
        }

        Vector2 position = polarToCartesian(nodeRadians[0], r);
        headCircle.Position = position;

        // A dot-length path renders as a filled disc with rounded caps — the tube's cross-section.
        headVertices[0] = position;
        headVertices[1] = position + new Vector2(0.1f, 0);
        headGlow.Vertices = headVertices;
        headGlow.Position = -headGlow.PositionInBoundingBox(Vector2.Zero);
    }

    /// <summary>
    /// Walks every link, clipping it to the radial band [<paramref name="innerRadius"/>,
    /// <paramref name="outerRadius"/>] and emitting the visible runs into <paramref name="pool"/> /
    /// <paramref name="container"/> at <paramref name="alpha"/>, starting at <paramref name="poolIndex"/>.
    /// Returns the next free pool index.
    /// </summary>
    private int renderBand(float innerRadius, float outerRadius, float alpha, List<SmoothPath> pool, Container<SmoothPath> container, int poolIndex,
                           List<GlowPath>? glowPool = null, Container<GlowPath>? glowTarget = null, float? glowAlpha = null)
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
                poolIndex = flushRun(pool, container, alpha, poolIndex, glowPool, glowTarget, glowAlpha);
                continue;
            }

            Vector2 startPoint = pointAt(i, rA, rB, tLo);

            // Continue the current run only if this link's visible portion begins exactly where the last
            // one ended (a shared, fully-visible node). Otherwise there is a gap — flush and start a
            // fresh path so the two spans are not bridged by a stray line.
            if (scratchVertices.Count > 0 && !approxEqual(scratchVertices[^1], startPoint))
                poolIndex = flushRun(pool, container, alpha, poolIndex, glowPool, glowTarget, glowAlpha);

            if (scratchVertices.Count == 0)
                scratchVertices.Add(startPoint);

            for (int k = 1; k <= segments_per_link; k++)
            {
                float t = tLo + (tHi - tLo) * ((float)k / segments_per_link);
                scratchVertices.Add(pointAt(i, rA, rB, t));
            }

            // Clipped before reaching its end node: the curve leaves the band here, so the run ends.
            if (tHi < 1f)
                poolIndex = flushRun(pool, container, alpha, poolIndex, glowPool, glowTarget, glowAlpha);
        }

        return flushRun(pool, container, alpha, poolIndex, glowPool, glowTarget, glowAlpha);
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
    private int flushRun(List<SmoothPath> pool, Container<SmoothPath> container, float alpha, int poolIndex,
                         List<GlowPath>? glowPool, Container<GlowPath>? glowTarget, float? glowAlpha)
    {
        if (scratchVertices.Count >= 2)
        {
            var path = getPath(pool, container, poolIndex);

            // Vertices setter copies the list, so reusing the scratch buffer afterwards is safe.
            path.Vertices = scratchVertices;
            path.Alpha = alpha;

            // Path auto-sizes to its vertex bounds and offsets content by vertexBounds.TopLeft; undo that
            // offset so a vertex at the polar origin (0,0) lands on the playfield centre (our anchor).
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);

            if (glowPool != null && glowTarget != null)
            {
                // The glow twin shares the run's vertices but carries its own (wider) bounding box,
                // so it needs its own origin compensation. Its alpha may differ from the crisp run's:
                // glow composites additively, so stacked layers need a smaller per-layer alpha.
                var glow = getGlowPath(glowPool, glowTarget, poolIndex);
                glow.Vertices = scratchVertices;
                glow.Alpha = glowAlpha ?? alpha;
                glow.Position = -glow.PositionInBoundingBox(Vector2.Zero);
            }

            poolIndex++;
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

    /// <summary>
    /// Lazily grows the given glow-twin pool, returning the glow path at <paramref name="index"/>.
    /// </summary>
    private GlowPath getGlowPath(List<GlowPath> pool, Container<GlowPath> container, int index)
    {
        while (pool.Count <= index)
        {
            var glow = new GlowPath(Thickness / 2, GlowBlurSigma, GlowStrength, GlowFalloff)
            {
                Anchor = Anchor.Centre,
            };

            pool.Add(glow);
            container.Add(glow);
        }

        return pool[index];
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        // Wait until the path has fully played out (timeOffset >= 0, i.e. Time.Current >= EndTime) AND
        // every nested object has judged. The children read this body's per-frame geometry (AngleDegAt)
        // to detect catches, so the body must stay alive until they are done; only then does it take its
        // own unscored IgnoreHit, leave Idle, and expire via UpdateHitStateTransforms.
        if (timeOffset < 0)
            return;

        foreach (var nested in NestedHitObjects)
        {
            if (!nested.AllJudged)
                return;
        }

        ApplyResult(HitResult.IgnoreHit);
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        // The body's result is always the unscored IgnoreHit sentinel (see CheckForResult), and
        // IgnoreHit counts as hit — so ArmedState.Miss is unreachable here. Failure presentation is
        // carried by the per-child judgement feedback, the uncaught dim, and the escaping dissolve.
        switch (state)
        {
            case ArmedState.Hit:
                escapeVisual.FadeOut(350, Easing.OutQuint);
                tipBox.FadeOut(350, Easing.OutQuint);
                headContainer.FadeOut(350, Easing.OutQuint);
                bodyVisual.FadeOut(350, Easing.OutQuint).OnComplete(_ => Expire());
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
