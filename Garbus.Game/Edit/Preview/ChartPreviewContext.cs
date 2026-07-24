using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Gameplay.Objects;

namespace Garbus.Game.Edit.Preview;

internal sealed class ChartPreviewContext
{
    private readonly GarbusScrollingInfo scrollingInfo;

    internal ChartPreviewContext(GarbusScrollingInfo scrollingInfo)
    {
        this.scrollingInfo = scrollingInfo;
    }

    internal double LifetimeEndFor(HitObject hitObject) => hitObject.GetEndTime() + scrollingInfo.TimeRange.Value;

    internal double ResultTimeFor(HitObject hitObject) => hitObject.GetEndTime();
}
