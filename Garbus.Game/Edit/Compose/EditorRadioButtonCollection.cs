// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Components/RadioButtons/EditorRadioButtonCollection.cs
// + EditorRadioButton.cs (merged into one file).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; EditorRadioButton derives from BasicButton
// (osu-framework) instead of osu.Game's OsuButton; OverlayColourProvider/OsuColour swapped for hardcoded
// Colour4 values; OsuSpriteText/SpriteIcon/Circle icon swapped for framework primitives; tooltip kept.
// The one-selected-at-a-time radio semantics (Select()/Deselect() wiring) are preserved verbatim.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osuTK;

namespace Garbus.Game.Edit.Compose
{
    public partial class EditorRadioButtonCollection : CompositeDrawable
    {
        private IReadOnlyList<RadioButton> items = Array.Empty<RadioButton>();

        public IReadOnlyList<RadioButton> Items
        {
            get => items;
            set
            {
                if (ReferenceEquals(items, value))
                    return;

                items = value;

                buttonContainer.Clear();
                items.ForEach(addButton);
            }
        }

        private readonly FlowContainer<EditorRadioButton> buttonContainer;

        public EditorRadioButtonCollection()
        {
            AutoSizeAxes = Axes.Y;

            InternalChild = buttonContainer = new FillFlowContainer<EditorRadioButton>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5)
            };
        }

        private RadioButton? currentlySelected;

        private void addButton(RadioButton button)
        {
            button.Selected.ValueChanged += selected =>
            {
                if (selected.NewValue)
                {
                    currentlySelected?.Deselect();
                    currentlySelected = button;
                }
                else
                    currentlySelected = null;
            };

            buttonContainer.Add(new EditorRadioButton(button));
        }
    }

    public partial class EditorRadioButton : BasicButton, IHasTooltip
    {
        public readonly RadioButton Button;

        private static readonly Colour4 default_background_colour = new Colour4(60, 60, 70, 255);
        private static readonly Colour4 selected_background_colour = new Colour4(120, 120, 150, 255);

        private Drawable icon = null!;

        public EditorRadioButton(RadioButton button)
        {
            Button = button;

            Text = button.Label;
            Action = button.Select;

            RelativeSizeAxes = Axes.X;
            Height = 40;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Add(icon = (Button.CreateIcon?.Invoke() ?? new Circle()).With(b =>
            {
                b.Blending = BlendingParameters.Additive;
                b.Anchor = Anchor.CentreLeft;
                b.Origin = Anchor.CentreLeft;
                b.Size = new Vector2(20);
                b.X = 10;
            }));

            Button.Selected.ValueChanged += _ => updateSelectionState();
            Button.Selected.BindDisabledChanged(disabled => Enabled.Value = !disabled, true);
            updateSelectionState();
        }

        private void updateSelectionState()
        {
            if (!IsLoaded)
                return;

            BackgroundColour = Button.Selected.Value ? selected_background_colour : default_background_colour;
            icon.Colour = Button.Selected.Value ? Colour4.White : Colour4.White.Darken(0.5f);
        }

        protected override SpriteText CreateText() => new SpriteText
        {
            Depth = -1,
            Origin = Anchor.CentreLeft,
            Anchor = Anchor.CentreLeft,
            X = 40f
        };

        public LocalisableString TooltipText => Button.TooltipText;
    }
}
