// Abstracts where playable charts come from (bundled resources vs an on-disk library folder).
// Enumeration + full load are pure; only track resolution needs the AudioManager.

using System.Collections.Generic;
using Garbus.Game.Charts;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;

namespace Garbus.Game.Screens.SongSelect
{
    public interface IChartSource
    {
        /// <summary>Decoded metadata for every chart this source exposes (broken files skipped).</summary>
        IEnumerable<ChartCard> Enumerate();

        /// <summary>Fully decodes the card's chart and applies defaults, ready for play.</summary>
        GarbusChart LoadChart(ChartCard card);

        /// <summary>A fresh <see cref="Track"/> for the card's audio. Caller owns/disposes it.</summary>
        Track GetTrack(ChartCard card, AudioManager audio);
    }
}
