using System;
using Garbus.Game.Gameplay.UI.Scrolling;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace Garbus.Game.UI;

/// <summary>
/// Draws the spawn halo — the radius a hit object appears on and holds at while its spawn animation
/// plays — as a thin ring. Specified in docs/presentation-specs/Playfield.md ("Spawn halo and spawn
/// phase") and docs/superpowers/specs/2026-08-01-spawn-halo-ring-design.md.
///
/// The radius comes entirely from this container's own size: <see cref="Arc"/> derives its radius
/// from its <c>ChildSize</c>, so sizing this wrapper to the halo fraction is what puts the stroke on
/// the halo. Every size in the chain is relative, so a playfield resize needs no handling.
/// </summary>
public sealed partial class SpawnHaloRing : Container
{
    private const float default_thickness = 2;

    // The outer ring is opaque white; this reads as gray against the dark playfield. Translucency
    // matters specifically because the ring draws in FRONT of the centre combo counter (see Ring's
    // child order) — it tints the digits rather than slicing them.
    private const float default_alpha = 0.35f;

    // Arc's default 32 segments would give ~12px chords at the halo's ~120px diameter, which reads as
    // visible faceting. The halo is a small circle, so a higher resolution is cheap.
    private const int resolution = 64;

    /// <summary>Full width of the drawn ring stroke, in pixels.</summary>
    public BindableFloat Thickness { get; } = new BindableFloat(default_thickness);

    [Resolved(CanBeNull = true)]
    private GarbusScrollingInfo? scrollingInfo { get; set; }

    // Mirrors GarbusScrollingHitObjectContainer's fallback: a bare test scene with no cached
    // GarbusScrollingInfo still gets the production default rather than a second literal to drift.
    private readonly GarbusScrollingInfo fallbackScrollingInfo = new GarbusScrollingInfo();

    private readonly IBindable<double> spawnHaloFraction = new BindableDouble();

    private readonly Arc arc;

    public SpawnHaloRing()
    {
        RelativeSizeAxes = Axes.Both;
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
        Alpha = default_alpha;

        AddInternal(arc = new Arc(0, 2 * MathF.PI, default_thickness) { Resolution = resolution });
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        arc.Thickness.BindTo(Thickness);

        spawnHaloFraction.BindTo((scrollingInfo ?? fallbackScrollingInfo).SpawnHaloFraction);
        spawnHaloFraction.BindValueChanged(_ => updateSize(), true);
    }

    // The relative size is the fraction ITSELF, not twice it. ScrollLength is already a radius
    // (min(W, H) / 2 of the playfield), so this container's halving and the playfield's cancel: a
    // relative size of `fraction` puts Arc's own min(ChildSize) / 2 at exactly
    // fraction * ScrollLength. Sizing to 2 * fraction would draw the ring at twice the halo radius.
    //
    // A zero fraction collapses this to zero size, and Arc already skips a non-positive radius, so
    // nothing is drawn — no separate hide is needed.
    private void updateSize() => Size = new Vector2((float)spawnHaloFraction.Value);
}
