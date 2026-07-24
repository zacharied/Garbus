// Draws one thin, semi-transparent yellow polygon per same-start-time cardinal
// chord, inscribed at the chord's shared (co-radial) distance from centre. Lives in Ring above the hit
// objects but below judgement feedback and the outer ring. Geometry comes from ChordHighlighter +
// ProgressAtTime, so it keeps its full shape while the chord is scrolling in, even if one member despawns early.
//
// In ordinary gameplay it is shown at full opacity only while at least one member is alive AND still unjudged
// (ArmedState.Idle). The instant the chord reaches the ring and is judged, the polygon is frozen at its
// last (co-radial-with-the-ring) shape and fades out in place over CONNECTOR_FADE_OUT ms — it does NOT
// keep tracking ProgressAtTime through the note's fade-out (which would balloon it past the ring).
// Preview instead derives geometry and alpha directly from the shared chord time so stopped seeks and
// rewinds do not depend on observing the judgement transition.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Edit.Preview;
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

    [Resolved(CanBeNull = true)]
    private ChartPreviewContext? previewContext { get; set; }

    // How long the polygon takes to fade out once its chord is judged.
    private const double connector_fade_out = 200;

    // One reusable path per chord, keyed by the chord's shared start time. Kept (faded) while the chord
    // remains indexed, to avoid per-frame allocation churn.
    private readonly Dictionary<double, SmoothPath> pathsByStartTime = new Dictionary<double, SmoothPath>();

    // Start times whose path is currently shown at full opacity. Used to fire the fade-out transform
    // exactly once, on the present→judged transition (and to snap back on rewind).
    private readonly HashSet<double> shownStartTimes = new HashSet<double>();

    private IReadOnlyList<ChordIndex.ChordGroup> groups = Array.Empty<ChordIndex.ChordGroup>();

    public ChordConnectorOverlay()
    {
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        chords.IndexChanged += synchronizeIndex;
        synchronizeIndex();
    }

    private void synchronizeIndex()
    {
        groups = chords.Groups;
        var currentStartTimes = groups.Select(g => g.StartTime).ToHashSet();

        foreach (double staleStartTime in pathsByStartTime.Keys
                                                          .Where(t => !currentStartTimes.Contains(t))
                                                          .ToArray())
        {
            SmoothPath stale = pathsByStartTime[staleStartTime];
            pathsByStartTime.Remove(staleStartTime);
            shownStartTimes.Remove(staleStartTime);
            RemoveInternal(stale, true);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (previewContext != null)
        {
            updatePreview();
            return;
        }

        // Only members that are alive AND still unjudged keep the connector at full opacity. A judged note
        // (Hit/Miss) is being resolved at the ring, so its chord's connector freezes and fades out.
        var active = new HashSet<HitObject>(ring.AliveHitObjects
            .Where(d => d.State.Value == ArmedState.Idle)
            .Select(d => d.HitObject));

        foreach (var group in groups)
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

                if (path == null)
                {
                    path = new SmoothPath
                    {
                        Anchor = Anchor.Centre,
                        PathRadius = ChordColours.ConnectorPathRadius,
                        Colour = ChordColours.Connector,
                        Alpha = 0,
                    };
                    pathsByStartTime[group.StartTime] = path;
                    AddInternal(path);
                }

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

    private void updatePreview()
    {
        var alive = new HashSet<HitObject>(ring.AliveHitObjects.Select(d => d.HitObject));

        foreach (var group in groups)
        {
            bool present = group.Members.Any(m => alive.Contains(m.Object));
            pathsByStartTime.TryGetValue(group.StartTime, out var path);

            if (!present)
            {
                if (path != null)
                    path.Alpha = 0;

                continue;
            }

            float radius = ring.ScrollingContainer.ProgressAtTime(group.StartTime);

            var vertices = group.Members.Select(m => polar(m.AngleDeg, radius)).ToList();
            if (vertices.Count >= 3)
                vertices.Add(vertices[0]);

            if (path == null)
            {
                path = new SmoothPath
                {
                    Anchor = Anchor.Centre,
                    PathRadius = ChordColours.ConnectorPathRadius,
                    Colour = ChordColours.Connector,
                    Alpha = 0,
                };
                pathsByStartTime[group.StartTime] = path;
                AddInternal(path);
            }

            path.Vertices = vertices;
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);

            double elapsed = Time.Current - group.StartTime;
            path.Alpha = elapsed <= 0
                ? 1
                : elapsed >= connector_fade_out
                    ? 0
                    : osu.Framework.Utils.Interpolation.ValueAt(elapsed, 1f, 0f, 0, connector_fade_out, Easing.OutQuint);
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        if (chords != null)
            chords.IndexChanged -= synchronizeIndex;

        base.Dispose(isDisposing);
    }

    // Matches GarbusScrollingHitObjectContainer.PositionAtTime: +x right, -y up (screen y grows downward).
    private static Vector2 polar(int angleDeg, float radius)
    {
        float radians = MathUtils.DegToRad(angleDeg);
        return new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);
    }
}
