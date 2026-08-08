using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace Garbus.Game.UI;

/// <summary>
/// The current combo drawn as large text at the geometric centre of the <see cref="Ring"/> — behind
/// the <see cref="SpawnHaloRing"/> and every hit object. Objects spawn on the halo and travel
/// outward, so the halo ring draws in front of this to keep its radius reading exactly against the
/// digits. Hidden at combo 0 so the empty centre reads clean before the first hit.
/// </summary>
public partial class ComboDisplay : SpriteText
{
    private readonly BindableInt combo = new BindableInt();

    private const float display_alpha = 0.6f;

    [BackgroundDependencyLoader]
    private void load(GarbusPlayfield playfield)
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
        Font = new FontUsage("Inter-Bold").With(size: 96);
        Colour = Color4.WhiteSmoke;
        Alpha = 0;

        combo.BindTo(playfield.Combo);
        combo.BindValueChanged(onComboChanged, true);
    }

    private void onComboChanged(ValueChangedEvent<int> c)
    {
        Text = c.NewValue.ToString();
        Alpha = c.NewValue > 0 ? display_alpha : 0;
    }
}
