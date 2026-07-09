// Modeled on osu.Game (https://github.com/ppy/osu) — the IScrollingInfo that
// osu.Game/Rulesets/UI/Scrolling/DrawableScrollingRuleset.cs caches for its scrolling playfield.
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Modeled on: Garbus has no DrawableRuleset, so the editor composer owns and caches the IScrollingInfo
// that its ScrollingPlayfield/ScrollingHitObjectContainer resolve. This mirrors the DrawableScrollingRuleset
// LocalScrollingInfo: three bindables (direction/time-range/algorithm) with a constant scroll algorithm.

using osu.Framework.Bindables;
using Garbus.Game.Gameplay.UI.Scrolling;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// The composer-owned <see cref="IScrollingInfo"/> that drives the editor's scrolling playfield.
    /// The <see cref="ScrollingHitObjectComposer{T}"/> writes <see cref="Direction"/> and pipes its
    /// <c>TimelineTimeRange</c> into <see cref="TimeRange"/>.
    /// </summary>
    public class EditorScrollingInfo : IScrollingInfo
    {
        public readonly Bindable<ScrollingDirection> DirectionBindable = new Bindable<ScrollingDirection>(ScrollingDirection.Down);
        public readonly BindableDouble TimeRangeBindable = new BindableDouble(3000)
        {
            MinValue = 100,
            MaxValue = 20000,
        };
        public readonly Bindable<IScrollAlgorithm> AlgorithmBindable = new Bindable<IScrollAlgorithm>(new ConstantScrollAlgorithm());

        public IBindable<ScrollingDirection> Direction => DirectionBindable;
        public IBindable<double> TimeRange => TimeRangeBindable;
        public IBindable<IScrollAlgorithm> Algorithm => AlgorithmBindable;
    }
}
