using System;
using System.Collections.Generic;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// The aggregate of one parameter across a multi-object selection: either a single shared
    /// <see cref="Value"/> (all agree) or <see cref="IsMixed"/> when the targets disagree.
    /// </summary>
    public readonly struct MultiValue<T>
    {
        /// <summary>The selected targets hold differing values for this parameter.</summary>
        public readonly bool IsMixed;

        /// <summary>The shared value; meaningful only when <see cref="IsMixed"/> is false.</summary>
        public readonly T Value;

        public MultiValue(bool isMixed, T value)
        {
            IsMixed = isMixed;
            Value = value;
        }
    }

    public static class MultiValue
    {
        /// <summary>
        /// Collapses a per-object parameter to a single <see cref="MultiValue{T}"/>. Must not be called
        /// on an empty list — callers don't render a control for an empty selection.
        /// </summary>
        public static MultiValue<T> Aggregate<TObj, T>(IReadOnlyList<TObj> objs, Func<TObj, T> get)
        {
            var comparer = EqualityComparer<T>.Default;
            T first = get(objs[0]);

            for (int i = 1; i < objs.Count; i++)
            {
                if (!comparer.Equals(get(objs[i]), first))
                    return new MultiValue<T>(isMixed: true, first);
            }

            return new MultiValue<T>(isMixed: false, first);
        }
    }
}
