// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Drawables/EditorDrawableHoldNote.cs).
// BacHitObject → GarbusHitObject; DrawableHitObject import updated.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// A hold note on the editor timeline: the square head at its start time with a translucent body
/// stretching over the duration (the scrolling container sets the height for
/// <see cref="Garbus.Game.Gameplay.Objects.Types.IHasDuration"/> objects; downward scrolling grows it
/// upward from the bottom origin).
/// </summary>
public partial class EditorDrawableHoldNote : EditorDrawableGarbusHitObject<HoldNote>
{
    private readonly Container nestedContainer;

    public EditorDrawableHoldNote(HoldNote hitObject)
        : base(hitObject)
    {
        Width = EditorDrawableCardinalNote.NOTE_SIZE;
        Origin = Anchor.BottomCentre;
        AddInternal(nestedContainer = new Container { RelativeSizeAxes = Axes.Both });
    }

    protected override Drawable CreateVisual() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 12,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Colour = Color4.White,
                Alpha = 0.35f,
            },
            new EditorSpritePiece("square")
            {
                RelativeSizeAxes = Axes.None,
                Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE),
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.Centre,
            },
        },
    };

    protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject) =>
        new EditorDrawableNestedStub((GarbusHitObject)hitObject);

    protected override void AddNestedHitObject(DrawableHitObject hitObject) => nestedContainer.Add(hitObject);

    protected override void ClearNestedHitObjects() => nestedContainer.Clear(false);
}
