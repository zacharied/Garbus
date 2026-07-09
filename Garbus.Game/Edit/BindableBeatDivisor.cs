// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/BindableBeatDivisor.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace changed to Garbus.Game.Edit; GetColourFor (OsuColour dependency) and
// GetSize (display helper) removed — call sites will use hardcoded colours; all numeric logic kept verbatim.

#nullable disable

using System.Linq;
using osu.Framework.Bindables;

namespace Garbus.Game.Edit
{
    public class BindableBeatDivisor : BindableInt
    {
        public static readonly int[] PREDEFINED_DIVISORS = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 16 };

        public const int MINIMUM_DIVISOR = 1;
        public const int MAXIMUM_DIVISOR = 64;

        public Bindable<BeatDivisorPresetCollection> ValidDivisors { get; } = new Bindable<BeatDivisorPresetCollection>(BeatDivisorPresetCollection.COMMON);

        public BindableBeatDivisor(int value = 1)
            : base(value)
        {
            ValidDivisors.BindValueChanged(_ => updateBindableProperties(), true);
            BindValueChanged(_ => ensureValidDivisor());
        }

        /// <summary>
        /// Set a divisor, updating the valid divisor range appropriately.
        /// </summary>
        /// <param name="divisor">The intended divisor.</param>
        /// <param name="preferKnownPresets">Forces changing the valid divisors to a known preset.</param>
        /// <returns>Whether the divisor was successfully set.</returns>
        public bool SetArbitraryDivisor(int divisor, bool preferKnownPresets = false)
        {
            if (divisor < MINIMUM_DIVISOR || divisor > MAXIMUM_DIVISOR)
                return false;

            // If the current valid divisor range doesn't contain the proposed value, attempt to find one which does.
            if (preferKnownPresets || !ValidDivisors.Value.Presets.Contains(divisor))
            {
                if (BeatDivisorPresetCollection.COMMON.Presets.Contains(divisor))
                    ValidDivisors.Value = BeatDivisorPresetCollection.COMMON;
                else if (BeatDivisorPresetCollection.TRIPLETS.Presets.Contains(divisor))
                    ValidDivisors.Value = BeatDivisorPresetCollection.TRIPLETS;
                else
                    ValidDivisors.Value = BeatDivisorPresetCollection.Custom(divisor);
            }

            Value = divisor;
            return true;
        }

        private void updateBindableProperties()
        {
            ensureValidDivisor();

            MinValue = ValidDivisors.Value.Presets.Min();
            MaxValue = ValidDivisors.Value.Presets.Max();
        }

        private void ensureValidDivisor()
        {
            if (!ValidDivisors.Value.Presets.Contains(Value))
                Value = 1;
        }

        public void SelectNext()
        {
            var presets = ValidDivisors.Value.Presets;
            if (presets.Cast<int?>().SkipWhile(preset => preset != Value).ElementAtOrDefault(1) is int newValue)
                Value = newValue;
        }

        public void SelectPrevious()
        {
            var presets = ValidDivisors.Value.Presets;
            if (presets.Cast<int?>().TakeWhile(preset => preset != Value).LastOrDefault() is int newValue)
                Value = newValue;
        }

        protected override int DefaultPrecision => 1;

        public override void BindTo(Bindable<int> them)
        {
            // bind to valid divisors first (if applicable) to ensure correct transfer of the actual divisor.
            if (them is BindableBeatDivisor otherBeatDivisor)
                ValidDivisors.BindTo(otherBeatDivisor.ValidDivisors);

            base.BindTo(them);
        }

        protected override Bindable<int> CreateInstance() => new BindableBeatDivisor();

        /// <summary>
        /// Retrieves the applicable divisor for a specific beat index.
        /// </summary>
        /// <param name="index">The 0-based beat index.</param>
        /// <param name="beatDivisor">The beat divisor.</param>
        /// <param name="validDivisors">The list of valid divisors which can be chosen from. Assumes ordered from low to high. Defaults to <see cref="PREDEFINED_DIVISORS"/> if omitted.</param>
        /// <returns>The applicable divisor.</returns>
        public static int GetDivisorForBeatIndex(int index, int beatDivisor, int[] validDivisors = null)
        {
            validDivisors ??= PREDEFINED_DIVISORS;

            int beat = index % beatDivisor;

            foreach (int divisor in validDivisors)
            {
                if ((beat * divisor) % beatDivisor == 0)
                    return divisor;
            }

            return 0;
        }
    }
}
