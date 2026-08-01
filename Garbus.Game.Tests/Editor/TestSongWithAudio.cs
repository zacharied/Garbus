// Shared fixture for editor scenes that need real decodable audio on disk (anything exercising a
// WaveformGraph). The bundled test track is copied into a scratch song directory and the returned
// SongFile is rooted there, so SongFile.GetAudioStream/GetTrackStore both resolve.

using System.IO;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Resources;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;

namespace Garbus.Game.Tests.Editor
{
    internal static class TestSongWithAudio
    {
        /// <summary>
        /// Seeds <paramref name="directoryName"/> under the test storage with the bundled
        /// <c>test-track.ogg</c> and returns a <see cref="SongFile"/> whose single chart uses shared
        /// timing with one point at time 0.
        /// </summary>
        public static SongFile Create(Storage storage, string directoryName, double beatLength = 500)
        {
            string directory = storage.GetStorageForDirectory(directoryName).GetFullPath(string.Empty);

            string audioPath = Path.Combine(directory, "test-track.ogg");

            if (!File.Exists(audioPath))
            {
                using var resources = new DllResourceStore(typeof(GarbusResources).Assembly);
                File.WriteAllBytes(audioPath, resources.Get("Tracks/test-track.ogg"));
            }

            var song = GarbusSong.CreateDefault();
            song.Resources.Track = "test-track.ogg";
            song.ControlPointInfo!.Clear();
            song.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = beatLength });

            return new SongFile(song, Path.Combine(directory, "song.garbus"));
        }
    }
}
