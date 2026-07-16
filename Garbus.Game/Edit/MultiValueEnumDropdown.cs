using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// A dropdown over enum values with an optional transient "&lt;multiple&gt;" entry, shown when the
    /// selection holds differing values. Picking a real value invokes the supplied callback; the
    /// "&lt;multiple&gt;" entry is display-only and disappears once the caller re-aggregates.
    /// </summary>
    public partial class MultiValueEnumDropdown<T> : BasicDropdown<MultiValueEnumDropdown<T>.Choice> where T : struct, Enum
    {
        public const string MixedText = "<multiple>";

        public MultiValueEnumDropdown(MultiValue<T> state, Action<T> onChange)
        {
            var items = new List<Choice>();
            if (state.IsMixed)
                items.Add(Choice.Mixed);
            items.AddRange(Enum.GetValues<T>().Select(v => new Choice(v)));

            Items = items;
            Current.Value = state.IsMixed ? Choice.Mixed : new Choice(state.Value);

            // Bound AFTER the initial value is set, so only user selections fire the callback.
            Current.BindValueChanged(e =>
            {
                if (!e.NewValue.IsMixed)
                    onChange(e.NewValue.Value);
            });
        }

        protected override LocalisableString GenerateItemText(Choice item) => FormatChoice(item);

        internal static LocalisableString FormatChoice(Choice item)
            => item.IsMixed ? MixedText : item.Value.GetLocalisableDescription();

        /// <summary>
        /// A dropdown entry: either a concrete enum <see cref="Value"/> or the display-only
        /// "&lt;multiple&gt;" sentinel (<see cref="IsMixed"/>). A value type so the dropdown's item map
        /// can key on it without null-key issues.
        /// </summary>
        public readonly struct Choice : IEquatable<Choice>
        {
            public readonly bool IsMixed;
            public readonly T Value;

            public Choice(T value)
            {
                IsMixed = false;
                Value = value;
            }

            private Choice(bool isMixed)
            {
                IsMixed = isMixed;
                Value = default;
            }

            public static readonly Choice Mixed = new Choice(isMixed: true);

            public bool Equals(Choice other)
                => IsMixed == other.IsMixed && EqualityComparer<T>.Default.Equals(Value, other.Value);

            public override bool Equals(object? obj) => obj is Choice other && Equals(other);

            public override int GetHashCode() => IsMixed ? -1 : Value.GetHashCode();
        }
    }
}
