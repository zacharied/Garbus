// A song: the charts (difficulties) sharing one folder, with the group's display title/artist.

using System;
using System.Collections.Generic;

namespace Garbus.Game.Screens.SongSelect
{
    public class SongGroup
    {
        public string Title { get; }
        public string Artist { get; }
        public Guid SongId { get; }
        public string SongLocator { get; }
        public IReadOnlyList<ChartCard> Charts { get; }

        public SongGroup(string title, string artist, IReadOnlyList<ChartCard> charts, Guid songId = default, string songLocator = "")
        {
            Title = title;
            Artist = artist;
            SongId = songId;
            SongLocator = songLocator;
            Charts = charts;
        }
    }
}
