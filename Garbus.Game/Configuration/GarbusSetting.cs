namespace Garbus.Game.Configuration
{
    public enum GarbusSetting
    {
        /// <summary>
        /// Global user audio offset in milliseconds, applied on top of the platform offset.
        /// Positive values shift audio later relative to gameplay time.
        /// </summary>
        AudioOffset,

        /// <summary>
        /// Use the framework's experimental low-latency WASAPI output path. Roughly halves audio
        /// output latency on Windows, which the editor's hitsound feedback needs to feel on-beat.
        /// The clock's platform offset auto-recalibrates (+15 → −10 ms) when this flips
        /// (see <c>FramedChartClock.WINDOWS_EXPERIMENTAL_AUDIO_OFFSET</c>). Falls back to the normal
        /// output path if the device does not support it.
        /// </summary>
        UseExperimentalWasapi,

        // --- Editor View settings ---

        /// <summary>Show red timing-change lines in the timeline strip.</summary>
        EditorShowTimingChanges,

        /// <summary>Show translucent design-point regions in the timeline strip.</summary>
        EditorShowDesignRegions,

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

        /// <summary>Mini preview distance from the right edge of the compose workspace.</summary>
        MiniPreviewX,

        /// <summary>Mini preview distance from the bottom edge of the compose workspace.</summary>
        MiniPreviewY,

        /// <summary>
        /// The directory last used in any editor file dialog (Open / Save As / resource pickers).
        /// Dialogs start here on next open; persisted across sessions.
        /// </summary>
        EditorLastFileDirectory,

        // --- Song select ---

        /// <summary>Whether song select groups charts by song (true) or lists every chart flat (false).</summary>
        SongSelectGrouped,

        // --- Gameplay ---

        /// <summary>Scroll speed (higher = faster). Maps to gameplay TimeRange via ScrollSpeedMapping.</summary>
        ScrollSpeed,
    }
}
