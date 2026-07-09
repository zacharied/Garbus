// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/Components/EditSquarePiece.cs).
// OsuColour resolve removed — the border colour (osu's OsuColour.YellowDark) is inlined as
// new Colour4(255, 196, 40, 255).

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>The yellow outline box used by note blueprints (the Garbus analogue of mania's EditNotePiece).</summary>
internal partial class EditSquarePiece : CompositeDrawable
{
    public EditSquarePiece()
    {
        InternalChild = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            BorderThickness = 3,
            BorderColour = new Colour4(255, 196, 40, 255),
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                AlwaysPresent = true,
            },
        };
    }
}
