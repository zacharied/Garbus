// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Audio/HitSampleInfo.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: skin/beatmap sample lookup, suffix priority and editor bank flags removed — a
// sample resolves to a plain sample-store lookup under Samples/Gameplay ("{bank}-{name}", then "{name}").

using System;
using System.Collections.Generic;

namespace Garbus.Game.Gameplay.Audio
{
    /// <summary>
    /// Describes a gameplay hit sample.
    /// </summary>
    public class HitSampleInfo : IEquatable<HitSampleInfo>
    {
        public const string HIT_NORMAL = @"hitnormal";
        public const string HIT_WHISTLE = @"hitwhistle";
        public const string HIT_FINISH = @"hitfinish";
        public const string HIT_CLAP = @"hitclap";

        public const string BANK_NORMAL = @"normal";
        public const string BANK_SOFT = @"soft";
        public const string BANK_DRUM = @"drum";

        /// <summary>
        /// The name of the sample to load.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The bank to load the sample from.
        /// </summary>
        public readonly string Bank;

        /// <summary>
        /// The sample volume.
        /// </summary>
        public int Volume { get; }

        public HitSampleInfo(string name, string bank = BANK_NORMAL, int volume = 100)
        {
            Name = name;
            Bank = bank;
            Volume = volume;
        }

        /// <summary>
        /// Retrieve all possible sample-store lookups, returned in order of preference (highest first).
        /// </summary>
        public virtual IEnumerable<string> LookupNames
        {
            get
            {
                yield return $"Gameplay/{Bank}-{Name}";

                yield return $"Gameplay/{Name}";
            }
        }

        public virtual bool Equals(HitSampleInfo? other)
            => other != null && Name == other.Name && Bank == other.Bank;

        public override bool Equals(object? obj)
            => obj is HitSampleInfo other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Name, Bank);
    }
}
