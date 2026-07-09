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

    /// <summary>UTF-8 latin-readable variants for players who can't read the native script.</summary>
    public string RomanisedTitle { get; set; } = string.Empty;

    public string RomanisedArtist { get; set; } = string.Empty;

    /// <summary>The media the song comes from.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Space-separated search tags.</summary>
    public string Tags { get; set; } = string.Empty;

    /// <summary>
    /// The audio file this chart is timed against, resolved through the game's track store
    /// (full filename including extension).
    /// </summary>
    public string AudioFile { get; set; } = string.Empty;

    /// <summary>
    /// The background image beside the chart file (full filename including extension);
    /// empty when the chart has none. Stored only this phase — nothing renders it yet.
    /// </summary>
    public string BackgroundFile { get; set; } = string.Empty;
}
