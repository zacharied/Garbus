using System.Linq;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// Whole-chart editor commands that live outside the vendored <see cref="EditorChart"/> so it
    /// stays close to its osu.Game source.
    /// </summary>
    public static class EditorChartCommands
    {
        /// <summary>
        /// Moves every hit object to its nearest snapped time for <paramref name="divisor"/>, as a
        /// single undoable transaction. Invoked from the editor's File menu ("Snap all notes").
        /// </summary>
        public static void SnapAllToDivisor(this EditorChart chart, int divisor)
        {
            chart.BeginChange();
            foreach (var h in chart.HitObjects.ToList())
            {
                h.StartTime = chart.ControlPointInfo.GetClosestSnappedTime(h.StartTime, divisor);
                chart.Update(h);
            }
            chart.EndChange();
        }
    }
}
