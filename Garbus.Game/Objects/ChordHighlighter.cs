// A resolvable holder for the current ChordIndex. Gameplay rebuilds it once from the static chart; the
// editor rebuilds it on every chart mutation. Cardinal note drawables and the connector overlay read it.

using System;
using System.Collections.Generic;
using Garbus.Game.Gameplay.Objects;

namespace Garbus.Game.Objects;

public sealed class ChordHighlighter
{
    private ChordIndex index = new ChordIndex(Array.Empty<HitObject>());

    public void Rebuild(IEnumerable<HitObject> hitObjects) => index = new ChordIndex(hitObjects);

    public bool IsInChord(HitObject hitObject) => index.IsInChord(hitObject);

    public IReadOnlyList<ChordIndex.ChordGroup> Groups => index.Groups;
}
