using System.Diagnostics;

namespace Garbus.Game.Edit.Preview;

internal sealed class ChartPreviewClock
{
    private readonly long timestampFrequency;

    private double authoritativeTime;
    private bool isRunning;
    private double rate;
    private long revision;
    private long authoritativeTimestamp;

    public ChartPreviewClock()
        : this(Stopwatch.Frequency)
    {
    }

    public ChartPreviewClock(long timestampFrequency)
    {
        this.timestampFrequency = timestampFrequency;
    }

    public double CurrentTime => CurrentTimeAt(Stopwatch.GetTimestamp());

    public double CurrentTimeAt(long timestamp)
    {
        if (!isRunning)
            return authoritativeTime;

        double elapsedMilliseconds = (timestamp - authoritativeTimestamp) * 1000.0 / timestampFrequency;
        return authoritativeTime + elapsedMilliseconds * rate;
    }

    public void Apply(ChartPreviewTransport state)
    {
        if (state.Revision <= revision)
            return;

        revision = state.Revision;
        authoritativeTime = state.Time;
        isRunning = state.IsRunning;
        rate = state.Rate;
        authoritativeTimestamp = state.Timestamp;
    }
}
