// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableCardinalCardinalHoldNoteHead.cs).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// The head of a <see cref="DrawableCardinalHoldNote"/>. It is a purely judgemental object — the parent hold draws
/// the head sprite and owns the input — so this draws nothing and never handles input itself. Its result is
/// applied when the parent delegates a press via <see cref="UpdateResult"/>, or it auto-misses once its hit
/// window elapses, mirroring <see cref="DrawableNote{T}.CheckForResult"/> (i.e. a plain cardinal note).
/// </summary>
public partial class DrawableCardinalCardinalHoldNoteHead : DrawableGarbusHitObject<CardinalHoldNoteHead>, ISelfPosition
{
    // The parent shows the combined hold result; the head is scored/combo'd but its popup is suppressed so
    // the two don't stack on the playfield centre.
    public override bool DisplayResult => false;

    public DrawableCardinalCardinalHoldNoteHead(CardinalHoldNoteHead hitObject)
        : base(hitObject)
    {
    }

    /// <summary>
    /// Triggers a user-driven result check. Called by the parent hold's <c>OnPressed</c> so the head is
    /// judged on the same press, without independently participating in input. Returns whether it judged.
    /// </summary>
    public bool UpdateResult() => base.UpdateResult(true);

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (!userTriggered)
        {
            if (!HitObject.HitWindows.CanBeHit(timeOffset))
                ApplyMinResult();
            return;
        }

        var result = HitObject.HitWindows.ResultFor(timeOffset);

        if (result == HitResult.None)
            return;

        ApplyResult(result);
    }
}
