using Garbus.Game.Core;
using Garbus.Game.Gameplay;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI.Scrolling;

namespace Garbus.Game.Edit.Preview;

internal sealed class PreviewGameplayPresentationPolicy : IGameplayPresentationPolicy
{
    private readonly GarbusScrollingInfo scrollingInfo;

    internal PreviewGameplayPresentationPolicy(GarbusScrollingInfo scrollingInfo)
    {
        this.scrollingInfo = scrollingInfo;
    }

    public bool HandlesInput => false;
    public bool PlaysSamples => false;
    public bool PlaysSpawnAnimations => false;
    public bool UsesExternalResults => true;
    public bool UsesClockDrivenVisuals => true;

    public double LifetimeEndFor(HitObject hitObject) => hitObject.GetEndTime() + scrollingInfo.TimeRange.Value;
    public double ResultTimeFor(HitObject hitObject) => hitObject.GetEndTime();
    public bool PresentsHoldAsHeld(DrawableHitObject hold) => true;
    public bool PresentsSliderAngleAsCaught(HorizontalDirection side, double angleDeg) => true;
}
