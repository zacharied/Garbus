using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace Garbus.Game.Input
{
    /// <summary>
    /// Draws the icon for a <see cref="GamepadButton"/> on a given <see cref="GamepadType"/>, resolved via
    /// <see cref="GamepadButtonIcons"/>. Square by default and scaled to fit, so callers set a single
    /// <see cref="Drawable.Size"/> (or width/height) and get a correctly proportioned glyph. If the
    /// controller has no artwork for the button it simply draws nothing.
    /// </summary>
    public partial class GamepadButtonSprite : Sprite
    {
        private readonly GamepadButton button;
        private readonly GamepadType type;

        public GamepadButtonSprite(GamepadButton button, GamepadType type = GamepadButtonIcons.DefaultType)
        {
            this.button = button;
            this.type = type;

            // The source art is square; keep aspect so non-square sizes letterbox rather than stretch.
            FillMode = FillMode.Fit;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            string? path = GamepadButtonIcons.ResolveTexturePath(button, type);
            if (path != null)
                Texture = textures.Get(path);
        }
    }
}
