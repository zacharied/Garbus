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
        private readonly ChartStore charts;
        private readonly ITrackStore trackStore;
        private readonly TextureStore? textures;

        public ResourceChartSource(ChartStore charts, ITrackStore trackStore, TextureStore? textures = null)
        {
            this.charts = charts;
            this.trackStore = trackStore;
            this.textures = textures;
        }

        public IEnumerable<ChartCard> Enumerate()
        {
            foreach (string name in charts.GetAvailableCharts())
            {
                // The bundled test chart is a developer fixture (PlayScreen's default / test harness),
                // not a real song. Keep it decodable via ChartStore but hide it from song select.
                if (string.Equals(name, PlayScreen.DEFAULT_CHART, StringComparison.OrdinalIgnoreCase))
                    continue;

                ChartCard? card = null;
                try
                {
                    var chart = charts.Get(name);
                    string? subfolder = Path.GetDirectoryName(name);
                    string groupKey = string.IsNullOrEmpty(subfolder) ? "res:" + name : "res:" + subfolder;

                    card = new ChartCard
                    {
                        Source = this,
                        Locator = name,
                        GroupKey = groupKey,
                        Title = chart.Metadata.Title,
                        Artist = chart.Metadata.Artist,
                        ChartName = chart.Metadata.ChartName,
                        Level = chart.Metadata.Level,
                        PreviewTime = chart.PreviewTime,
                        AudioFile = chart.Metadata.AudioFile,
                        BackgroundFile = chart.Metadata.BackgroundFile,
                    };
                }
                catch (Exception ex)
                {
                    Logger.Log($"Skipping unreadable bundled chart \"{name}\": {ex.Message}", level: LogLevel.Important);
                }

                if (card != null)
                    yield return card;
            }
        }

        public GarbusChart LoadChart(ChartCard card)
        {
            var chart = charts.Get(card.Locator);
            chart.ApplyDefaults();
            return chart;
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
