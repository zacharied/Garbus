using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace Garbus.Game.UI;

/// <summary>
/// The current combo drawn as large text at the geometric centre of the <see cref="Ring"/> — behind
/// every hit object (objects spawn at the centre and travel outward, so this reads as a number the
/// notes emerge from). Hidden at combo 0 so the empty centre reads clean before the first hit.
/// </summary>
public partial class ComboDisplay : SpriteText
{
    private readonly BindableInt combo = new BindableInt();

    [BackgroundDependencyLoader]
    private void load(GarbusPlayfield playfield)
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
        Font = new FontUsage("Noto-Basic").With(size: 100);
        Colour = Color4.White;
        Alpha = 0;

        combo.BindTo(playfield.Combo);
        combo.BindValueChanged(onComboChanged, true);
    }

    private void onComboChanged(ValueChangedEvent<int> c)
    {
        Text = c.NewValue.ToString();
        Alpha = c.NewValue > 0 ? 1 : 0;
    }
}
