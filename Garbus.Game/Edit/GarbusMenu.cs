// Bespoke for Garbus (modeled on osu.Game's OsuMenu / ToggleMenuItem / DrawableStatefulMenuItem,
// rebuilt on the framework's Basic* widgets).

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;

namespace Garbus.Game.Edit;

/// <summary>
/// A non-interactive <see cref="MenuItem"/> rendered as a thin divider line by <see cref="GarbusMenu"/>
/// (modeled on osu's <c>OsuMenuItemSpacer</c>). Groups related items within a dropdown.
/// </summary>
public class GarbusMenuSpacer : MenuItem
{
    public GarbusMenuSpacer()
        : base(" ")
    {
    }
}

/// <summary>A <see cref="MenuItem"/> carrying an on/off state, rendered with a checkbox by <see cref="GarbusMenu"/>.</summary>
public class ToggleMenuItem : MenuItem
{
    public readonly BindableBool State = new BindableBool();

    /// <summary>
    /// Strong reference to the bindable passed in. <see cref="Bindable{T}.BindTo"/> links only weakly in
    /// both directions, and <c>ConfigManager.GetBindable</c> hands out a bound copy the config itself
    /// holds only weakly — so without an owning reference here that copy becomes GC-eligible the moment
    /// the caller's local goes out of scope. Once collected the item still flips <see cref="State"/> (and
    /// its checkbox) but the change reaches nothing, making the toggle look inert.
    /// </summary>
    private readonly Bindable<bool> source;

    /// <param name="text">The displayed text.</param>
    /// <param name="state">The bindable this item's state follows and toggles (e.g. a config bindable).</param>
    public ToggleMenuItem(LocalisableString text, Bindable<bool> state)
        : base(text)
    {
        source = state;
        State.BindTo(source);
        Action.Value = () => State.Value = !State.Value;
    }
}

/// <summary>
/// <see cref="BasicMenu"/> that renders <see cref="ToggleMenuItem"/>s with a checkbox. Clicking a
/// toggle flips it and keeps the menu open (matching osu), so several settings can be changed in one
/// visit.
/// </summary>
public partial class GarbusMenu : BasicMenu
{
    public GarbusMenu(Direction direction, bool topLevelMenu = false)
        : base(direction, topLevelMenu)
    {
    }

    protected override Menu CreateSubMenu() => new GarbusMenu(Direction.Vertical)
    {
        Anchor = Direction == Direction.Horizontal ? Anchor.BottomLeft : Anchor.TopRight,
    };

    protected override DrawableMenuItem CreateDrawableMenuItem(MenuItem item)
    {
        switch (item)
        {
            case GarbusMenuSpacer spacer:
                return new DrawableGarbusMenuSpacer(spacer);

            case ToggleMenuItem toggle:
                return new DrawableToggleMenuItem(toggle);

            default:
                return base.CreateDrawableMenuItem(item);
        }
    }

    /// <summary>Renders a <see cref="GarbusMenuSpacer"/> as a thin divider that swallows hover/click.</summary>
    internal partial class DrawableGarbusMenuSpacer : BasicDrawableMenuItem
    {
        public DrawableGarbusMenuSpacer(GarbusMenuSpacer item)
            : base(item)
        {
            // Shrink the vertical footprint so the divider reads as a gap rather than a full row.
            Scale = new Vector2(1, 0.6f);
            BackgroundColour = Colour4.Transparent;
            BackgroundColourHover = Colour4.Transparent;

            AddInternal(new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.X,
                Width = 0.9f,
                Height = 1.5f,
                Colour = Colour4.White,
                Alpha = 0.25f,
            });
        }

        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnClick(ClickEvent e) => true;
    }

    internal partial class DrawableToggleMenuItem : BasicDrawableMenuItem
    {
        // Keep the menu open so several settings can be flipped in one visit (matches osu).
        public override bool CloseMenuOnClick => false;

        public DrawableToggleMenuItem(ToggleMenuItem item)
            : base(item)
        {
            ((ToggleContent)Content).State.BindTo(item.State);
        }

        protected override Drawable CreateContent() => new ToggleContent();

        private partial class ToggleContent : FillFlowContainer, IHasText
        {
            public readonly BindableBool State = new BindableBool();

            private readonly SpriteText text;
            private readonly Box check;

            public LocalisableString Text
            {
                get => text.Text;
                set => text.Text = value;
            }

            public ToggleContent()
            {
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Horizontal;
                Spacing = new Vector2(4, 0);
                Padding = new MarginPadding(2);

                Children = new Drawable[]
                {
                    new Container
                    {
                        Size = new Vector2(12),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Children = new Drawable[]
                        {
                            // the "empty checkbox" backdrop, always visible.
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Colour4.White,
                                Alpha = 0.25f,
                            },
                            check = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Colour4.White,
                                Alpha = 0,
                            },
                        },
                    },
                    text = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Font = FrameworkFont.Condensed,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                State.BindValueChanged(e => check.FadeTo(e.NewValue ? 1 : 0, 100), true);
            }
        }
    }
}
