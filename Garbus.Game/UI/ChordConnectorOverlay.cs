// Gameplay-only overlay: draws one thin, semi-transparent yellow polygon per same-start-time cardinal
// chord, inscribed at the chord's shared (co-radial) distance from centre. Lives in Ring below the hit
// objects. Geometry comes from ChordHighlighter + ProgressAtTime (never from live note positions), so it
// keeps its full shape until the last member of the chord has despawned.

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

    // One reusable path per chord, keyed by the chord's shared start time. Hidden when the chord is not
    // currently present, rather than removed, to avoid per-frame allocation churn.
    private readonly Dictionary<double, SmoothPath> pathsByStartTime = new Dictionary<double, SmoothPath>();

    public ChordConnectorOverlay()
    {
        RelativeSizeAxes = Axes.Both;
    }

    protected override void Update()
    {
        base.Update();

        var alive = new HashSet<HitObject>(ring.AliveHitObjects.Select(d => d.HitObject));
        var present = new HashSet<double>();

        foreach (var group in chords.Groups)
        {
            // Present while ANY member still has an alive drawable (covers the whole hit/miss fade-out).
            if (!group.Members.Any(m => alive.Contains(m.Object)))
                continue;

            present.Add(group.StartTime);

            float radius = ring.ScrollingContainer.ProgressAtTime(group.StartTime);

            var vertices = group.Members.Select(m => polar(m.AngleDeg, radius)).ToList();
            if (vertices.Count >= 3)
                vertices.Add(vertices[0]); // close the loop

            if (!pathsByStartTime.TryGetValue(group.StartTime, out var path))
            {
                path = new SmoothPath
                {
                    Anchor = Anchor.Centre,
                    PathRadius = ChordColours.ConnectorPathRadius,
                    Colour = ChordColours.Connector,
                };
                pathsByStartTime[group.StartTime] = path;
                AddInternal(path);
            }

            path.Vertices = vertices;
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);
            path.Show();
        }

        foreach (var kvp in pathsByStartTime)
        {
            if (!present.Contains(kvp.Key))
                kvp.Value.Hide();
        }
    }

    // Matches GarbusScrollingHitObjectContainer.PositionAtTime: +x right, -y up (screen y grows downward).
    private static Vector2 polar(int angleDeg, float radius)
    {
        float radians = MathUtils.DegToRad(angleDeg);
        return new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);
    }
}
