// Check: the chart references a background file and the file exists on disk.

using System.Collections.Generic;
using System.IO;

namespace Garbus.Game.Edit.Screens.Verify.Checks
{
    /// <summary>
    /// Reports an issue when <see cref="Charts.ChartMetadata.BackgroundFile"/> is empty
    /// or the referenced file does not exist in the chart directory.
    ///
    /// Directory-null policy: same as <see cref="CheckAudioPresent"/> — disk existence is skipped
    /// for unsaved charts; an empty field still reports an issue.
    /// </summary>
    public class CheckBackgroundPresent : ICheck
    {
        public string Name => "Background Present";

        public IEnumerable<Issue> Run(CheckContext context)
        {
            string backgroundFile = context.Song.Resources.Background;

            if (string.IsNullOrWhiteSpace(backgroundFile))
            {
                yield return new Issue(null, "No background file is set.", Name);
                yield break;
            }

            // Skip the disk check for unsaved (in-memory only) charts.
            if (context.SongFile.Directory == null)
                yield break;

            string fullPath = Path.Combine(context.SongFile.Directory, backgroundFile);

            if (!File.Exists(fullPath))
                yield return new Issue(null, $"Background file '{backgroundFile}' not found in song directory.", Name);
        }
    }
}
