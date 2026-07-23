namespace Garbus.Game.Input
{
    /// <summary>
    /// A family of physical controller whose face buttons carry distinct artwork. The value selects which
    /// icon set <see cref="GamepadButtonIcons"/> resolves a <see cref="GamepadButton"/> against.
    /// </summary>
    public enum GamepadType
    {
        /// <summary>Sony PlayStation DualSense ("DS5"). The only controller supported for now.</summary>
        DualSense,
    }
}
