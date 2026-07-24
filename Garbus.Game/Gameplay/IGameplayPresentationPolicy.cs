using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;

namespace Garbus.Game.Gameplay;

internal interface IGameplayPresentationPolicy
{
    bool HandlesInput { get; }
    bool PlaysSamples { get; }
    bool PlaysSpawnAnimations { get; }
    bool UsesExternalResults { get; }
    bool UsesClockDrivenVisuals { get; }

    double LifetimeEndFor(HitObject hitObject);
    double ResultTimeFor(HitObject hitObject);
    bool PresentsHoldAsHeld(DrawableHitObject hold);
    bool PresentsSliderAngleAsCaught(HorizontalDirection side, double angleDeg);
}
