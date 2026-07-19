// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/IHittableNote.cs).

using System;
using Garbus.Game.Gameplay.Objects.Drawables;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A non-generic view over a note drawable that participates in a lane's note-lock. Lets the
/// <see cref="UI.Lane"/> and <see cref="UI.GarbusOrderedHitPolicy"/> drive the policy without knowing the
/// concrete <see cref="DrawableNote{T}"/> type.
/// </summary>
public interface IHittableNote
{
    /// <summary>
    /// Note-lock gate installed by the owning lane: vetoes a press that belongs to an older
    /// eligible object in the lane.
    /// </summary>
    Func<DrawableHitObject, double, bool>? CheckHittable { get; set; }

    /// <summary>
    /// Whether this note's note-locked press has been judged — the head, for holds. Once true the
    /// object no longer competes for presses (its tail may still be pending).
    /// </summary>
    bool PressJudged { get; }
}
