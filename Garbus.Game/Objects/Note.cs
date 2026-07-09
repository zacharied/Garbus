// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Note.cs).

using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;

namespace Garbus.Game.Objects;

public abstract partial class Note : GarbusHitObject
{
    protected override HitWindows CreateHitWindows()
    {
        return base.CreateHitWindows();
    }

    public abstract GarbusButtonInput ButtonInput { get; }
}
