using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Garbus.Game.Charts.Format;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;

namespace Garbus.Game.Charts;

/// <summary>A complete song on disk plus the directory containing its shared resources.</summary>
public class SongFile : IDisposable
{
    public GarbusSong Song { get; }

    public string? FilePath { get; private set; }

    public string? Directory => FilePath == null ? null : Path.GetDirectoryName(FilePath);

    public bool NeedsVersionUpgrade { get; private set; }

    private ITrackStore? trackStore;
    private string? trackStoreDirectory;

    private LargeTextureStore? jacketStore;
    private string? jacketStoreDirectory;

    public SongFile(GarbusSong song, string? filePath = null, bool needsVersionUpgrade = false)
    {
        Song = song;
        FilePath = filePath;
        NeedsVersionUpgrade = needsVersionUpgrade;
    }

    public static SongFile Load(string path)
    {
        SongDecodeResult decoded = GarbusSongSerializer.Decode(File.ReadAllBytes(path));
        return new SongFile(decoded.Song, path, decoded.WasConvertedFromVersion1);
    }

    public void Save(string path)
    {
        string absolutePath = Path.GetFullPath(path);
        string destinationDirectory = Path.GetDirectoryName(absolutePath)
                                      ?? throw new InvalidOperationException("Song path has no directory.");
        System.IO.Directory.CreateDirectory(destinationDirectory);

        string json = GarbusSongSerializer.Encode(Song);
        string tempPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        string? oldDirectory = Directory;
        bool changingDirectory = oldDirectory != null
                                 && !string.Equals(Path.GetFullPath(oldDirectory), Path.GetFullPath(destinationDirectory), StringComparison.OrdinalIgnoreCase);

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (changingDirectory)
                copyResourcesForSaveAs(oldDirectory!, destinationDirectory);

            File.Move(tempPath, absolutePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        if (!string.Equals(destinationDirectory, trackStoreDirectory, StringComparison.OrdinalIgnoreCase))
        {
            (trackStore as IDisposable)?.Dispose();
            trackStore = null;
            trackStoreDirectory = null;
        }

        if (!string.Equals(destinationDirectory, jacketStoreDirectory, StringComparison.OrdinalIgnoreCase))
        {
            jacketStore?.Dispose();
            jacketStore = null;
            jacketStoreDirectory = null;
        }

        FilePath = absolutePath;
        NeedsVersionUpgrade = false;
    }

    public void Save()
    {
        if (FilePath == null)
            throw new InvalidOperationException("Cannot save: song has no file path. Use Save(string path) first.");
        Save(FilePath);
    }

    public string ImportResource(string sourcePath)
    {
        if (Directory == null)
            throw new InvalidOperationException("Cannot import resource: song has no directory. Save the song first.");

        string fileName = Path.GetFileName(sourcePath);
        string destination = Path.Combine(Directory, fileName);
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            File.Copy(sourcePath, destination, overwrite: true);
        return fileName;
    }

    public ITrackStore? GetTrackStore(AudioManager audio)
    {
        if (Directory == null)
            return null;
        if (trackStore != null && string.Equals(trackStoreDirectory, Directory, StringComparison.OrdinalIgnoreCase))
            return trackStore;

        trackStore = audio.GetTrackStore(new StorageBackedResourceStore(new NativeStorage(Directory)));
        trackStoreDirectory = Directory;
        return trackStore;
    }

    /// <summary>
    /// Loads the song's jacket texture from its directory, or null when the song has never been
    /// saved or has no <see cref="SongResources.Background"/>. Mirrors <see cref="GetTrackStore"/>'s
    /// per-directory store caching. LargeTextureStore: jackets are large and must not be atlased.
    /// </summary>
    public Texture? GetJacketTexture(GameHost host)
    {
        if (Directory == null || string.IsNullOrEmpty(Song.Resources.Background))
            return null;

        if (jacketStore == null || !string.Equals(jacketStoreDirectory, Directory, StringComparison.OrdinalIgnoreCase))
        {
            jacketStore = new LargeTextureStore(host.Renderer,
                host.CreateTextureLoaderStore(new StorageBackedResourceStore(new NativeStorage(Directory))),
                manualMipmaps: false);
            jacketStoreDirectory = Directory;
        }

        return jacketStore.Get(Song.Resources.Background);
    }

    public Stream? GetAudioStream()
    {
        if (Directory == null || string.IsNullOrEmpty(Song.Resources.Track))
            return null;
        return new StorageBackedResourceStore(new NativeStorage(Directory)).GetStream(Song.Resources.Track);
    }

    private void copyResourcesForSaveAs(string sourceDirectory, string destinationDirectory)
    {
        var resources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(Song.Resources.Track)) resources.Add(Song.Resources.Track);
        if (!string.IsNullOrEmpty(Song.Resources.Background)) resources.Add(Song.Resources.Background);

        var copies = new List<(string Resource, string Source, string Destination)>();
        foreach (string resource in resources)
        {
            string source = resolveResourcePath(sourceDirectory, resource);
            string destination = resolveResourcePath(destinationDirectory, resource);
            if (!File.Exists(source))
                throw new FileNotFoundException($"Song resource \"{resource}\" is missing.", source);
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
                continue;

            copies.Add((resource, source, destination));
        }

        foreach (var copy in copies)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(copy.Destination)!);
            File.Copy(copy.Source, copy.Destination, overwrite: true);
        }
    }

    private static string resolveResourcePath(string root, string resource)
    {
        string absoluteRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(absoluteRoot, resource));
        if (!candidate.StartsWith(absoluteRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Song resource \"{resource}\" escapes the song directory.");
        return candidate;
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        (trackStore as IDisposable)?.Dispose();
        trackStore = null;
        trackStoreDirectory = null;
        jacketStore?.Dispose();
        jacketStore = null;
        jacketStoreDirectory = null;
        IsDisposed = true;
    }

    public bool IsDisposed { get; private set; }
}
