// Draws one thin, semi-transparent yellow polygon per same-start-time cardinal chord, inscribed at the
// chord's shared (co-radial) distance from centre. Lives in Ring below the hit objects. It is purely a
// visual aid with no gameplay implications.
//
// Its whole presentation is a pure function of the clock: geometry and alpha are recomputed and assigned
// every frame from the current time, so it behaves identically whether the clock is played forward
// (gameplay) or scrubbed / paused / rewound (the editor timeline and mini preview) — no stuck lines, and no
// notion of "preview" vs "gameplay" anywhere. It never reads judgement or ArmedState; it only asks which
// chords currently have a live member and where they sit on the way to the ring. Geometry comes from
// ChordHighlighter + ProgressAtTime (never live note positions), so the full polygon holds its shape while
// scrolling in, even if one member despawns early.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Objects;
using Garbus.Game.Utils;
using osuTK;

namespace Garbus.Game.UI;

public partial class ChordConnectorOverlay : CompositeDrawable
{
    [Resolved]
    private ChordHighlighter chords { get; set; } = null!;

    [Resolved]
    private Ring ring { get; set; } = null!;

    // How long the polygon takes to fade out once its chord reaches the ring.
    private const double connector_fade_out = 200;

    // One reusable path per chord, keyed by the chord's shared start time. Kept (hidden) rather than removed
    // when the chord is off-screen, to avoid per-frame allocation churn while scrubbing back and forth.
    private readonly Dictionary<double, SmoothPath> pathsByStartTime = new Dictionary<double, SmoothPath>();

    public ChordConnectorOverlay()
    {
        RelativeSizeAxes = Axes.Both;
    }

    // Local half-thickness for the connector line. At 1:1 or larger it is exactly ConnectorPathRadius, so
    // gameplay is unchanged; when the overlay is rendered below 1:1 (the editor mini preview scales the whole
    // playfield down) it grows inversely with the draw scale so the ON-SCREEN thickness stays at the design
    // 2px instead of shrinking to sub-pixel. Derived from the overlay's own local→screen scale, so no
    // external "is this a preview" flag is needed.
    private float pathRadius()
    {
        float screenScale = DrawWidth > 0 ? ScreenSpaceDrawQuad.Width / DrawWidth : 1f;

        if (screenScale <= 0 || float.IsNaN(screenScale))
            return ChordColours.ConnectorPathRadius;

        return MathF.Max(ChordColours.ConnectorPathRadius, ChordColours.ConnectorPathRadius / screenScale);
    }

    protected override void Update()
    {
        base.Update();

        // Recomputed each frame so the line tracks the current draw scale (e.g. a window or preview resize).
        float lineRadius = pathRadius();

        // Chords that no longer exist (e.g. a member deleted in the editor, breaking the chord) drop out of
        // the index — dispose their orphaned paths so they don't linger on screen.
        pruneStalePaths();

        var aliveObjects = new HashSet<HitObject>(ring.AliveHitObjects.Select(d => d.HitObject));

        foreach (var group in chords.Groups)
        {
            pathsByStartTime.TryGetValue(group.StartTime, out var path);

            // Alpha is a pure function of the clock: full while the chord scrolls in, then an eased fade over
            // connector_fade_out after it reaches the ring at StartTime, and nothing at all when no member is
            // live (scrolled away, deleted, or seeked past). No transforms, so any seek/rewind is exact.
            bool anyAlive = group.Members.Any(m => aliveObjects.Contains(m.Object));
            double fade = Math.Clamp((Time.Current - group.StartTime) / connector_fade_out, 0, 1);
            float alpha = anyAlive ? 1f - (float)osu.Framework.Utils.Interpolation.ApplyEasing(Easing.OutQuint, fade) : 0f;

            if (alpha <= 0f)
            {
                if (path != null)
                    path.Alpha = 0;

                continue;
            }

            // ProgressAtTime is clamped to the ring, so the polygon grows in and then freezes at the ring.
            float radius = ring.ScrollingContainer.ProgressAtTime(group.StartTime);
            var vertices = group.Members.Select(m => polar(m.AngleDeg, radius)).ToList();
            if (vertices.Count >= 3)
                vertices.Add(vertices[0]); // close the loop

            path = ensurePath(group.StartTime, path, lineRadius);
            path.Vertices = vertices;
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);
            path.Alpha = alpha;
        }
    }

    // Remove + dispose any path whose chord start time is no longer present in the current chord index.
    private void pruneStalePaths()
    {
        if (pathsByStartTime.Count == 0)
            return;

        var current = new HashSet<double>(chords.Groups.Select(g => g.StartTime));

        foreach (var startTime in pathsByStartTime.Keys.Where(k => !current.Contains(k)).ToList())
        {
            if (pathsByStartTime.Remove(startTime, out var path))
                RemoveInternal(path, true);
        }
    }

    private SmoothPath ensurePath(double startTime, SmoothPath? path, float lineRadius)
    {
        if (path == null)
        {
            path = new SmoothPath
            {
                Anchor = Anchor.Centre,
                PathRadius = lineRadius,
                Colour = ChordColours.Connector,
                Alpha = 0,
            };
            pathsByStartTime[startTime] = path;
            AddInternal(path);
        }

        if (path.PathRadius != lineRadius)
            path.PathRadius = lineRadius;

        return path;
    }

    // Matches GarbusScrollingHitObjectContainer.PositionAtTime: +x right, -y up (screen y grows downward).
    private static Vector2 polar(int angleDeg, float radius)
    {
        float radians = MathUtils.DegToRad(angleDeg);
        return new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);
    }
}
