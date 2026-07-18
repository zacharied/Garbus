using System;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// Perceptual (audio-taper) mapping between a linear slider position (0..1) and actual audio
    /// gain (0..1). A linear slider crams the usable loudness range into the bottom few percent;
    /// this power curve stretches the quiet end out so most of the travel lands in useful territory.
    /// Calibrated so slider position 0.30 outputs ~0.03 gain — i.e. what used to require setting the
    /// raw volume to 3% now sits at slider 30%. Endpoints are preserved: 0→0 and 1→1.
    /// </summary>
    public static class VolumeCurve
    {
        // gain = position^EXPONENT. EXPONENT = ln(0.03)/ln(0.30) ≈ 2.9, so 0.30^2.9 ≈ 0.0305.
        public const double EXPONENT = 2.9;

        /// <summary>Actual audio gain for a given linear slider position.</summary>
        public static double ToGain(double position) => position <= 0 ? 0 : Math.Pow(position, EXPONENT);

        /// <summary>The slider position that produces a given audio gain (inverse of <see cref="ToGain"/>).</summary>
        public static double ToPosition(double gain) => gain <= 0 ? 0 : Math.Pow(gain, 1 / EXPONENT);
    }
}
