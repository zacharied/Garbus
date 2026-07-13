// Ported from BigAssCircle (shoulder counterpart of the held cardinal note).

using System;
using System.Threading;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Input;

namespace Garbus.Game.Objects;

/// <summary>
/// A held shoulder note — a <see cref="ShoulderNote"/> with a <see cref="Duration"/>. Its angle, button and
/// lane are derived from its <see cref="Side"/> (like <see cref="ShoulderNote"/>); it carries a nested head
/// judged like a shoulder press, with the tail judged on how much of the hold was held.
/// </summary>
public class ShoulderHoldNote : Note, IHasCardinalDirection, IHasAngle, IHasDuration
{
    public required HorizontalDirection Side { get; set; }

    public double Duration { get; set; }

    public double EndTime => StartTime + Duration;

    public int AngleDeg => Side.ToAngleDeg();

    public override GarbusButtonInput ButtonInput => Side switch
    {
        HorizontalDirection.Left => GarbusButtonInput.ButtonL,
        HorizontalDirection.Right => GarbusButtonInput.ButtonR,
        _ => throw new InvalidOperationException()
    };

    public CardinalDirection Direction => Side == HorizontalDirection.Left
        ? CardinalDirection.West
        : CardinalDirection.East;

    public HoldNoteHead<ShoulderHoldNote> Head { get; private set; } = null!;

    protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
    {
        base.CreateNestedHitObjects(cancellationToken);

        AddNested(Head = new HoldNoteHead<ShoulderHoldNote>(this)
        {
            StartTime = StartTime,
        });
    }

    public override HitsoundFamily Hitsounds => HitsoundFamilies.ShoulderHoldNote;
}
