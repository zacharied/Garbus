// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/BigAssCirclePlayfield.cs).
// BigAssCirclePlayfield → GarbusPlayfield. Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Input;

namespace Garbus.Game.UI;

/// <summary>
/// The top-level playfield — the analog of mania's <c>ManiaPlayfield</c>. It nests a single
/// <see cref="Ring"/> (the arena that owns the lanes) and forwards every hit object to it, keeping only
/// the global overlays that are not tied to a single lane.
/// </summary>
[Cached]
public partial class GarbusPlayfield : Playfield
{
    private readonly Ring ring = new Ring();

    private readonly Drawable stickIndicatorL = new StickIndicator() { Side = HorizontalDirection.Left };
    private readonly Drawable stickIndicatorR = new StickIndicator() { Side = HorizontalDirection.Right };

    [Cached]
    private AnalogInputManager analogInputManager { get; set; } = new AnalogInputManager();

    public GarbusPlayfield()
    {
        Padding = new MarginPadding(150);
        AddNested(ring);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddRangeInternal([
            analogInputManager,
            ring,
            stickIndicatorL,
            stickIndicatorR,
        ]);
    }

    public override void Add(HitObject hitObject) => ring.Add(hitObject);

    public override bool Remove(HitObject hitObject) => ring.Remove(hitObject);

    public override void Add(DrawableHitObject h) => ring.Add(h);

    public override bool Remove(DrawableHitObject h) => ring.Remove(h);
}
