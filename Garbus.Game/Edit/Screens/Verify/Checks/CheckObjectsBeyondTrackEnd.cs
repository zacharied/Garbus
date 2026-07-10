// Check: no hit object ends after the track ends.

using System.Collections.Generic;
using Garbus.Game.Gameplay.Objects;

namespace Garbus.Game.Edit.Screens.Verify.Checks
{
    /// <summary>
    /// Reports one issue per hit object whose end time exceeds <see cref="CheckContext.TrackLength"/>.
    /// End time is determined via <see cref="HitObjectExtensions.GetEndTime"/> — returns
    /// <see cref="Gameplay.Objects.Types.IHasDuration.EndTime"/> when the object implements it,
    /// otherwise falls back to <see cref="Gameplay.Objects.HitObject.StartTime"/>.
    /// </summary>
    public class CheckObjectsBeyondTrackEnd : ICheck
    {
        public string Name => "Objects Beyond Track End";

        public IEnumerable<Issue> Run(CheckContext context)
        {
            foreach (var obj in context.Chart.HitObjects)
            {
                double endTime = obj.GetEndTime();

                if (endTime > context.TrackLength)
                {
                    yield return new Issue(
                        obj.StartTime,
                        $"Object at {obj.StartTime:F0} ms ends at {endTime:F0} ms, after the track ends at {context.TrackLength:F0} ms.",
                        Name);
                }
            }
        }
    }
}
