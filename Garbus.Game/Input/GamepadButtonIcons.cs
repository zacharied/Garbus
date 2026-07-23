using System.Collections.Generic;

namespace Garbus.Game.Input
{
    /// <summary>
    /// Resolves a <see cref="GamepadButton"/> to the <c>TextureStore</c>-relative path of its icon for a
    /// given <see cref="GamepadType"/>. Paths are relative to <c>Garbus.Resources/Textures</c> and use
    /// forward slashes, so a result feeds straight into <c>TextureStore.Get(path)</c> (see
    /// <see cref="GamepadButtonSprite"/>).
    ///
    /// Layout: <c>Icons/Gamepad/{typeDir}/{buttonFile}</c>. Adding a controller is a new
    /// <see cref="GamepadType"/>, a folder of PNGs, and one entry in <see cref="type_directories"/> +
    /// one file map here — nothing else changes.
    /// </summary>
    public static class GamepadButtonIcons
    {
        /// <summary>
        /// The controller assumed when a caller does not specify one. Everything is DualSense for now.
        /// TODO(Phase 5): detect the connected pad and drive this from the active controller.
        /// </summary>
        public const GamepadType DefaultType = GamepadType.DualSense;

        private const string base_path = "Icons/Gamepad";

        private static readonly IReadOnlyDictionary<GamepadType, string> type_directories =
            new Dictionary<GamepadType, string>
            {
                { GamepadType.DualSense, "DS5" },
            };

        // File stems under each controller's directory. Compass-named buttons map to the physical
        // artwork filenames (DpadEast -> the right-arrow art, etc.).
        private static readonly IReadOnlyDictionary<GamepadButton, string> dual_sense_files =
            new Dictionary<GamepadButton, string>
            {
                { GamepadButton.FaceSouth, "cross" },
                { GamepadButton.FaceEast, "circle" },
                { GamepadButton.FaceWest, "square" },
                { GamepadButton.FaceNorth, "triangle" },
                { GamepadButton.DpadNorth, "dpad-up" },
                { GamepadButton.DpadSouth, "dpad-down" },
                { GamepadButton.DpadWest, "dpad-left" },
                { GamepadButton.DpadEast, "dpad-right" },
                { GamepadButton.ShoulderLeft, "l1" },
                { GamepadButton.ShoulderRight, "r1" },
                { GamepadButton.StickLeft, "stick-left" },
                { GamepadButton.StickRight, "stick-right" },
            };

        private static IReadOnlyDictionary<GamepadButton, string>? filesFor(GamepadType type) =>
            type switch
            {
                GamepadType.DualSense => dual_sense_files,
                _ => null,
            };

        /// <summary>
        /// Resolves the texture path for <paramref name="button"/> on <paramref name="type"/>, or
        /// <c>null</c> when that controller has no artwork for the button.
        /// </summary>
        public static string? ResolveTexturePath(GamepadButton button, GamepadType type = DefaultType)
        {
            if (!type_directories.TryGetValue(type, out string? dir))
                return null;

            if (filesFor(type) is not { } files || !files.TryGetValue(button, out string? file))
                return null;

            return $"{base_path}/{dir}/{file}";
        }

        /// <summary>
        /// The gamepad button that a <see cref="GarbusAction"/> is bound to: a "…1" action is a d-pad
        /// direction, a "…2" action is the matching face button. Lets a prompt show the real glyph for a
        /// game action without the caller knowing the physical layout.
        /// </summary>
        public static GamepadButton ToGamepadButton(this GarbusAction action) =>
            action switch
            {
                GarbusAction.ButtonE1 => GamepadButton.DpadEast,
                GarbusAction.ButtonN1 => GamepadButton.DpadNorth,
                GarbusAction.ButtonW1 => GamepadButton.DpadWest,
                GarbusAction.ButtonS1 => GamepadButton.DpadSouth,
                GarbusAction.ButtonE2 => GamepadButton.FaceEast,
                GarbusAction.ButtonN2 => GamepadButton.FaceNorth,
                GarbusAction.ButtonW2 => GamepadButton.FaceWest,
                GarbusAction.ButtonS2 => GamepadButton.FaceSouth,
                GarbusAction.ButtonL => GamepadButton.ShoulderLeft,
                GarbusAction.ButtonR => GamepadButton.ShoulderRight,
                _ => throw new System.ArgumentOutOfRangeException(nameof(action), action, null),
            };
    }
}
