using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// A labelled slider row: name on the left, a live value readout on the right, and a slider
    /// bar bound to <c>current</c>.
    /// </summary>
    public partial class SettingsSlider : CompositeDrawable
    {
        private readonly Bindable<double> current;
        private readonly Func<double, string> format;
        private SpriteText valueText = null!;

        public SettingsSlider(string label, Bindable<double> current, Func<double, string> format)
        {
            this.current = current;
            this.format = format;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            new SpriteText
                            {
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Text = label,
                                Font = FontUsage.Default.With(size: 18),
                                Colour = Color4.White,
                            },
                            valueText = new SpriteText
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Font = FontUsage.Default.With(size: 14),
                                Colour = new Color4(180, 180, 200, 255),
                            },
                        },
                    },
                    new BasicSliderBar<double>
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 12,
                        Current = current,
                        BackgroundColour = new Color4(60, 60, 74, 255),
                        SelectionColour = new Color4(120, 160, 255, 255),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            current.BindValueChanged(v => valueText.Text = format(v.NewValue), true);
        }
    }
}
