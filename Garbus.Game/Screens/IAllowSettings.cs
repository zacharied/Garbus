namespace Garbus.Game.Screens
{
    /// <summary>Marker for screens on which the global settings overlay is available.</summary>
    public interface IAllowSettings
    {
        /// <summary>
        /// Whether the floating settings gear should be shown on this screen. Screens that expose
        /// settings through their own chrome (e.g. the editor, via its File → Game settings menu
        /// item) return false — the overlay is still permitted, it just has no floating gear.
        /// </summary>
        bool ShowSettingsGear => true;
    }
}
