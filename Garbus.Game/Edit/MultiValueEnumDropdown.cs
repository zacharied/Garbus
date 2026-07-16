using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// A <see cref="BasicDropdown{T}"/> over a nullable enum where <c>null</c> is a transient
    /// "<multiple>" sentinel shown when the selection holds differing values. Picking a real value
    /// invokes the supplied callback; the sentinel disappears once the caller re-aggregates.
    /// </summary>
    public partial class MultiValueEnumDropdown<T> : BasicDropdown<T?> where T : struct, Enum
    {
        public const string MixedText = "<multiple>";

        private List<T?> allItems = null!;

        public MultiValueEnumDropdown(MultiValue<T> state, Action<T> onChange)
        {
            var items = new List<T?>();
            if (state.IsMixed)
                items.Add(null);
            items.AddRange(Enum.GetValues<T>().Select(v => (T?)v));

            allItems = items;
            // Only pass non-null items to the base implementation (dropdown menu can't use null as key)
            base.Items = items.Where(v => v.HasValue).ToList();

            Current.Value = state.IsMixed ? null : state.Value;

            // Bound AFTER the initial value is set, so only user selections fire the callback.
            Current.BindValueChanged(e =>
            {
                if (e.NewValue is T v)
                    onChange(v);
            });
        }

        // Expose the full items list including the null sentinel
        public new IReadOnlyList<T?> Items => allItems;

        protected override LocalisableString GenerateItemText(T? item)
            => item.HasValue ? base.GenerateItemText(item) : MixedText;
    }
}
