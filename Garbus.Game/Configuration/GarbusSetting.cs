namespace Garbus.Game.Configuration
{
    public enum GarbusSetting
    {
        /// <summary>
        /// Global user audio offset in milliseconds, applied on top of the platform offset.
        /// Positive values shift audio later relative to gameplay time.
        /// </summary>
        AudioOffset,

        // --- Editor View settings ---

        /// <summary>Show red timing-change lines in the timeline strip.</summary>
        EditorShowTimingChanges,

        /// <summary>Show beat tick lines in the timeline strip.</summary>
        EditorShowTicks,

        /// <summary>Opacity (0–1) of the waveform layer in the timeline strip.</summary>
        EditorWaveformOpacity,

        /// <summary>
        /// When placing a hit object, automatically seek the editor clock to its start time.
        /// </summary>
        EditorAutoSeekOnPlacement,

        /// <summary>Contract the side toolboxes to give more room to the compose area.</summary>
        EditorContractSidebars,
    }
}
