// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/UI/Scrolling/ScrollingPlayfield.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespaces; DrawableHitObject XML doc reference updated to local namespace.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osuTK;

namespace Garbus.Game.Gameplay.UI.Scrolling
{
    /// <summary>
    /// A type of <see cref="Playfield"/> specialized towards scrolling hit objects.
    /// </summary>
    public abstract partial class ScrollingPlayfield : Playfield
    {
        protected readonly IBindable<ScrollingDirection> Direction = new Bindable<ScrollingDirection>();

        public new ScrollingHitObjectContainer HitObjectContainer => (ScrollingHitObjectContainer)base.HitObjectContainer;

        [Resolved]
        public IScrollingInfo ScrollingInfo { get; private set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Direction.BindTo(ScrollingInfo.Direction);
        }

        /// <summary>
        /// Given a position in screen space, return the time within this column.
        /// </summary>
        public virtual double TimeAtScreenSpacePosition(Vector2 screenSpacePosition) => HitObjectContainer.TimeAtScreenSpacePosition(screenSpacePosition);

        /// <summary>
        /// Given a time, return the screen space position within this column.
        /// </summary>
        public virtual Vector2 ScreenSpacePositionAtTime(double time) => HitObjectContainer.ScreenSpacePositionAtTime(time);

        protected sealed override HitObjectContainer CreateHitObjectContainer() => CreateScrollingHitObjectContainer();

        protected virtual ScrollingHitObjectContainer CreateScrollingHitObjectContainer() => new ScrollingHitObjectContainer();
    }
}
