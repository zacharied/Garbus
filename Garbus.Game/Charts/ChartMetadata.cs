namespace Garbus.Game.Charts;

public class ChartMetadata
{
    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    /// <summary>
    /// The chart author.
    /// </summary>
    public string Charter { get; set; } = string.Empty;

    /// <summary>
    /// The name of this particular chart of the song (osu's "difficulty name").
    /// </summary>
    public string ChartName { get; set; } = string.Empty;

    /// <summary>
    /// The audio file this chart is timed against, resolved through the game's track store
    /// (full filename including extension).
    /// </summary>
    public string AudioFile { get; set; } = string.Empty;
}
