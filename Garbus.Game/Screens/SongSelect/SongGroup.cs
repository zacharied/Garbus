// A song: the charts (difficulties) sharing one folder, with the group's display title/artist.

using System.Collections.Generic;

namespace Garbus.Game.Screens.SongSelect
{
    public class SongGroup
    {
        public string Title { get; }
        public string Artist { get; }
        public IReadOnlyList<ChartCard> Charts { get; }

        public SongGroup(string title, string artist, IReadOnlyList<ChartCard> charts)
        {
            Title = title;
            Artist = artist;
            Charts = charts;
        }
    }
}
