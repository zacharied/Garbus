using osu.Framework.Bindables;

namespace Garbus.Game.Objects;

public class GarbusPath
{
    public required BindableList<GarbusPathControlPoint> ControlPoints { get; init; }
}
