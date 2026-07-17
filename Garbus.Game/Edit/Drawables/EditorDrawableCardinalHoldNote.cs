// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Drawables/EditorDrawableCardinalHoldNote.cs).
// BacHitObject → GarbusHitObject; DrawableHitObject import updated.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects; // ChordColours, ChordHighlighter
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// A hold note on the editor timeline: the square head at its start time with a translucent body
/// stretching over the duration (the scrolling container sets the height for
/// <see cref="Garbus.Game.Gameplay.Objects.Types.IHasDuration"/> objects; downward scrolling grows it
/// upward from the bottom origin).
/// </summary>
public partial class EditorDrawableCardinalHoldNote : EditorDrawableGarbusHitObject<CardinalHoldNote>
{
    private readonly Container nestedContainer;

    // The head sprites (primary visual + ghost twin) are centred on the start line, so their bottom
    // halves hang below this drawable's duration rectangle — track them so hit-testing covers them.
    private readonly List<Drawable> headPieces = new List<Drawable>();

    [Resolved]
    private ChordHighlighter chords { get; set; } = null!;

    public EditorDrawableCardinalHoldNote(CardinalHoldNote hitObject)
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
                    Colour = Color4.White,
                    Alpha = 0.35f,
                },
                head = new EditorSpritePiece("square")
                {
                    RelativeSizeAxes = Axes.None,
                    Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE),
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.Centre,
                },
            },
        };

        headPieces.Add(head);
        return visual;
    }

    public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
    {
        if (base.ReceivePositionalInputAt(screenSpacePos))
            return true;

        // The head square straddles the start line; accept its full extent (ISSUES.md: hold notes
        // must be selectable by the head, not just the body).
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

    protected override void Update()
    {
        base.Update();

        // Whole-drawable tint covers head + body and the ghost twin.
        Colour = chords.IsInChord(HitObject) ? ChordColours.Highlight : Color4.White;
    }
}
