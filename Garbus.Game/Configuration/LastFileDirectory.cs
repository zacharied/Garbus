using System.IO;

namespace Garbus.Game.Configuration
{
    /// <summary>
    /// Shared read/write helpers for <see cref="GarbusSetting.EditorLastFileDirectory"/> — every
    /// editor file dialog (Open / Save As / resource pickers) starts in the directory last used by
    /// any of them, persisted across sessions.
    /// </summary>
    public static class LastFileDirectory
    {
        /// <summary>The remembered directory if it still exists, otherwise null (selector default).</summary>
        public static string? Get(GarbusConfigManager config)
        {
            string stored = config.Get<string>(GarbusSetting.EditorLastFileDirectory);
            return !string.IsNullOrEmpty(stored) && Directory.Exists(stored) ? stored : null;
        }

        public static void Set(GarbusConfigManager config, string? directory)
        {
            if (!string.IsNullOrEmpty(directory))
                config.SetValue(GarbusSetting.EditorLastFileDirectory, directory);
        }
    }
}
