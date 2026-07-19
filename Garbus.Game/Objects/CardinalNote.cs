// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/CardinalNote.cs).

using System;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;
using Garbus.Game.Objects.Judgement;

namespace Garbus.Game.Objects;

public class CardinalNote : Note, IHasCardinalDirection, IHasMutableAngle
{
    public required int AngleDeg { get; set; }

    public CardinalDirection Direction => CardinalDirectionExtensions.FromAngle(AngleDeg);

    public override GarbusButtonInput ButtonInput => Direction switch
    {
        CardinalDirection.East => GarbusButtonInput.ButtonE,
        CardinalDirection.North => GarbusButtonInput.ButtonN,
        CardinalDirection.West => GarbusButtonInput.ButtonW,
        CardinalDirection.South => GarbusButtonInput.ButtonS,
        _ => throw new InvalidOperationException()
    };

    public override HitsoundFamily Hitsounds => HitsoundFamilies.CardinalNote;

    protected override HitWindows CreateHitWindows() => new CardinalNoteHitWindows();
}
