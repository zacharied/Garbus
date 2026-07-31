// Pins for SongFile.GetJacketTexture, the editor test-mode jacket source: null without a saved
// directory or a Resources.Background entry, and a real texture when a jacket file exists beside
// the saved song. Uses a generated png fixture (never real song content).

using System.IO;
using Garbus.Game.Charts;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Garbus.Game.Tests.Visual
{
    public partial class TestSceneSongFileJacket : GarbusTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Test]
        public void TestNullWithoutDirectory()
        {
            AddAssert("unsaved song has no jacket", () =>
            {
                var songFile = new SongFile(GarbusSong.CreateDefault());
                songFile.Song.Resources.Background = "jacket.png";
                return songFile.GetJacketTexture(host) == null;
            });
        }

        [Test]
        public void TestNullWithoutBackgroundResource()
        {
            AddAssert("song with no background resource has no jacket", () =>
            {
                string dir = storage.GetStorageForDirectory("jacket-test-nobg").GetFullPath(string.Empty);
                var songFile = new SongFile(GarbusSong.CreateDefault());
                songFile.Save(Path.Combine(dir, "song.garbus"));
                return songFile.GetJacketTexture(host) == null;
            });
        }

        [Test]
        public void TestLoadsJacketBesideSavedSong()
        {
            AddAssert("saved song with jacket file loads it", () =>
            {
                string dir = storage.GetStorageForDirectory("jacket-test").GetFullPath(string.Empty);

                using (var img = new Image<Rgba32>(4, 4))
                    img.SaveAsPng(Path.Combine(dir, "jacket.png"));

                var songFile = new SongFile(GarbusSong.CreateDefault());
                songFile.Song.Resources.Background = "jacket.png";
                songFile.Save(Path.Combine(dir, "song.garbus"));

                return songFile.GetJacketTexture(host) != null;
            });
        }
    }
}
