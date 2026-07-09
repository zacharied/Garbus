// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/BacBeatSnapGrid.cs). BacBeatSnapGrid →
// GarbusBeatSnapGrid; BacEditorPlayfield → GarbusEditorPlayfield; base BeatSnapGrid / HitObjectComposer
// are the Garbus vendored ones (Edit.Compose). Otherwise verbatim.

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
