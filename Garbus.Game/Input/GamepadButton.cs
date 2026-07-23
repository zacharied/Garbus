namespace Garbus.Game.Input
{
    /// <summary>
    /// A physical button on a gamepad, named by its position rather than any one vendor's label so a
    /// single value resolves to different artwork per <see cref="GamepadType"/> — e.g.
    /// <see cref="FaceSouth"/> is Cross on a DualSense but A on an Xbox pad. Compass directions match the
    /// codebase's existing input convention (<see cref="GarbusAction"/>): North = up, East = right.
    /// </summary>
    public enum GamepadButton
    {
        /// <summary>Bottom face button. DualSense: Cross.</summary>
        FaceSouth,

        /// <summary>Right face button. DualSense: Circle.</summary>
        FaceEast,

        /// <summary>Left face button. DualSense: Square.</summary>
        FaceWest,

        /// <summary>Top face button. DualSense: Triangle.</summary>
        FaceNorth,

        /// <summary>D-pad up.</summary>
        DpadNorth,

        /// <summary>D-pad down.</summary>
        DpadSouth,

        /// <summary>D-pad left.</summary>
        DpadWest,

        /// <summary>D-pad right.</summary>
        DpadEast,

        /// <summary>Left shoulder / bumper. DualSense: L1.</summary>
        ShoulderLeft,

        /// <summary>Right shoulder / bumper. DualSense: R1.</summary>
        ShoulderRight,

        /// <summary>Left analog stick (press = L3).</summary>
        StickLeft,

        /// <summary>Right analog stick (press = R3).</summary>
        StickRight,
    }
}
