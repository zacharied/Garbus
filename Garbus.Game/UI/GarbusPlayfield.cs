using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Input;
using Garbus.Game.Objects;

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

    private readonly WarningIndicatorDisplay warningIndicators = new WarningIndicatorDisplay();

    [Cached]
    private AnalogInputManager analogInputManager { get; set; } = new AnalogInputManager();

    [Cached]
    private ChordHighlighter chordHighlighter { get; set; } = new ChordHighlighter();

    /// <summary>
    /// The current combo, surfaced for the centre <see cref="ComboDisplay"/> to bind to. Driven by the
    /// screen that owns scoring (<see cref="Screens.PlayScreen"/>), which pushes the value each frame.
    /// </summary>
    public BindableInt Combo { get; } = new BindableInt();

    private readonly bool interactive;

    public GarbusPlayfield(bool interactive = true)
    {
        this.interactive = interactive;
        Padding = new MarginPadding(30);
        AddNested(ring);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        // warningIndicators sits before the ring so the ring's white stroke draws on top of the glow's
        // clipped inner edge — the glow reads as light emanating from under the ring.
        var children = new List<Drawable> { warningIndicators, ring };

        if (interactive)
        {
            // Input manager first so the ring's stroke draws over it; stick indicators are input feedback.
            children.Insert(0, analogInputManager);
            children.Add(stickIndicatorL);
            children.Add(stickIndicatorR);
        }

        AddRangeInternal(children.ToArray());
    }

    public override void Add(HitObject hitObject) => ring.Add(hitObject);

    public override bool Remove(HitObject hitObject) => ring.Remove(hitObject);

    public override void Add(DrawableHitObject h) => ring.Add(h);

    public override bool Remove(DrawableHitObject h) => ring.Remove(h);

    /// <summary>
    /// Hand the full set of chart hit objects to the warning-indicator display (approaching heads/slams)
    /// and rebuild the chord highlight index. Call once after adding drawables.
    /// </summary>
    public void SetHitObjects(IEnumerable<GarbusHitObject> hitObjects)
    {
        var list = hitObjects.ToList();
        warningIndicators.SetHitObjects(list);
        chordHighlighter.Rebuild(list);
    }

    /// <summary>The warning-indicator display (GAR-3). Exposed for wiring and tests.</summary>
    public WarningIndicatorDisplay WarningIndicators => warningIndicators;
}
