using Garbus.Game.Input;

namespace Garbus.Game.Objects;

public abstract partial class Note : GarbusHitObject
{
    public abstract GarbusButtonInput ButtonInput { get; }
}
