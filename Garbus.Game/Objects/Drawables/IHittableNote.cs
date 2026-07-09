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
    /// Note-lock gate installed by the owning lane: only the earliest un-judged object may be hit.
    /// </summary>
    Func<DrawableHitObject, double, bool>? CheckHittable { get; set; }

    /// <summary>
    /// Forces this object to be missed, disregarding its own result check. Used by the lane's hit policy
    /// to note-lock earlier objects when a later one is hit.
    /// </summary>
    void MissForcefully();
}
