// Editor timeline representation of a ShoulderHoldNote: a purple head square in its side's lane strip with
// a translucent body over the duration. Combines EditorDrawableShoulderNote (x from ShoulderXFraction) with
// EditorDrawableCardinalHoldNote (duration body + head hit-testing).

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Drawables;

public partial class EditorDrawableShoulderHoldNote : EditorDrawableGarbusHitObject<ShoulderHoldNote>
{
    private readonly Container nestedContainer;
    private readonly List<Drawable> headPieces = new List<Drawable>();

    public EditorDrawableShoulderHoldNote(ShoulderHoldNote hitObject)
        : base(hitObject)
    {
        Width = EditorDrawableCardinalNote.NOTE_SIZE;
        Origin = Anchor.BottomCentre;
        AddInternal(nestedContainer = new Container { RelativeSizeAxes = Axes.Both });
    }

    protected override Drawable CreateVisual()
    {
        EditorSpritePiece head;

        var visual = new Container
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
                    Colour = Color4.MediumPurple,
                    Alpha = 0.35f,
                },
                head = new EditorSpritePiece("square")
                {
                    RelativeSizeAxes = Axes.None,
                    Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE),
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.Centre,
                    Colour = Color4.MediumPurple,
                },
            },
        };

        headPieces.Add(head);
        return visual;
    }

    // Shoulder notes sit in their side's dedicated lane strip, not at their derived in-game angle.
    protected override float ComputeXFraction() => GarbusEditorPlayfield.ShoulderXFraction(HitObject.Side);

    protected override float? TwinXFraction() => null;

    public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
    {
        if (base.ReceivePositionalInputAt(screenSpacePos))
            return true;

        foreach (var head in headPieces)
        {
            if (head.ScreenSpaceDrawQuad.Contains(screenSpacePos))
                return true;
        }

        return false;
    }

    protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject) =>
        new EditorDrawableNestedStub((GarbusHitObject)hitObject);

    protected override void AddNestedHitObject(DrawableHitObject hitObject) => nestedContainer.Add(hitObject);

    protected override void ClearNestedHitObjects() => nestedContainer.Clear(false);
}
