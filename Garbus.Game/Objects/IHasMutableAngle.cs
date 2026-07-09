// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/IHasMutableAngle.cs).

namespace Garbus.Game.Objects;

/// <summary>
/// An <see cref="IHasAngle"/> whose angle is stored (not derived) and can be rewritten — the editor's
/// hook for placing and dragging objects around the circle. Derived-angle objects
/// (<see cref="ShoulderNote"/>, slider/hold nested objects) intentionally do not implement this.
/// </summary>
public interface IHasMutableAngle : IHasAngle
{
    new int AngleDeg { get; set; }
}
