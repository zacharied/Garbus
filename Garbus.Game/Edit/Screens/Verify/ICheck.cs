// Interface for a single verify-tab check.

using System.Collections.Generic;
using Garbus.Game.Charts;

namespace Garbus.Game.Edit.Screens.Verify
{
    /// <summary>Everything a check may inspect.</summary>
    public record CheckContext(GarbusChart Chart, ChartFile ChartFile, double TrackLength);

    /// <summary>A single verify-tab check. Stateless — called fresh each Refresh.</summary>
    public interface ICheck
    {
        /// <summary>Short display name shown in the issue table's CheckName column.</summary>
        string Name { get; }

        /// <summary>
        /// Runs the check and returns any issues found.
        /// The returned sequence is consumed immediately; deferred evaluation is fine.
        /// </summary>
        IEnumerable<Issue> Run(CheckContext context);
    }
}
