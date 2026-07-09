// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Beatmaps/Timing/TimeSignature.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace + nullability only.

using System;

namespace Garbus.Game.Charts.Timing
{
    /// <summary>
    /// Stores the time signature of a track.
    /// For now, the lower numeral can only be 4; support for other denominators can be considered at a later date.
    /// </summary>
    public class TimeSignature : IEquatable<TimeSignature>
    {
        /// <summary>
        /// The numerator of a signature.
        /// </summary>
        public int Numerator { get; }

        public TimeSignature(int numerator)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numerator);

            Numerator = numerator;
        }

        public static TimeSignature SimpleTriple { get; } = new TimeSignature(3);
        public static TimeSignature SimpleQuadruple { get; } = new TimeSignature(4);

        public override string ToString() => $"{Numerator}/4";

        public bool Equals(TimeSignature? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return Numerator == other.Numerator;
        }

        public override bool Equals(object? obj) => obj is TimeSignature other && Equals(other);

        public override int GetHashCode() => Numerator;
    }
}
