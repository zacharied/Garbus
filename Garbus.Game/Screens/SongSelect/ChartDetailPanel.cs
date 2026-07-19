// The right-hand song-select detail panel: the selected chart's background image (or a placeholder
// square), title, artist, chart name + level, and a "Press X to play!" button. Display-only — it
// invokes LaunchRequested on click and holds no selection/launch logic of its own.

using System;
using Garbus.Game.Charts;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Screens.SongSelect
{
    public partial class ChartDetailPanel : CompositeDrawable
    {
        private const float image_size = 300;

        private readonly Box placeholder;
        private readonly Sprite image;
        private readonly SpriteText titleText;
        private readonly SpriteText artistText;
        private readonly SpriteText chartInfoText;
        private readonly BasicButton playButton;

        /// <summary>The currently displayed card, or null in the empty state. Exposed for tests.</summary>
        public ChartCard? DisplayedCard { get; private set; }

        /// <summary>Whether a non-null background texture is currently shown (vs the placeholder). Test hook.</summary>
        public bool HasBackground { get; private set; }

        /// <summary>Invoked when the play button is clicked. Set by the owning screen.</summary>
        public Action? LaunchRequested { get; set; }

        public ChartDetailPanel()
        {
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(24, 24, 34, 255) },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Padding = new MarginPadding { Top = 56, Horizontal = 24 },
                    Spacing = new Vector2(0, 14),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Size = new Vector2(image_size),
                            Masking = true,
                            CornerRadius = 6,
                            Children = new Drawable[]
                            {
                                placeholder = new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(45, 45, 60, 255) },
                                image = new Sprite
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    FillMode = FillMode.Fill,
                                    Alpha = 0,
                                },
                            },
                        },
                        titleText = new SpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = FontUsage.Default.With(size: 30, weight: "Bold"),
                            Colour = Color4.White,
                        },
                        artistText = new SpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = FontUsage.Default.With(size: 20),
                            Colour = new Color4(190, 190, 205, 255),
                        },
                        chartInfoText = new SpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = FontUsage.Default.With(size: 18),
                            Colour = new Color4(150, 160, 190, 255),
                        },
                        playButton = new BasicButton
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Size = new Vector2(image_size, 72),
                            Text = "Press X to play!",
                            BackgroundColour = new Color4(70, 90, 140, 255),
                            Action = () => LaunchRequested?.Invoke(),
                        },
                    },
                },
            };

            Show(null, null);
        }

        /// <summary>Populates every field for <paramref name="card"/> (empty state when null).</summary>
        public void Show(ChartCard? card, Texture? background)
        {
            DisplayedCard = card;

            if (card == null)
            {
                titleText.Text = "Select a chart";
                artistText.Text = string.Empty;
                chartInfoText.Text = string.Empty;
                playButton.Enabled.Value = false;
            }
            else
            {
                titleText.Text = card.Title;
                artistText.Text = card.Artist;
                chartInfoText.Text = formatChartInfo(card);
                playButton.Enabled.Value = true;
            }

            HasBackground = background != null;
            image.Texture = background;
            image.Alpha = background != null ? 1 : 0;
            placeholder.Alpha = background != null ? 0 : 1;
        }

        // "{ChartName} · Lv.{Level}", omitting whichever piece is absent.
        private static string formatChartInfo(ChartCard card)
        {
            bool hasName = !string.IsNullOrEmpty(card.ChartName);
            bool hasLevel = card.Level > 0;

            if (hasName && hasLevel)
                return $"{card.ChartName} · Lv.{card.Level}";
            if (hasName)
                return card.ChartName;
            if (hasLevel)
                return $"Lv.{card.Level}";

            return string.Empty;
        }
    }
}
