// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Graphics/UserInterface/TernaryState*.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; collapsed StatefulMenuItem<T> +
// TernaryStateMenuItem + TernaryStateToggleMenuItem into one file using GarbusMenuItem as base
// (OsuMenuItem equivalent); Hotkey/Icon properties dropped; GetIconForState dropped (no OsuMenu
// renderer in Garbus — BasicContextMenuContainer renders plain items).

using System;
using osu.Framework.Bindables;
using osu.Framework.Localisation;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A <see cref="GarbusMenuItem"/> that holds a <see cref="TernaryState"/> and toggles it on click.
    /// </summary>
    public class TernaryStateToggleMenuItem : GarbusMenuItem
    {
        /// <summary>The current ternary state displayed by this item.</summary>
        public readonly Bindable<TernaryState> State = new Bindable<TernaryState>();

        public TernaryStateToggleMenuItem(LocalisableString text, MenuItemType type = MenuItemType.Standard, Action<TernaryState>? action = null)
            : base(text, type)
        {
            Action.Value = () =>
            {
                State.Value = getNextState(State.Value);
                action?.Invoke(State.Value);
            };
        }

        private static TernaryState getNextState(TernaryState state) => state switch
        {
            TernaryState.False => TernaryState.True,
            TernaryState.Indeterminate => TernaryState.True,
            TernaryState.True => TernaryState.False,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}
