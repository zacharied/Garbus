// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Core/Path/BacPath.cs). BacPath → GarbusPath.

using osu.Framework.Bindables;

namespace Garbus.Game.Objects;

public class GarbusPath
{
    public required BindableList<GarbusPathControlPoint> ControlPoints { get; init; }
}
