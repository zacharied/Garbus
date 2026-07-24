using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace Garbus.Game.Edit.Drawables;

/// <summary>A ruleset texture displayed fit-to-bounds; the reusable building block of editor visuals.</summary>
public partial class EditorSpritePiece : CompositeDrawable
{
    private readonly string textureName;

    public EditorSpritePiece(string textureName)
    {
        this.textureName = textureName;
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        var sprite = new Sprite
        {
            RelativeSizeAxes = Axes.Both,
            FillMode = FillMode.Fit,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        };

        // Texture must be assigned AFTER RelativeSizeAxes: the setter auto-sizes a zero-sized sprite to
        // the raw texture dimensions, which relative sizing would then treat as a huge multiplier.
        sprite.Texture = textures.Get(textureName);

        InternalChild = sprite;
    }
}
