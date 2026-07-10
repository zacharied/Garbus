// Contract type for a single verify-tab finding.

namespace Garbus.Game.Edit.Screens.Verify
{
    /// <summary>
    /// A single finding from an <see cref="ICheck"/>.
    /// </summary>
    /// <param name="Time">The chart time this issue refers to, or null for a global issue (no seek target).</param>
    /// <param name="Message">Human-readable description of the problem.</param>
    /// <param name="CheckName">The <see cref="ICheck.Name"/> that emitted this issue.</param>
    public record Issue(double? Time, string Message, string CheckName);
}
