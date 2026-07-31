using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// A labelled dropdown row: name on top, a dropdown over the enum <typeparamref name="T"/> below,
    /// bound to <c>current</c>. Item text uses each value's <c>[Description]</c> when it has one.
    /// Pass <c>items</c> to offer a subset of the enum rather than all of it — the window modes the
    /// running platform actually supports, say.
    /// </summary>
    public partial class SettingsEnumDropdown<T> : CompositeDrawable
        where T : struct, Enum
    {
        public SettingsEnumDropdown(string label, Bindable<T> current, IEnumerable<T>? items = null)
        {
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
                    new SpriteText
                    {
                        Text = label,
                        Font = FontUsage.Default.With(size: 18),
                        Colour = Color4.White,
                    },
                    new DescriptionDropdown
                    {
                        RelativeSizeAxes = Axes.X,
                        Items = items ?? Enum.GetValues<T>(),
                        Current = current,
                    },
                },
            };
        }

        // A BasicDropdown whose item text is the enum value's [Description] (falling back to its name).
        private partial class DescriptionDropdown : BasicDropdown<T>
        {
            public DescriptionDropdown()
            {
                // The open menu pops over whatever sits below the row (combo-box style) instead of
                // growing the row and reflowing the panel. Rows below must be drawn behind this one
                // for the menu to be visible — SettingsOverlay reverses its row depths to that end.
                Menu.BypassAutoSizeAxes = Axes.Y;
            }

            protected override LocalisableString GenerateItemText(T item) => item.GetLocalisableDescription();
        }
    }
}
