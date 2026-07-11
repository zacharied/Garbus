// Bespoke for Garbus. Shared divisor→colour/height palette used by the timeline tick display and
// the compose beat-divisor control. Hardcoded colours (Garbus drops osu.Game's OsuColour).
using osuTK.Graphics;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// Palette keyed on the applicable beat divisor. Bar lines / whole beats are white; finer
    /// subdivisions cycle through a fixed set of colours and shrink in height.
    /// </summary>
    public static class BeatDivisorColours
    {
        /// <summary>Colour for a beat divisor (0 = bar line, treated as white).</summary>
        public static Color4 ColourFor(int divisor) => divisor switch
        {
            0 => Color4.White,          // bar lines
            1 => Color4.White,
            2 => new Color4(220, 100, 100, 255),   // red family
            3 => new Color4(100, 200, 100, 255),   // green
            4 => new Color4(100, 140, 220, 255),   // blue
            6 => new Color4(220, 160, 80, 255),    // orange
            8 => new Color4(160, 100, 220, 255),   // purple
            _ => new Color4(180, 180, 180, 255),   // grey for unusual divisors
        };

        /// <summary>Relative tick height (0..1) for a beat divisor.</summary>
        public static float HeightFor(int divisor) => divisor switch
        {
            1 => 1.0f,
            2 => 0.7f,
            3 => 0.6f,
            4 => 0.5f,
            6 => 0.45f,
            8 => 0.4f,
            _ => 0.35f,
        };
    }
}
