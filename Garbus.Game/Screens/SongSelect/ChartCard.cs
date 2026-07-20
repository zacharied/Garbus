// One decoded chart entry in the song-select list: display metadata plus the locator its owning
// source uses to load the full chart and its audio.

using System;
using Garbus.Game.Charts;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;

namespace Garbus.Game.Screens.SongSelect
{
    public class ChartCard
    {
        /// <summary>The source that produced this card and can load its chart/track.</summary>
        public required IChartSource Source { get; init; }

        /// <summary>Source-specific handle (disk path or resource name).</summary>
        public required string Locator { get; init; }

        public string SongLocator { get; init; } = string.Empty;

        public Guid SongId { get; init; }

        public Guid ChartId { get; init; }

        /// <summary>Folder identity charts are grouped by (a song = one folder).</summary>
        public required string GroupKey { get; init; }

        public string Title { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public string ChartName { get; init; } = string.Empty;
        public int Level { get; init; }
        public double? PreviewTime { get; init; }
        public string AudioFile { get; init; } = string.Empty;

        /// <summary>The background image beside the chart (full filename); empty when none.</summary>
        public string BackgroundFile { get; init; } = string.Empty;

        /// <summary>Title, plus the chart (difficulty) name in brackets when present.</summary>
        public string DisplayName => string.IsNullOrEmpty(ChartName) ? Title : $"{Title} [{ChartName}]";

        public PlayableChart LoadChart() => Source.LoadChart(this);

        public Track GetTrack(AudioManager audio) => Source.GetTrack(this, audio);
    }
}
