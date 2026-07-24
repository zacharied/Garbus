using System;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;
using Garbus.Game.Objects.Judgement;

namespace Garbus.Game.Objects;

public partial class ShoulderNote : Note, IHasCardinalDirection, IHasAngle
{
    public required HorizontalDirection Side { get; set; }

    public int AngleDeg => Side.ToAngleDeg();

    public override GarbusButtonInput ButtonInput => Side switch
    {
        HorizontalDirection.Left => GarbusButtonInput.ButtonL,
        HorizontalDirection.Right => GarbusButtonInput.ButtonR,
        _ => throw new InvalidOperationException()
    };

    /// <summary>
    /// A left shoulder travels in the West lane, a right shoulder in the East lane.
    /// </summary>
    public CardinalDirection Direction => Side == HorizontalDirection.Left
        ? CardinalDirection.West
        : CardinalDirection.East;

    public override HitsoundFamily Hitsounds => HitsoundFamilies.ShoulderNote;

    protected override HitWindows CreateHitWindows() => new ShoulderNoteHitWindows();
}
