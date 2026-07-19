// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/Lane.cs). Original carries the ppy
// template MIT header: Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Objects.Drawables;

namespace Garbus.Game.UI;

/// <summary>
/// A "column": the analog of mania's <c>Column</c>. Every note routed here lives in the lane's own
/// <see cref="HitObjectContainer"/>, and the lane runs its own <see cref="GarbusOrderedHitPolicy"/> over just
/// those objects — so note lock is independent per lane. Cardinal notes and shoulder notes are grouped into
/// separate lanes even where they share an angle (West/East), so their note-lock never interferes.
///
/// A lane may optionally own a <see cref="PlayfieldKeybeam"/> that lights up for its direction (the four
/// cardinal lanes do; the shoulder lanes, driven by the L/R buttons rather than a cardinal, do not).
///
/// All lanes are full-size and share the same polar centre, so they physically overlap; the "lane" is a
/// logical grouping, not a spatial region like a mania column.
/// </summary>
[Cached]
public partial class Lane : Playfield
{
    private readonly PlayfieldKeybeam? keybeam;

    private readonly GarbusOrderedHitPolicy hitPolicy;

    protected override HitObjectContainer CreateHitObjectContainer() => new GarbusScrollingHitObjectContainer();

    public Lane(PlayfieldKeybeam? keybeam = null)
    {
        this.keybeam = keybeam;
        RelativeSizeAxes = Axes.Both;

        // Scoped to this lane's container, so it only ever considers this lane's objects.
        hitPolicy = new GarbusOrderedHitPolicy(HitObjectContainer);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        if (keybeam == null)
        {
            AddInternal(HitObjectContainer);
            return;
        }

        // The keybeam draws behind the hit objects (a proxy renders it at the back), but the real drawable
        // sits in front of them in the input queue. That way it observes each press (returning false) before
        // a button consumes it, so it still lights up even though a hit now consumes the press. Mirrors the
        // way mania proxies its column pieces to separate draw order from input order.
        AddRangeInternal([
            keybeam.CreateProxy(),
            HitObjectContainer,
            keybeam,
        ]);
    }

    protected override void OnNewDrawableHitObject(DrawableHitObject drawableHitObject)
    {
        base.OnNewDrawableHitObject(drawableHitObject);

        if (drawableHitObject is IHittableNote note)
            note.CheckHittable = hitPolicy.IsHittable;
    }
}
