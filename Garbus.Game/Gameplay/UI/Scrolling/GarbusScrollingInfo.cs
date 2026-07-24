// Replaces osu.Game's IScrollingInfo/ScrollingTestContainer.TestScrollingInfo pair. Garbus's
// playfield is radial, so there is no scrolling direction — only the visible time range and the
// algorithm mapping time to distance-from-centre.

using osu.Framework.Bindables;

namespace Garbus.Game.Gameplay.UI.Scrolling
{
    public class GarbusScrollingInfo
    {
        /// <summary>
        /// The span of time an object is visible for while travelling from the centre to the ring.
        /// </summary>
        public readonly BindableDouble TimeRange = new BindableDouble(700);

        /// <summary>
        /// The algorithm which controls hit object positions and sizes.
        /// </summary>
        public readonly Bindable<IScrollAlgorithm> Algorithm = new Bindable<IScrollAlgorithm>(new ConstantScrollAlgorithm());
    }
}
