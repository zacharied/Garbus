using System;
using Garbus.Game.Edit.Compose;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// A tri-state checkbox for the inspector: <see cref="TernaryState.Indeterminate"/> (a dash) shows
    /// when the selection holds differing boolean values. Clicking a True box unchecks the selection;
    /// clicking a False or Indeterminate box checks it. Not yet wired to any hit-object property.
    /// </summary>
    public partial class MultiValueCheckbox : CompositeDrawable
    {
        public TernaryState State { get; }

        private readonly string label;
        private readonly Action<bool> onChange;

        private Box checkMark = null!;
        private Box dash = null!;

        public MultiValueCheckbox(string label, MultiValue<bool> state, Action<bool> onChange)
        {
            this.label = label;
            this.onChange = onChange;
            State = state.IsMixed ? TernaryState.Indeterminate
                : state.Value ? TernaryState.True : TernaryState.False;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        /// <summary>Click resolution: a checked box unchecks; anything else checks.</summary>
        internal static bool NextValue(TernaryState state) => state != TernaryState.True;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Children = new Drawable[]
                {
                    new Container
                    {
                        Size = new Vector2(16),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = new Colour4(40, 40, 48, 255) },
                            checkMark = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Scale = new Vector2(0.6f),
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Colour = Colour4.White,
                                Alpha = State == TernaryState.True ? 1 : 0,
                            },
                            dash = new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 3,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Colour = new Colour4(180, 180, 190, 255),
                                Alpha = State == TernaryState.Indeterminate ? 1 : 0,
                            },
                        },
                    },
                    new SpriteText
                    {
                        Text = label,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Font = FontUsage.Default.With(size: 12),
                        Colour = new Colour4(180, 180, 190, 255),
                    },
                },
            };
        }

        protected override bool OnClick(ClickEvent e)
        {
            onChange(NextValue(State));
            return true;
        }
    }
}
