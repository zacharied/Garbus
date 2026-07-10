namespace Garbus.Game.Gameplay.Objects
{
    /// <summary>
    /// A bar line marking a measure boundary. Derived from timing and never serialized to a chart.
    /// </summary>
    public class BarLine : HitObject
    {
        /// <summary>The 1-based measure number this bar line begins.</summary>
        public int MeasureIndex { get; set; }
    }
}
