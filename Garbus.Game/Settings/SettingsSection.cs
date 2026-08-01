using System;
using Garbus.Game.UI;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// A labelled group of settings rows: an uppercase section title over a divider rule, then the
    /// rows themselves. Located by <see cref="Drawable.Name"/> (the title), never by layout position.
    /// </summary>
    public partial class SettingsSection : CompositeDrawable
    {
        private static readonly Color4 default_label_colour = new Color4(150, 150, 175, 255);
        private static readonly Color4 default_divider_colour = new Color4(90, 90, 115, 120);

        private readonly SpriteText label;
        private readonly Box divider;

        /// <summary>The section title's colour. Exposed so the tuning scene can drive it live.</summary>
        public Color4 LabelColour
        {
            get => label.Colour;
            set => label.Colour = value;
        }

        /// <summary>The divider rule's colour. Exposed so the tuning scene can drive it live.</summary>
        public Color4 DividerColour
        {
            get => divider.Colour;
            set => divider.Colour = value;
        }

        public SettingsSection(string title, params Drawable[] rows)
        {
            ArgumentNullException.ThrowIfNull(title);

            Name = title;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            // Front-first so an open dropdown menu on one row pops over the rows beneath it inside
            // this section — "Frame limiter" over "Screen mode", say. The section flow the sections
            // themselves live in is front-first for the same reason.
            var flow = new FrontFirstFillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 14),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 5),
                        Children = new Drawable[]
                        {
                            label = new SpriteText
                            {
                                Text = title.ToUpperInvariant(),
                                Font = FontUsage.Default.With(size: 14),
                                Colour = default_label_colour,
                            },
                            divider = new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 1,
                                Colour = default_divider_colour,
                            },
                        },
                    },
                },
            };

            foreach (var row in rows)
                flow.Add(row);

            InternalChild = flow;
        }
    }
}
