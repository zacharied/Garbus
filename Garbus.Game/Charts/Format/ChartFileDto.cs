// The on-disk shape of a .garbus chart file (format decisions recorded in PLAN-port.md: JSON for
// readability/diffability, integer version field, control points as typed lists). This DTO layer is
// deliberately separate from the domain model so the file format can stay stable while the domain
// evolves — all mapping lives in GarbusChartSerializer.
//
// Note: hit object polymorphism uses a "type" discriminator which System.Text.Json (net8) requires to
// be the FIRST property of each hit object — the encoder always writes it first; keep it first when
// hand-editing charts.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Garbus.Game.Charts.Format;

public class ChartFileDto
{
    public int Version { get; set; }

    public ChartMetadataDto Metadata { get; set; } = new ChartMetadataDto();

    public double? PreviewTime { get; set; }

    public List<TimingPointDto> TimingPoints { get; set; } = new List<TimingPointDto>();

    public List<HitObjectDto> HitObjects { get; set; } = new List<HitObjectDto>();

    public List<DesignPointDto> DesignPoints { get; set; } = new List<DesignPointDto>();
}

public class ChartMetadataDto
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Charter { get; set; } = string.Empty;
    public string ChartName { get; set; } = string.Empty;
    public string RomanisedTitle { get; set; } = string.Empty;
    public string RomanisedArtist { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string AudioFile { get; set; } = string.Empty;
    public string BackgroundFile { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Difficulty { get; set; } = nameof(Charts.Difficulty.Novice);
}

public class TimingPointDto
{
    public double Time { get; set; }
    public double BeatLength { get; set; }

    /// <summary>Time signature numerator (the denominator is always 4, as in osu).</summary>
    public int TimeSignature { get; set; } = 4;

    public bool OmitFirstBarLine { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CardinalNoteDto), "cardinal")]
[JsonDerivedType(typeof(CardinalHoldNoteDto), "cardinal-hold")]
[JsonDerivedType(typeof(ShoulderNoteDto), "shoulder")]
[JsonDerivedType(typeof(ShoulderHoldNoteDto), "shoulder-hold")]
[JsonDerivedType(typeof(SliderBodyDto), "slider")]
[JsonDerivedType(typeof(SlamCenteredDto), "slamCentered")]
[JsonDerivedType(typeof(SlamEdgeDto), "slamEdge")]
public abstract class HitObjectDto
{
    public double Time { get; set; }
}

public class CardinalNoteDto : HitObjectDto
{
    public int AngleDeg { get; set; }
}

public class CardinalHoldNoteDto : HitObjectDto
{
    public int AngleDeg { get; set; }
    public double Duration { get; set; }
}

public class ShoulderNoteDto : HitObjectDto
{
    public string Side { get; set; } = string.Empty;
}

public class ShoulderHoldNoteDto : HitObjectDto
{
    public string Side { get; set; } = string.Empty;
    public double Duration { get; set; }
}

public class SliderBodyDto : HitObjectDto
{
    public int AngleDeg { get; set; }
    public string Side { get; set; } = string.Empty;
    public List<PathControlPointDto> ControlPoints { get; set; } = new List<PathControlPointDto>();
}

public class PathControlPointDto
{
    public double TimeOffset { get; set; }
    public int RotationOffset { get; set; }
    public bool Smooth { get; set; }

    /// <summary>An osu.Framework <c>Easing</c> name; "None" for constant-velocity linear.</summary>
    public string SweepEasing { get; set; } = "None";
}

public class SlamCenteredDto : HitObjectDto
{
    public int AngleDeg { get; set; }
    public string Side { get; set; } = string.Empty;
}

public class SlamEdgeDto : HitObjectDto
{
    public int AngleDeg { get; set; }
    public string Side { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TutorialMessageDto), "tutorial-message")]
public abstract class DesignPointDto
{
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}

public class TutorialMessageDto : DesignPointDto
{
    public string Text { get; set; } = string.Empty;
}
