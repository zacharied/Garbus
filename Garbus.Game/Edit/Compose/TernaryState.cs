// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Graphics/UserInterface/TernaryState.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; file moved here to avoid osu.Game dependency.

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// An on/off state with an extra indeterminate state.
    /// </summary>
    public enum TernaryState
    {
        /// <summary>
        /// The current state is false.
        /// </summary>
        False,

        /// <summary>
        /// The current state is a combination of <see cref="False"/> and <see cref="True"/>.
        /// The state becomes <see cref="True"/> if the <see cref="TernaryStateToggleMenuItem"/> is pressed.
        /// </summary>
        Indeterminate,

        /// <summary>
        /// The current state is true.
        /// </summary>
        True
    }
}
