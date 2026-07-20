using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game.Charts.Format;
using osu.Framework.IO.Stores;

namespace Garbus.Game.Charts;

public class SongStore
{
    private readonly IResourceStore<byte[]> store;

    public SongStore(IResourceStore<byte[]> resources)
    {
        store = new NamespacedResourceStore<byte[]>(resources, @"Charts");
    }

    public SongDecodeResult Get(string name)
    {
        using var stream = store.GetStream(name);
        if (stream == null)
            throw new FileNotFoundException($"Song \"{name}\" not found in resources.");
        return GarbusSongSerializer.Decode(stream);
    }

    public IEnumerable<string> GetAvailableSongs() =>
        store.GetAvailableResources().Where(n => n.EndsWith(GarbusSongSerializer.FILE_EXTENSION, StringComparison.OrdinalIgnoreCase));
}
