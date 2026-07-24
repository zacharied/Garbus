using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// A slider on the editor timeline. The drawable itself is a note-width strip spanning the duration
/// (positioned at the body's angle); the <see cref="SliderPolylineVisual"/> draws the actual node
/// polyline, which freely extends horizontally beyond the strip.
/// </summary>
public partial class EditorDrawableSliderBody : EditorDrawableGarbusHitObject<SliderBody>
{
    private readonly Container nestedContainer;

    public EditorDrawableSliderBody(SliderBody hitObject)
        : base(hitObject)
    {
        Width = EditorDrawableCardinalNote.NOTE_SIZE;
        Origin = Anchor.BottomCentre;
        AddInternal(nestedContainer = new Container { RelativeSizeAxes = Axes.Both });
    }

    protected override Drawable CreateVisual() => new SliderPolylineVisual(HitObject);

    // the polyline draws its own wrap copies (VisibleWrapCopies) — a base ghost twin would duplicate them.
    protected override float? TwinXFraction() => null;

    protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject) =>
        new EditorDrawableNestedStub((GarbusHitObject)hitObject);

    protected override void AddNestedHitObject(DrawableHitObject hitObject) => nestedContainer.Add(hitObject);

    protected override void ClearNestedHitObjects() => nestedContainer.Clear(false);
}
