// Validation of a slider path's node-time invariant, shared by every editor authoring path
// (placement, node drag, T-insert) so the rule for "horizontal line" arcs lives in one place.
//
// A slider path is the implicit head at time 0 followed by control points carrying a TimeOffset.
// A "zero-length link" (two consecutive nodes at the same time) renders as a constant-radius arc.
// The invariant: times are non-decreasing, at most one zero-length link may occur in a row (so an
// arc is a single collapsed segment, never a stack of three-plus nodes at one time), and — for a
// complete path — the total duration is > 0 (an all-zero path is invisible).

using System.Collections.Generic;

namespace Garbus.Game.Objects;

public static class GarbusSliderPath
{
    /// <summary>
    /// The ordering half of the invariant: control-point <paramref name="offsets"/> (the head at
    /// time 0 is implied) are non-decreasing and no two consecutive links are both zero-length.
    /// Used while a placement is still building up, where the duration is not yet &gt; 0.
    /// </summary>
    public static bool AreTimesOrdered(IReadOnlyList<double> offsets)
    {
        double previous = 0;           // the implicit head sits at time 0
        bool previousLinkZero = false; // there is no link leading into the head

        foreach (double offset in offsets)
        {
            if (offset < previous)
                return false; // times must not go backwards

            bool linkZero = offset == previous;

            if (linkZero && previousLinkZero)
                return false; // two zero-length links in a row = 3+ nodes at one time

            previousLinkZero = linkZero;
            previous = offset;
        }

        return true;
    }

    /// <summary>
    /// The full invariant for a complete path: <see cref="AreTimesOrdered"/> plus at least one control
    /// point (the head alone is not a path). The total duration MAY be 0 — a path collapsed entirely to
    /// the head's time is a constant-radius arc at a single instant. Used by node drag, T-insert, and
    /// placement commit.
    /// </summary>
    public static bool AreTimesValid(IReadOnlyList<double> offsets)
        => AreTimesOrdered(offsets) && offsets.Count > 0;
}
