// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/BacSnapResult.cs). BacSnapResult →
// GarbusSnapResult; SnapResult/Playfield resolve to the Garbus vendored types (Edit.Compose /
// Gameplay.UI). Otherwise verbatim.

using Garbus.Game.Edit.Compose;
using Garbus.Game.Gameplay.UI;
using osuTK;

namespace Garbus.Game.Edit;

/// <summary>
/// A <see cref="SnapResult"/> that additionally carries the angle-snapped, wrap-normalised angle the
/// cursor position corresponds to. Produced by
/// <see cref="GarbusHitObjectComposer.FindSnappedAngleTimeAndPosition"/>.
/// </summary>
public class GarbusSnapResult : SnapResult
{
    public readonly int AngleDeg;

    public GarbusSnapResult(Vector2 screenSpacePosition, double? time, int angleDeg, Playfield? playfield = null)
        : base(screenSpacePosition, time, playfield)
    {
        AngleDeg = angleDeg;
    }
}
