using System.Globalization;

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
        public const double DEFAULT_SPEED = 4;

        /// <summary>Slider snap step: scroll speed is adjustable in tenths.</summary>
        public const double PRECISION = 0.1;

        // TimeRange = BASELINE / speed, so speed 10 -> 700 ms, 20 -> 350 ms, 1 -> 7000 ms.
        private const double baseline = 7000.0;

        public static double ToTimeRange(double speed) => baseline / speed;

        /// <summary>Formats a scroll speed for display, always showing one decimal place (e.g. "4.0").</summary>
        public static string FormatSpeed(double speed) => speed.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
