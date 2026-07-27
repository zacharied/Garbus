namespace Garbus.Game.Settings
{
    /// <summary>
    /// Opens the global settings overlay. Cached in DI by <see cref="GlobalSettingsContainer"/>'s host
    /// so any screen (e.g. the editor's File → Game settings item) can request it without holding a
    /// direct reference to the container.
    /// </summary>
    public interface ISettingsOverlayControl
    {
        /// <summary>Shows the settings overlay, if the current screen permits settings.</summary>
        void OpenSettings();
    }
}
