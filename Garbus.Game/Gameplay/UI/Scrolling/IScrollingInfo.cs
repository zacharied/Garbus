// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/UI/Scrolling/IScrollingInfo.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace only; HitObject reference removed from XML doc (namespace change).

using osu.Framework.Bindables;

namespace Garbus.Game.Gameplay.UI.Scrolling
{
    public interface IScrollingInfo
    {
        /// <summary>
        /// The direction hit objects should scroll in.
        /// </summary>
        IBindable<ScrollingDirection> Direction { get; }

        /// <summary>
        /// The span of time that is visible by the length of the scrolling axes.
        /// </summary>
        IBindable<double> TimeRange { get; }

        /// <summary>
        /// The algorithm which controls hit object positions and sizes.
        /// </summary>
        IBindable<IScrollAlgorithm> Algorithm { get; }
    }
}
