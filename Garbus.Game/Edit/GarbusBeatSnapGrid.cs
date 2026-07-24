using System.Collections.Generic;
using osu.Framework.Graphics.Containers;
using Garbus.Game.Edit.Compose;

namespace Garbus.Game.Edit;

public partial class GarbusBeatSnapGrid : BeatSnapGrid
{
    protected override IEnumerable<Container> GetTargetContainers(HitObjectComposer composer)
    {
        yield return ((GarbusEditorPlayfield)composer.Playfield).UnderlayElements;
    }
}
