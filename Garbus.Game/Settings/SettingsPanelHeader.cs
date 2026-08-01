using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// The bar floating over the top of the settings panel: an icon button and a title, on an opaque
    /// background a shade lighter than the panel, casting a drop shadow onto the rows that scroll
    /// beneath it. One instance is shared by the settings view and the controls sub-view — call
    /// <see cref="ShowAs"/> to retarget it.
    /// </summary>
    public partial class SettingsPanelHeader : CompositeDrawable
    {
        /// <summary>
        /// The action button's <see cref="Drawable.Name"/>. Tests locate the button by this rather
        /// than by its glyph (cosmetic) or its type (which would mean widening visibility for tests).
        /// </summary>
        public const string ActionButtonName = "settings header action";

        private static readonly Color4 default_background_colour = new Color4(34, 34, 48, 255);

        private readonly Box background;
        private readonly SpriteText titleText;
        private readonly ActionButton actionButton;

        /// <summary>The title currently displayed.</summary>
        public LocalisableString Title => titleText.Text;

        public Color4 BackgroundColour
        {
            get => background.Colour;
            set => background.Colour = value;
        }

        public Color4 ShadowColour
        {
            get => EdgeEffect.Colour;
            set
            {
                var effect = EdgeEffect;
                effect.Colour = value;
                EdgeEffect = effect;
            }
        }

        public float ShadowRadius
        {
            get => EdgeEffect.Radius;
            set
            {
                var effect = EdgeEffect;
                effect.Radius = value;
                EdgeEffect = effect;
            }
        }

        public float ShadowOffsetY
        {
            get => EdgeEffect.Offset.Y;
            set
            {
                var effect = EdgeEffect;
                effect.Offset = new Vector2(0, value);
                EdgeEffect = effect;
            }
        }

        public SettingsPanelHeader()
        {
            RelativeSizeAxes = Axes.X;
            Height = 56;

            // EdgeEffect only renders on a masking drawable.
            Masking = true;
            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = new Color4(0, 0, 0, 140),
                Radius = 12,
                Offset = new Vector2(0, 3),
            };

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = default_background_colour,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Padding = new MarginPadding { Left = 20 },
                    Children = new Drawable[]
                    {
                        actionButton = new ActionButton
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        titleText = new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 28),
                            Colour = Color4.White,
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Points the header at a view: its title, and the icon and action of the button beside it.
        /// </summary>
        public void ShowAs(string title, IconUsage icon, Action onClick)
        {
            titleText.Text = title;
            actionButton.SetAction(icon, onClick);
        }

        // The icon button at the left of the header. Dismisses the overlay on the settings view and
        // returns from the sub-view on the controls view — whichever action ShowAs last handed it.
        private partial class ActionButton : CompositeDrawable
        {
            private readonly SpriteIcon icon;

            private Action? onClick;

            public ActionButton()
            {
                Name = ActionButtonName;

                Size = new Vector2(28);
                CornerRadius = 6;
                Masking = true;

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(60, 60, 78, 255) },
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(16),
                        Colour = Color4.White,
                    },
                };
            }

            public void SetAction(IconUsage newIcon, Action action)
            {
                icon.Icon = newIcon;
                onClick = action;
            }

            protected override bool OnClick(ClickEvent e)
            {
                onClick?.Invoke();
                return true;
            }
        }
    }
}
