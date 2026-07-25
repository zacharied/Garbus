// Regression guard for the song-select background leak behind the playtester crash (heavy slowdown →
// GPU device loss during deferred disposal). DirectoryChartSource used a plain atlased TextureStore
// per song directory: every browsed song stood up its own 1024x1024 atlas AND kept its oversized
// background resident for the life of the source, so a long browse accumulated GPU memory without
// bound. The fix serves backgrounds through a LargeTextureStore, which bypasses atlasing and frees
// each texture once the detail panel dereferences it. This runs headless (dummy renderer): it checks
// how backgrounds are loaded, not real VRAM, so no GPU memory is allocated.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using Garbus.Game.Screens.SongSelect;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSongSelectBackgroundLeak : GarbusTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private DirectoryChartSource source = null!;
        private string root = null!;

        private readonly List<bool> loaded = new List<bool>();

        [Test]
        public void TestBackgroundsLoadThroughNonAtlasingLargeTextureStore()
        {
            const int songs = 8;

            AddStep("seed library + browse every song", () =>
            {
                loaded.Clear();

                root = Directory.CreateTempSubdirectory("garbus-bgleak-").FullName;
                for (int i = 1; i <= songs; i++)
                    seedSong(i);

                source = new DirectoryChartSource(root, host);

                // Walk the real enumeration path and load each card's background exactly as
                // SongSelectScreen does when the selection moves onto a chart.
                foreach (var card in source.Enumerate().OrderBy(c => c.Level))
                    loaded.Add(source.GetBackground(card) != null);
            });

            // Functionality preserved: a real background texture loads for every song.
            AddAssert("a background loaded for every song", () => loaded.Count == songs && loaded.All(x => x));

            // The fix: backgrounds are served by LargeTextureStore, which bypasses atlasing (no
            // per-directory 1024x1024 atlas) and purges each texture once dereferenced — not the plain
            // TextureStore that leaked an atlas plus the oversized background per song, unbounded.
            AddAssert("every background store bypasses atlasing (LargeTextureStore)", () =>
            {
                var stores = backgroundStores();
                return stores.Count == songs && stores.All(s => s is LargeTextureStore);
            });

            AddStep("cleanup", () =>
            {
                source.Dispose();
                Directory.Delete(root, true);
            });
        }

        private void seedSong(int i)
        {
            string dir = Path.Combine(root, $"song-{i}");
            Directory.CreateDirectory(dir);

            // A distinct, valid background image per song so each load is a genuine texture. Real
            // library backgrounds are ~1000-1200px; size is irrelevant to how they are loaded here.
            using (var img = new Image<Rgba32>(16, 16))
                img.SaveAsPng(Path.Combine(dir, "bg.png"));

            var song = GarbusSong.CreateDefault();
            song.Metadata.Title = $"Song {i}";
            song.Metadata.Artist = "Test";
            song.Resources.Track = "song.ogg";
            song.Resources.Background = "bg.png";
            song.Charts[0].Metadata.Level = i;
            File.WriteAllText(Path.Combine(dir, "chart.garbus"), GarbusSongSerializer.Encode(song));
        }

        // The per-directory background stores live in a private cache with no public surface, so the
        // guard reads the field directly to assert which store type serves backgrounds.
        private IReadOnlyList<object> backgroundStores()
        {
            var field = typeof(DirectoryChartSource).GetField("textureStores", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return ((IDictionary)field.GetValue(source)!).Values.Cast<object>().ToList();
        }
    }
}
