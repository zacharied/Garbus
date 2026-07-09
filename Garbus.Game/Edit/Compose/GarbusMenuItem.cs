// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Graphics/UserInterface/OsuMenuItem.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: replaces OsuMenuItem; extends osu-framework MenuItem directly (no osu.Game
// dependency); Hotkey/Icon properties and OsuContextMenu wiring dropped — Garbus uses
// BasicContextMenuContainer which needs only framework MenuItem.

using System;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A <see cref="MenuItem"/> with an optional <see cref="MenuItemType"/> for visual categorisation.
    /// Replaces <c>OsuMenuItem</c> for Garbus (no osu.Game dependency required).
    /// </summary>
    public class GarbusMenuItem : MenuItem
    {
        public readonly MenuItemType Type;

        public GarbusMenuItem(LocalisableString text, MenuItemType type = MenuItemType.Standard)
            : this(text, type, null)
        {
        }

        public GarbusMenuItem(LocalisableString text, MenuItemType type, Action? action)
            : base(text, action)
        {
            Type = type;
        }
    }
}
