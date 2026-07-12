using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK.Graphics;

namespace Garbus.Game.Edit;

/// <summary>
/// A floating toggle at the top-left of the compose playfield that flips the angle-view direction
/// (<see cref="GarbusHitObjectComposer.ReverseAngleView"/>). The label shows the current x→θ rotation
/// sense: <c>⇄ CCW</c> when normal (South centred), <c>⇄ CW</c> when reversed (North centred).
/// </summary>
public partial class AngleDirectionToggleButton : CompositeDrawable
{
    [Resolved]
    private GarbusHitObjectComposer composer { get; set; } = null!;

    private readonly BindableBool reversed = new BindableBool();
    private SpriteText label = null!;

    /// <summary>The current label text (exposed for tests).</summary>
    public string LabelText => label.Text.ToString();

    [BackgroundDependencyLoader]
    private void load()
    {
        AutoSizeAxes = Axes.Both;
        Anchor = Anchor.TopLeft;
        Origin = Anchor.TopLeft;
        Margin = new MarginPadding(4);

        InternalChild = new Container
        {
            AutoSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 4,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0f, 0f, 0f, 0.6f) },
                label = new SpriteText
                {
                    Margin = new MarginPadding { Horizontal = 8, Vertical = 3 },
                    Font = FontUsage.Default.With(size: 16),
                    Colour = Color4.Yellow,
                },
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        reversed.BindTo(composer.ReverseAngleView);
        reversed.BindValueChanged(v => label.Text = v.NewValue ? "⇄ CW" : "⇄ CCW", true);
    }

    protected override bool OnClick(ClickEvent e)
    {
        composer.ReverseAngleView.Value = !composer.ReverseAngleView.Value;
        return true;
    }
}
