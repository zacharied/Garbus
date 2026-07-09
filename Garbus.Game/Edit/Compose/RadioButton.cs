// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Components/RadioButtons/RadioButton.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; verbatim otherwise (pure model class).

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A logical radio button: one member of an <see cref="EditorRadioButtonCollection"/>. Selecting one
    /// deselects its siblings (the collection wires that up).
    /// </summary>
    public class RadioButton
    {
        /// <summary>
        /// Whether this <see cref="RadioButton"/> is selected.
        /// Disable this bindable to disable the button.
        /// </summary>
        public readonly BindableBool Selected;

        /// <summary>
        /// The item related to this button.
        /// </summary>
        public string Label;

        /// <summary>
        /// A function which creates a drawable icon to represent this item. If null, a sane default should be used.
        /// </summary>
        public readonly Func<Drawable?>? CreateIcon;

        private readonly Action? action;

        public RadioButton(string label, Action? action, Func<Drawable?>? createIcon = null)
        {
            Label = label;
            CreateIcon = createIcon;
            this.action = action;
            Selected = new BindableBool();
        }

        /// <summary>
        /// Selects this <see cref="RadioButton"/>.
        /// </summary>
        public void Select()
        {
            if (!Selected.Value)
            {
                Selected.Value = true;
                action?.Invoke();
            }
        }

        /// <summary>
        /// Deselects this <see cref="RadioButton"/>.
        /// </summary>
        public void Deselect() => Selected.Value = false;

        // Tooltip text that will be shown when hovered over
        public LocalisableString TooltipText { get; set; } = string.Empty;
    }
}
