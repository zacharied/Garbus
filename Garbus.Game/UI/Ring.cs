// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/Ring.cs). Original carries the ppy
// template MIT header: Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Objects;
using osu.Framework.Allocation;

namespace Garbus.Game.UI;

/// <summary>
/// The circular arena — the analog of mania's <c>Stage</c>. It owns the shared furniture drawn in the
/// polar coordinate system (the radial lines and the outer <see cref="Arc"/> ring), hosts the four
/// <see cref="Lane"/>s, and routes each incoming button to the lane matching its
/// <see cref="CardinalDirection"/>.
///
/// Paths sweep across every direction, so they are not lane objects: they live in the ring's own
/// <see cref="HitObjectContainer"/> (as bar lines do in a mania stage). That container also provides the
/// polar geometry a <see cref="Objects.Drawables.DrawableSliderBody"/> resolves each frame.
/// </summary>
[Cached]
public partial class Ring : Playfield
{
    private readonly Lane[] cardinalLanes = new Lane[Enum.GetValues<CardinalDirection>().Length];
    private readonly Lane[] shoulderLanes = new Lane[2];

    protected override HitObjectContainer CreateHitObjectContainer() => new GarbusScrollingHitObjectContainer();

    public Ring()
    {
        RelativeSizeAxes = Axes.Both;

        var laneContainer = new Container { RelativeSizeAxes = Axes.Both };

        // Lanes are created here (not in load) so they exist before the first Add is routed to them.
        foreach (var direction in Enum.GetValues<CardinalDirection>())
        {
            var lane = new Lane(new PlayfieldKeybeam(direction));
            cardinalLanes[(int)direction] = lane;

            laneContainer.Add(lane);
            AddNested(lane);
        }

        // Shoulder notes get their own lanes so their note-lock stays independent of the cardinal notes
        // they share a West/East angle with. They ride the L/R buttons rather than a cardinal, so no keybeam.
        foreach (var side in new[] { HorizontalDirection.Left, HorizontalDirection.Right })
        {
            var lane = new Lane();
            shoulderLanes[shoulderIndex(side)] = lane;

            laneContainer.Add(lane);
            AddNested(lane);
        }

        // Back-to-front: radial spokes, cross-lane paths, the lanes (keybeams + buttons on top), the ring.
        AddRangeInternal([
            new PlayfieldRadialLines(),
            HitObjectContainer,
            laneContainer,
            new Arc(0, 2 * MathF.PI)
            {
                Resolution = 128,
                Colour = Colour4.White,
            },
        ]);
    }

    public override void Add(HitObject hitObject)
    {
        if (laneFor(hitObject) is { } lane)
            lane.Add(hitObject);
        else
            base.Add(hitObject);
    }

    public override bool Remove(HitObject hitObject)
    {
        if (laneFor(hitObject) is { } lane)
            return lane.Remove(hitObject);

        return base.Remove(hitObject);
    }

    public override void Add(DrawableHitObject h)
    {
        if (laneFor(h.HitObject) is { } lane)
            lane.Add(h);
        else
            base.Add(h);
    }

    public override bool Remove(DrawableHitObject h)
    {
        if (laneFor(h.HitObject) is { } lane)
            return lane.Remove(h);

        return base.Remove(h);
    }

    /// <summary>
    /// The lane a hit object belongs to, or <c>null</c> for objects (e.g. sliders) that live in the ring's
    /// own container. Cardinal and shoulder notes are separated even where their angle coincides.
    /// </summary>
    private Lane? laneFor(HitObject hitObject) => hitObject switch
    {
        CardinalNote c => cardinalLanes[(int)c.Direction],
        HoldNote h => cardinalLanes[(int)h.Direction],
        ShoulderNote s => shoulderLanes[shoulderIndex(s.Side)],
        _ => null
    };

    private static int shoulderIndex(HorizontalDirection side) => side == HorizontalDirection.Left ? 0 : 1;
}
