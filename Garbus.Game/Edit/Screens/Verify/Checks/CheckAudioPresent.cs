// Check: the chart references an audio file and the file exists on disk.

using System.Collections.Generic;
using System.IO;

namespace Garbus.Game.Edit.Screens.Verify.Checks
{
    /// <summary>
    /// Reports an issue when the song track is empty or the referenced file does not exist.
    ///
    /// Directory-null policy: when <see cref="Charts.ChartFile.Directory"/> is null (unsaved chart)
    /// we can only validate that the field is non-empty; we skip the on-disk existence check because
    /// there is no directory to resolve against. An unsaved chart with an empty AudioFile still
    /// produces an issue.
    /// </summary>
    public class CheckAudioPresent : ICheck
    {
        public string Name => "Audio Present";

        public IEnumerable<Issue> Run(CheckContext context)
        {
            string audioFile = context.Song.Resources.Track;

            if (string.IsNullOrWhiteSpace(audioFile))
            {
                yield return new Issue(null, "No audio file is set.", Name);
                yield break;
            }

            // Skip the disk check for unsaved (in-memory only) charts.
            if (context.SongFile.Directory == null)
                yield break;

            string fullPath = Path.Combine(context.SongFile.Directory, audioFile);

            if (!File.Exists(fullPath))
                yield return new Issue(null, $"Audio file '{audioFile}' not found in song directory.", Name);
        }
    }
}
