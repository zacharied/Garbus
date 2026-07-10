// Check: no hit object starts before time zero.

using System.Collections.Generic;

namespace Garbus.Game.Edit.Screens.Verify.Checks
{
    /// <summary>
    /// Reports one issue per hit object whose <see cref="Gameplay.Objects.HitObject.StartTime"/>
    /// is negative. This can happen via timeline edge-placement or drag in the editor.
    /// </summary>
    public class CheckObjectsBeforeTimeZero : ICheck
    {
        public string Name => "Objects Before Time Zero";

        public IEnumerable<Issue> Run(CheckContext context)
        {
            foreach (var obj in context.Chart.HitObjects)
            {
                if (obj.StartTime < 0)
                {
                    yield return new Issue(
                        obj.StartTime,
                        $"Object starts at {obj.StartTime:F0} ms, before the beginning of the chart.",
                        Name);
                }
            }
        }
    }
}
