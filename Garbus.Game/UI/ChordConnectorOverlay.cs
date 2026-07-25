// Draws one thin, semi-transparent yellow polygon per same-start-time cardinal chord, inscribed at the
// chord's shared (co-radial) distance from centre. Lives in Ring below the hit objects. Geometry comes from
// ChordHighlighter + ProgressAtTime (never from live note positions), so it keeps its full shape while the
// chord is scrolling in, even if one member despawns early.
//
// Two presentation paths share the geometry:
//  - Gameplay (played forward in real time): shown at full opacity while at least one member is alive and
//    still unjudged (ArmedState.Idle). The instant the chord is judged at the ring, the polygon is frozen
//    and fades out via a real-time transform over CONNECTOR_FADE_OUT ms.
//  - The editor mini preview drives auto-hit drawables on a clock the editor scrubs (jumps, pauses, rewinds).
//    Real-time fade transforms never advance under a scrubbed clock and would leave lines stuck on screen, so
//    there the whole presentation (geometry + alpha) is a pure function of the clock — recomputed and
//    assigned every frame, exact under any seek.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
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

    // One reusable path per chord, keyed by the chord's shared start time. Kept (faded) rather than
    // removed when the chord is no longer present, to avoid per-frame allocation churn.
    private readonly Dictionary<double, SmoothPath> pathsByStartTime = new Dictionary<double, SmoothPath>();

    // Start times whose path is currently shown at full opacity. Used (gameplay only) to fire the fade-out
    // transform exactly once, on the present→judged transition (and to snap back on rewind).
    private readonly HashSet<double> shownStartTimes = new HashSet<double>();

    // Latches true once this overlay is driving auto-hit drawables (the mini preview). An overlay never
    // switches context, so once seen it stays true even on a frame where nothing is alive — which is what
    // lets stale preview lines be cleared when seeking to an empty region.
    private bool autoContext;

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

        var aliveByObject = new Dictionary<HitObject, DrawableHitObject>();
        foreach (var d in ring.AliveHitObjects)
        {
            aliveByObject[d.HitObject] = d;
            if (d.AutoHitActive)
                autoContext = true;
        }

        // Dispose paths whose chord no longer exists. A chord only leaves chords.Groups when the chart is
        // edited (e.g. the mini preview deletes a member and the 2-note chord drops to one) — never on
        // judgement — so a still-present chord is never pruned mid-fade. Without this, a broken chord's path is
        // never revisited by the loops below and lingers on screen at its last alpha.
        pruneStalePaths();

        if (autoContext)
        {
            foreach (var group in chords.Groups)
                updateAutoGroup(group, lineRadius, aliveByObject);

            return;
        }

        updateGameplay(lineRadius, aliveByObject);
    }

    // Auto-hit (mini preview): geometry + alpha are a pure function of the clock, assigned directly with no
    // transforms so the result is exact under any seek/rewind. Alpha is full while the chord is scrolling in,
    // then fades linearly over connector_fade_out after the ring-arrival time (StartTime); no alive members
    // (seeked away) clears it. Geometry uses ProgressAtTime, already clamped to the ring, so the polygon grows
    // in and freezes at the ring.
    private void updateAutoGroup(ChordIndex.ChordGroup group, float lineRadius, Dictionary<HitObject, DrawableHitObject> aliveByObject)
    {
        pathsByStartTime.TryGetValue(group.StartTime, out var path);

        bool anyAlive = group.Members.Any(m => aliveByObject.ContainsKey(m.Object));

        float alpha = 0f;
        if (anyAlive)
        {
            double elapsedPastRing = Time.Current - group.StartTime;
            alpha = elapsedPastRing < 0
                ? 1f
                : 1f - (float)(elapsedPastRing / connector_fade_out);
            alpha = Math.Clamp(alpha, 0f, 1f);
        }

        if (alpha <= 0f)
        {
            if (path != null)
                path.Alpha = 0;

            return;
        }

        float radius = ring.ScrollingContainer.ProgressAtTime(group.StartTime);
        var vertices = group.Members.Select(m => polar(m.AngleDeg, radius)).ToList();
        if (vertices.Count >= 3)
            vertices.Add(vertices[0]); // close the loop

        path = ensurePath(group.StartTime, path, lineRadius);
        path.Vertices = vertices;
        path.Position = -path.PositionInBoundingBox(Vector2.Zero);
        path.Alpha = alpha;
    }

    // Gameplay: unchanged real-time behaviour — full opacity while any member is alive and unjudged, then a
    // one-shot fade-out transform on the present→judged edge.
    private void updateGameplay(float lineRadius, Dictionary<HitObject, DrawableHitObject> aliveByObject)
    {
        var active = new HashSet<HitObject>(aliveByObject.Values
            .Where(d => d.State.Value == ArmedState.Idle)
            .Select(d => d.HitObject));

        foreach (var group in chords.Groups)
        {
            // Present while ANY member is still scrolling in (alive and unjudged).
            bool present = group.Members.Any(m => active.Contains(m.Object));
            pathsByStartTime.TryGetValue(group.StartTime, out var path);

            if (present)
            {
                float radius = ring.ScrollingContainer.ProgressAtTime(group.StartTime);

                var vertices = group.Members.Select(m => polar(m.AngleDeg, radius)).ToList();
                if (vertices.Count >= 3)
                    vertices.Add(vertices[0]); // close the loop

                path = ensurePath(group.StartTime, path, lineRadius);
                path.Vertices = vertices;
                path.Position = -path.PositionInBoundingBox(Vector2.Zero);

                // First appearance, or reappearance after a rewind un-judged the chord: cancel any pending
                // fade and snap fully visible. (Add returns true only on the not-present → present edge.)
                if (shownStartTimes.Add(group.StartTime))
                {
                    path.ClearTransforms();
                    path.Alpha = 1;
                }
            }
            else if (path != null && shownStartTimes.Remove(group.StartTime))
            {
                // Just judged / despawned: leave the frozen geometry in place and fade it out. (Remove
                // returns true only on the present → not-present edge, so the fade fires once.)
                path.FadeOut(connector_fade_out, Easing.OutQuint);
            }
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

            shownStartTimes.Remove(startTime);
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
