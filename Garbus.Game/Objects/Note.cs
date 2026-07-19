// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Note.cs).

using Garbus.Game.Input;

namespace Garbus.Game.Objects;

public abstract partial class Note : GarbusHitObject
{
    public abstract GarbusButtonInput ButtonInput { get; }
}
