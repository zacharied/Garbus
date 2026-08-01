// Replaces osu.Game's IScrollingInfo/ScrollingTestContainer.TestScrollingInfo pair. Garbus's
// playfield is radial, so there is no scrolling direction — only the visible time range, the spawn
// halo an object appears on, and the algorithm mapping time to distance-from-centre.

using osu.Framework.Bindables;

namespace Garbus.Game.Gameplay.UI.Scrolling
{
    public class GarbusScrollingInfo
    {
        public const double DEFAULT_TIME_RANGE = 700;
        public const double DEFAULT_SPAWN_HALO_FRACTION = 0.12;
        public const double DEFAULT_SPAWN_DURATION = 125;

        /// <summary>
        /// Sets radial velocity: an object covers one playfield radius per <see cref="TimeRange"/>.
        /// </summary>
        public readonly BindableDouble TimeRange = new BindableDouble(DEFAULT_TIME_RANGE);

        /// <summary>
        /// The radius of the spawn halo objects appear on, as a fraction of the playfield radius.
        /// Dimensionless so the halo tracks the playfield through a resize.
        /// </summary>
        public readonly BindableDouble SpawnHaloFraction = new BindableDouble(DEFAULT_SPAWN_HALO_FRACTION);

        /// <summary>
        /// How long an object holds motionless on the halo — and how long its spawn animation runs.
        /// One quantity, so an object is never still growing while it moves.
        /// </summary>
        public readonly BindableDouble SpawnDuration = new BindableDouble(DEFAULT_SPAWN_DURATION);

        /// <summary>
        /// The algorithm which controls hit object positions and sizes.
        /// </summary>
        public readonly Bindable<IScrollAlgorithm> Algorithm = new Bindable<IScrollAlgorithm>(new ConstantScrollAlgorithm());

        /// <summary>How long an object spends travelling from the halo to the ring (ms).</summary>
        public double TravelTime => TimeRange.Value * (1 - SpawnHaloFraction.Value);

        /// <summary>How long before its StartTime an object appears on the halo (ms).</summary>
        public double LeadTime => TravelTime + SpawnDuration.Value;
    }
}
