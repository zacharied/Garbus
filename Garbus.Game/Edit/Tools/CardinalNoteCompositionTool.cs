// Uses a plain text label rather than an icon glyph (editor visuals don't matter here).

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using Garbus.Game.Edit.Blueprints;
using Garbus.Game.Edit.Compose;

namespace Garbus.Game.Edit.Tools;

public class CardinalNoteCompositionTool : CompositionTool
{
    public CardinalNoteCompositionTool()
        : base("Note")
    {
    }

    public override Drawable CreateIcon() => new SpriteText { Text = "N" };

    public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new CardinalNotePlacementBlueprint();
}
