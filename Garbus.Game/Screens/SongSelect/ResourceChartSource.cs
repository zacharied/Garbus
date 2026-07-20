// Song-select source over the bundled .garbus resources (read-only). Track audio for these charts
// lives in the game's Tracks/ resource namespace, resolved through the DI ITrackStore.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;

namespace Garbus.Game.Screens.SongSelect
{
    public class ResourceChartSource : IChartSource
    {
        private readonly SongStore songs;
        private readonly ITrackStore trackStore;
        private readonly TextureStore? textures;

        public ResourceChartSource(SongStore songs, ITrackStore trackStore, TextureStore? textures = null)
        {
            this.songs = songs;
            this.trackStore = trackStore;
            this.textures = textures;
        }

        public IEnumerable<ChartCard> Enumerate()
        {
            foreach (string name in songs.GetAvailableSongs())
            {
                // The bundled test chart is a developer fixture (PlayScreen's default / test harness),
                // not a real song. Keep it decodable via ChartStore but hide it from song select.
                if (string.Equals(name, PlayScreen.DEFAULT_CHART, StringComparison.OrdinalIgnoreCase))
                    continue;

                List<ChartCard> cards;
                try
                {
                    var song = songs.Get(name).Song;
                    cards = song.Charts.Select(chart => new ChartCard
                        {
                            Source = this,
                            Locator = $"{name}#{chart.ChartId}",
                            SongLocator = name,
                            SongId = song.SongId,
                            ChartId = chart.ChartId,
                            GroupKey = "res:" + name,
                            Title = song.Metadata.Title,
                            Artist = song.Metadata.Artist,
                            ChartName = chart.Metadata.ChartName,
                            Level = chart.Metadata.Level,
                            PreviewTime = song.PreviewTime,
                            AudioFile = song.Resources.Track,
                            BackgroundFile = song.Resources.Background,
                        }).ToList();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Skipping unreadable bundled song \"{name}\": {ex.Message}", level: LogLevel.Important);
                    continue;
                }

                foreach (var card in cards)
                    yield return card;
            }
        }

        public PlayableChart LoadChart(ChartCard card)
        {
            var song = songs.Get(card.SongLocator).Song;
            return song.CreatePlayableChart(card.ChartId);
        }

        public Track GetTrack(ChartCard card, AudioManager audio) => trackStore.Get(card.AudioFile);

        public Texture? GetBackground(ChartCard card)
        {
            if (textures == null || string.IsNullOrEmpty(card.BackgroundFile))
                return null;

            return textures.Get($"Jackets/{card.BackgroundFile}");
        }
    }
}
