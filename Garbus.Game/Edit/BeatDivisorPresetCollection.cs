// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/BeatDivisorPresetCollection.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace changed to Garbus.Game.Edit (was osu.Game.Screens.Edit.Compose.Components).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Garbus.Game.Edit
{
    public class BeatDivisorPresetCollection
    {
        public BeatDivisorType Type { get; }
        public IReadOnlyList<int> Presets { get; }

        private BeatDivisorPresetCollection(BeatDivisorType type, IEnumerable<int> presets)
        {
            Type = type;
            Presets = presets.ToArray();
        }

        public static readonly BeatDivisorPresetCollection COMMON = new BeatDivisorPresetCollection(BeatDivisorType.Common, new[] { 1, 2, 4, 8, 16 });

        public static readonly BeatDivisorPresetCollection TRIPLETS = new BeatDivisorPresetCollection(BeatDivisorType.Triplets, new[] { 1, 3, 6, 12 });

        public static BeatDivisorPresetCollection Custom(int maxDivisor)
        {
            var presets = new List<int>();

            for (int candidate = 1; candidate <= Math.Sqrt(maxDivisor); ++candidate)
            {
                if (maxDivisor % candidate != 0)
                    continue;

                presets.Add(candidate);
                presets.Add(maxDivisor / candidate);
            }

            return new BeatDivisorPresetCollection(BeatDivisorType.Custom, presets.Distinct().Order());
        }
    }
}
