namespace Garbus.Game.Charts;

/// <summary>
/// A semantic representation of a chart's difficulty relative to the other charts for its song
/// (see docs/rules-specs/Charts.md). Ordered easiest → hardest.
/// </summary>
public enum Difficulty
{
    Tutorial,
    Novice,
    Advanced,
    Expert,
}
