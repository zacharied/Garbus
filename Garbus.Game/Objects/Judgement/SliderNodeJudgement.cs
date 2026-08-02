// The catch-timed judgement of one slider node, per docs/rules-specs/Judgement.md ("Catch timing").
// Perfect requires the node's angle to be covered as StartTime is reached; coverage anywhere else
// inside the node window is a Bad; no coverage inside it is a Miss.

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class SliderNodeJudgement
{
    private bool coveredInWindow;

    /// <summary>The node's judgement, or null while it is still undecided.</summary>
    public HitResult? Result { get; private set; }

    public void Reset()
    {
        coveredInWindow = false;
        Result = null;
    }

    /// <summary>
    /// Fold one frame of catch state in. <paramref name="covered"/> is whether the input covers the
    /// node's angle now; <paramref name="previousTime"/> is the previous frame's time, used to spot
    /// the frame that crosses StartTime.
    /// </summary>
    public HitResult? Update(double previousTime, double time, double startTime, bool covered)
    {
        if (Result is not null)
            return Result;

        bool inWindow = time >= startTime - SliderNodeHitWindows.NODE_WINDOW
                        && time <= startTime + SliderNodeHitWindows.NODE_WINDOW;

        if (covered && inWindow)
            coveredInWindow = true;

        if (inWindow && covered && previousTime < startTime && time >= startTime)
            Result = HitResult.Perfect;
        else if (time >= startTime && coveredInWindow)
            Result = HitResult.Bad;
        else if (time > startTime + SliderNodeHitWindows.NODE_WINDOW)
            Result = HitResult.Miss;

        return Result;
    }
}
