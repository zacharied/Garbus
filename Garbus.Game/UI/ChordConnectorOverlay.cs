// Gameplay-only overlay: draws one thin, semi-transparent yellow polygon per same-start-time cardinal
// chord, inscribed at the chord's shared (co-radial) distance from centre. Lives in Ring below the hit
// objects. Geometry comes from ChordHighlighter + ProgressAtTime (never from live note positions), so it
// keeps its full shape while the chord is scrolling in, even if one member despawns early.
//
// It is shown at full opacity only while at least one member is alive AND still unjudged
// (ArmedState.Idle). The instant the chord reaches the ring and is judged, the polygon is frozen at its
// last (co-radial-with-the-ring) shape and fades out in place over CONNECTOR_FADE_OUT ms — it does NOT
// keep tracking ProgressAtTime through the note's fade-out (which would balloon it past the ring).

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

    // How long the polygon takes to fade out once its chord is judged.
    private const double connector_fade_out = 200;

    // One reusable path per chord, keyed by the chord's shared start time. Kept (faded) rather than
    // removed when the chord is no longer present, to avoid per-frame allocation churn.
    private readonly Dictionary<double, SmoothPath> pathsByStartTime = new Dictionary<double, SmoothPath>();

    // Start times whose path is currently shown at full opacity. Used to fire the fade-out transform
    // exactly once, on the present→judged transition (and to snap back on rewind).
    private readonly HashSet<double> shownStartTimes = new HashSet<double>();

    public ChordConnectorOverlay()
    {
        RelativeSizeAxes = Axes.Both;
    }

    protected override void Update()
    {
        base.Update();

        // Only members that are alive AND still unjudged keep the connector at full opacity. A judged note
        // (Hit/Miss) is being resolved at the ring, so its chord's connector freezes and fades out.
        var active = new HashSet<HitObject>(ring.AliveHitObjects
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

    // Matches GarbusScrollingHitObjectContainer.PositionAtTime: +x right, -y up (screen y grows downward).
    private static Vector2 polar(int angleDeg, float radius)
    {
        float radians = MathUtils.DegToRad(angleDeg);
        return new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);
    }
}
