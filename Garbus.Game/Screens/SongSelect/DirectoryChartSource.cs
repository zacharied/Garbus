// Song-select source over the on-disk library folder (%APPDATA%/Garbus/charts). Recursively finds
// .garbus files; each folder is one song. Track audio sits beside each chart, resolved through a
// per-directory track store (cached, disposed with the source) exactly as ChartFile does.

using System;
using System.Collections.Generic;
using System.IO;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace Garbus.Game.Screens.SongSelect
{
    public class DirectoryChartSource : IChartSource, IDisposable
    {
        private readonly string rootDirectory;
        private readonly GameHost? host;
        private readonly Dictionary<string, ITrackStore> trackStores = new Dictionary<string, ITrackStore>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TextureStore> textureStores = new Dictionary<string, TextureStore>(StringComparer.OrdinalIgnoreCase);

        public DirectoryChartSource(string rootDirectory, GameHost? host = null)
        {
            this.rootDirectory = rootDirectory;
            this.host = host;
        }

        public IEnumerable<ChartCard> Enumerate()
        {
            if (!Directory.Exists(rootDirectory))
                yield break;

            foreach (string path in Directory.EnumerateFiles(rootDirectory, "*.garbus", SearchOption.AllDirectories))
            {
                ChartCard? card = null;
                try
                {
                    var chart = GarbusChartSerializer.Decode(File.ReadAllText(path));
                    card = new ChartCard
                    {
                        Source = this,
                        Locator = path,
                        GroupKey = Path.GetDirectoryName(path)!,
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
                    Logger.Log($"Skipping unreadable chart \"{path}\": {ex.Message}", level: LogLevel.Important);
                }

                if (card != null)
                    yield return card;
            }
        }

        public GarbusChart LoadChart(ChartCard card)
        {
            var chart = GarbusChartSerializer.Decode(File.ReadAllText(card.Locator));
            chart.ApplyDefaults();
            return chart;
        }

        public Track GetTrack(ChartCard card, AudioManager audio)
        {
            string dir = Path.GetDirectoryName(card.Locator)!;

            if (!trackStores.TryGetValue(dir, out var store))
            {
                store = audio.GetTrackStore(new StorageBackedResourceStore(new NativeStorage(dir)));
                trackStores[dir] = store;
            }

            return store.Get(card.AudioFile);
        }

        public Texture? GetBackground(ChartCard card)
        {
            if (host == null || string.IsNullOrEmpty(card.BackgroundFile))
                return null;

            string dir = Path.GetDirectoryName(card.Locator)!;

            if (!textureStores.TryGetValue(dir, out var store))
            {
                store = new TextureStore(host.Renderer, host.CreateTextureLoaderStore(new StorageBackedResourceStore(new NativeStorage(dir))));
                textureStores[dir] = store;
            }

            return store.Get(card.BackgroundFile);
        }

        public void Dispose()
        {
            foreach (var store in trackStores.Values)
                (store as IDisposable)?.Dispose();
            trackStores.Clear();

            foreach (var store in textureStores.Values)
                store.Dispose();
            textureStores.Clear();
        }
    }
}
