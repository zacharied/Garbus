// The editor's handle on a .garbus file at an arbitrary disk path, plus the directory its
// audio/background resources live in. Replaces the osu WorkingBeatmap concept for the editor — no
// realm, no BeatmapSetInfo, just a path and a decoded chart.

using System;
using System.IO;
using Garbus.Game.Charts.Format;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;

namespace Garbus.Game.Charts;

/// <summary>A .garbus chart on disk plus the directory its audio/background live in.</summary>
public class ChartFile : IDisposable
{
    /// <summary>The decoded chart model.</summary>
    public GarbusChart Chart { get; }

    /// <summary>Absolute path of the .garbus file, or null for a new unsaved chart.</summary>
    public string? FilePath { get; private set; }

    /// <summary>Directory containing the chart and its resources; null until first save.</summary>
    public string? Directory => FilePath == null ? null : Path.GetDirectoryName(FilePath);

    // Cached track store; invalidated when the chart is saved to a new path.
    private ITrackStore? trackStore;
    private string? trackStoreDirectory;

    /// <param name="chart">The chart model (may be brand-new or already decoded).</param>
    /// <param name="filePath">Absolute path of the backing .garbus file, or null for an unsaved chart.</param>
    public ChartFile(GarbusChart chart, string? filePath = null)
    {
        Chart = chart;
        FilePath = filePath;
    }

    /// <summary>
    /// Loads a chart from disk, decodes it, and applies defaults.
    /// </summary>
    /// <param name="path">Absolute path to the .garbus file.</param>
    public static ChartFile Load(string path)
    {
        string json = File.ReadAllText(path);
        var chart = GarbusChartSerializer.Decode(json);
        chart.ApplyDefaults();
        return new ChartFile(chart, path);
    }

    /// <summary>
    /// Encodes and writes the chart to <paramref name="path"/>, then updates <see cref="FilePath"/>.
    /// Any cached track store is dropped if the directory changes.
    /// </summary>
    public void Save(string path)
    {
        string json = GarbusChartSerializer.Encode(Chart);
        File.WriteAllText(path, json);

        // Invalidate cached track store if we're writing to a different directory.
        string? newDirectory = Path.GetDirectoryName(path);
        if (!string.Equals(newDirectory, trackStoreDirectory, StringComparison.OrdinalIgnoreCase))
        {
            (trackStore as IDisposable)?.Dispose();
            trackStore = null;
            trackStoreDirectory = null;
        }

        FilePath = path;
    }

    /// <summary>
    /// Encodes and writes the chart to <see cref="FilePath"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="FilePath"/> is null (chart has never been saved).</exception>
    public void Save()
    {
        if (FilePath == null)
            throw new InvalidOperationException("Cannot save: chart has no file path. Use Save(string path) to save to a new location.");

        Save(FilePath);
    }

    /// <summary>
    /// Copies an external file into the chart directory (overwrite allowed).
    /// </summary>
    /// <param name="sourcePath">Absolute path to the file to import.</param>
    /// <returns>The bare filename of the imported resource (e.g. <c>"song.ogg"</c>).</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Directory"/> is null (chart has not yet been saved).</exception>
    public string ImportResource(string sourcePath)
    {
        if (Directory == null)
            throw new InvalidOperationException("Cannot import resource: chart has no directory. Save the chart first.");

        string fileName = Path.GetFileName(sourcePath);
        string dest = Path.Combine(Directory, fileName);

        // Guard against copying a file onto itself (e.g., when the source is already in the chart directory).
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
            File.Copy(sourcePath, dest, overwrite: true);

        return fileName;
    }

    /// <summary>
    /// Returns a track store rooted at the chart directory, or null before the first save.
    /// The store is created lazily and invalidated when the chart is saved to a new path.
    /// </summary>
    /// <remarks>
    /// Track lookups must include the full filename with extension — <see cref="ITrackStore"/>
    /// only probes <c>.mp3</c> for extension-less lookups.
    /// </remarks>
    public ITrackStore? GetTrackStore(AudioManager audio)
    {
        if (Directory == null)
            return null;

        // Return cached store if the directory hasn't changed.
        if (trackStore != null && string.Equals(trackStoreDirectory, Directory, StringComparison.OrdinalIgnoreCase))
            return trackStore;

        trackStore = audio.GetTrackStore(new StorageBackedResourceStore(new NativeStorage(Directory)));
        trackStoreDirectory = Directory;
        return trackStore;
    }

    /// <summary>
    /// Opens the raw encoded-audio file for the chart's <see cref="ChartMetadata.AudioFile"/>, or
    /// null when the chart is unsaved, has no audio file set, or the file is absent from disk.
    /// The caller owns the returned stream and must dispose it (a <c>Waveform</c> takes ownership).
    /// </summary>
    public Stream? GetAudioStream()
    {
        if (Directory == null || string.IsNullOrEmpty(Chart.Metadata.AudioFile))
            return null;

        return new StorageBackedResourceStore(new NativeStorage(Directory)).GetStream(Chart.Metadata.AudioFile);
    }

    /// <summary>
    /// Disposes the cached track store so the AudioManager can release it promptly.
    /// The underlying <see cref="TrackStore"/> is an <see cref="AudioCollectionManager{T}"/> child;
    /// disposing it sets <c>IsAlive = false</c> and the parent manager removes it on the next update.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;

        (trackStore as IDisposable)?.Dispose();
        trackStore = null;
        trackStoreDirectory = null;
        IsDisposed = true;
    }

    /// <summary>
    /// Whether <see cref="Dispose"/> has been called. Exposed as a test seam so that editor
    /// teardown tests can assert that the ChartFile is cleaned up when the editor exits.
    /// </summary>
    public bool IsDisposed { get; private set; }
}
