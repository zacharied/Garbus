using osu.Framework.Allocation;
using osu.Framework.Graphics;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// Base for the simplified sprite representations shown on the editor timeline. These are entirely
/// separate from the gameplay drawables (which live in polar space): here the scrolling container drives
/// y from time, and this base drives x from the object's <see cref="IHasAngle.AngleDeg"/> every frame so
/// edits reflect immediately.
///
/// The editor scrolls <see cref="Gameplay.UI.Scrolling.ScrollingDirection.Down"/>, so drawables
/// anchor to the bottom and objects with a duration grow upward (the container sets their height).
/// Objects auto-judge when their time passes (for hitsound feedback) but never animate or fade on
/// judgement — they must stay visible and editable.
///
/// Visuals come from <see cref="CreateVisual"/> so a second instance (the ghost twin) can be shown,
/// offset by ±360°, whenever the object sits within a ghost band's reach of a grid edge.
/// </summary>
public abstract partial class EditorDrawableGarbusHitObject<T> : DrawableHitObject<GarbusHitObject>
    where T : GarbusHitObject, IHasAngle
{
    public new T HitObject => (T)base.HitObject;

    private Drawable? twin;

    // Show/Hide allocate a fade transform per call, and updateTwin runs every frame — null until the
    // twin's first state is applied, then tracks it so a fade fires only on an actual transition.
    private bool? twinShown;

    protected EditorDrawableGarbusHitObject(T hitObject)
        : base(hitObject)
    {
        Anchor = Anchor.BottomLeft;
        // Single-press sprites straddle their time line; duration objects override to BottomCentre so
        // the container-set height spans start → end exactly.
        Origin = Anchor.Centre;
        RelativePositionAxes = Axes.X;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(CreateVisual());
    }

    /// <summary>
    /// Creates this object's visual representation. May be called more than once — a fresh instance
    /// backs the ghost wrap-around twin.
    /// </summary>
    protected abstract Drawable CreateVisual();

    protected override void Update()
    {
        base.Update();
        X = ComputeXFraction();
        updateTwin();
    }

    private void updateTwin()
    {
        if (TwinXFraction() is float twinX)
        {
            if (twin == null)
                AddInternal(twin = CreateVisual());

            twin.X = (twinX - ComputeXFraction()) * (Parent?.DrawWidth ?? 0);

            if (twinShown != true)
            {
                twin.Show();
                twinShown = true;
            }
        }
        else if (twin != null && twinShown != false)
        {
            twin.Hide();
            twinShown = false;
        }
    }

    /// <summary>The x position (as a fraction of the full editor width). Defaults to the object's angle.</summary>
    protected virtual float ComputeXFraction() => EditorAngleMapping.ToX(HitObject.AngleDeg);

    /// <summary>Where the ghost twin sits (x-fraction of the full width), or null when no twin is visible.</summary>
    protected virtual float? TwinXFraction() => EditorAngleMapping.GhostTwinX(HitObject.AngleDeg);

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset >= 0)
            ApplyMaxResult();
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        // Editor objects never animate away on hit/miss; they scroll out via lifetime instead.
    }

    public override void PlaySamples()
    {
        // Hitsound feedback is only wanted when the playhead crosses the object with the clock
        // actually running — never while scrubbing/seeking with the clock stopped (a wheel-seek
        // judges every object it skips over). This drawable's Clock IS the EditorClock (ComposeTab
        // wires the composer subtree to it).
        if (Clock.IsRunning)
            base.PlaySamples();
    }

    /// <summary>
    /// The scrolling container owns editor lifetimes (it writes scroll-in → scroll-out times to the
    /// lifetime entry directly). Drawable-side writes come from DrawableHitObject.UpdateState's
    /// judgement-driven expiry: editor objects auto-judge and have no hit-state transforms, so that
    /// clamp would set LifetimeEnd to the judgement time — killing the drawable at its own start time
    /// whenever defaults are re-applied in place. Swallow them.
    /// </summary>
    public override double LifetimeEnd
    {
        get => base.LifetimeEnd;
        set { }
    }
}
