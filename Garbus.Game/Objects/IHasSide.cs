using Garbus.Game.Core;

namespace Garbus.Game.Objects;

/// <summary>
/// A hit object whose <see cref="HorizontalDirection"/> side is a stored, freely-toggleable property —
/// the editor's hook for flipping an object's side in place (the S key / "Right side" context menu)
/// without relocating it. Objects whose side is positional (<see cref="ShoulderNote"/>,
/// <see cref="ShoulderHoldNote"/>, where Side selects the lane/button and thus the object's position)
/// intentionally do not implement this — mirroring how <see cref="IHasMutableAngle"/> excludes
/// derived-angle objects.
/// </summary>
public interface IHasSide
{
    HorizontalDirection Side { get; set; }
}
