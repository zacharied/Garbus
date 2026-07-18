namespace Garbus.Game.Settings
{
    /// <summary>
    /// Maps the user-facing "scroll speed" (higher = faster) onto the gameplay
    /// <see cref="Gameplay.UI.Scrolling.GarbusScrollingInfo.TimeRange"/> in milliseconds
    /// (higher = slower). Calibrated so speed 10 reproduces the historical 700 ms default.
    /// </summary>
    public static class ScrollSpeedMapping
    {
        public const double MIN_SPEED = 1;
        public const double MAX_SPEED = 20;
        public const double DEFAULT_SPEED = 10;

        // TimeRange = BASELINE / speed, so speed 10 -> 700 ms, 20 -> 350 ms, 1 -> 7000 ms.
        private const double baseline = 7000.0;

        public static double ToTimeRange(double speed) => baseline / speed;
    }
}
