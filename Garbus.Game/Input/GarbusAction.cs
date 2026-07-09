// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/BigAssCircleInputManager.cs) — the action
// enums and extensions. BigAssCircleAction → GarbusAction, BigAssCircleButtonInput → GarbusButtonInput.
// The RulesetInputManager wrapper itself is replaced by GarbusInputManager (a plain KeyBindingContainer).

using System.ComponentModel;
using Garbus.Game.Core;

namespace Garbus.Game.Input
{
    /// <summary>
    /// One action per physical button. Each cardinal direction has two: the "…1" action is driven by the
    /// d-pad and the "…2" action by the matching face button. See <see cref="GarbusButtonInput"/> for
    /// the collapsed view where a direction is a single logical input.
    /// </summary>
    public enum GarbusAction
    {
        [Description("Button East (D-Pad)")]
        ButtonE1,

        [Description("Button East (Face)")]
        ButtonE2,

        [Description("Button North (D-Pad)")]
        ButtonN1,

        [Description("Button North (Face)")]
        ButtonN2,

        [Description("Button West (D-Pad)")]
        ButtonW1,

        [Description("Button West (Face)")]
        ButtonW2,

        [Description("Button South (D-Pad)")]
        ButtonS1,

        [Description("Button South (Face)")]
        ButtonS2,

        [Description("Button Left")]
        ButtonL,

        [Description("Button Right")]
        ButtonR,
    }

    /// <summary>
    /// The logical button set from before the d-pad/face split, where a single value (e.g.
    /// <see cref="ButtonE"/>) represents both physical buttons for a direction. Not wired into input yet;
    /// reachable from a <see cref="GarbusAction"/> via <see cref="GarbusActionExtensions.ToButtonInput"/>.
    /// </summary>
    public enum GarbusButtonInput
    {
        [Description("Button East")]
        ButtonE,

        [Description("Button North")]
        ButtonN,

        [Description("Button West")]
        ButtonW,

        [Description("Button South")]
        ButtonS,

        [Description("Button Left")]
        ButtonL,

        [Description("Button Right")]
        ButtonR,
    }

    public static class GarbusActionExtensions
    {
        public static CardinalDirection? ToCardinalDirection(this GarbusAction action)
        {
            return action switch
            {
                GarbusAction.ButtonE1 or GarbusAction.ButtonE2 => CardinalDirection.East,
                GarbusAction.ButtonN1 or GarbusAction.ButtonN2 => CardinalDirection.North,
                GarbusAction.ButtonW1 or GarbusAction.ButtonW2 => CardinalDirection.West,
                GarbusAction.ButtonS1 or GarbusAction.ButtonS2 => CardinalDirection.South,
                _ => null
            };
        }

        /// <summary>
        /// Collapses a <see cref="GarbusAction"/> onto the logical <see cref="GarbusButtonInput"/>
        /// it belongs to, folding a direction's d-pad ("…1") and face ("…2") actions together.
        /// </summary>
        public static GarbusButtonInput ToButtonInput(this GarbusAction action)
        {
            return action switch
            {
                GarbusAction.ButtonE1 or GarbusAction.ButtonE2 => GarbusButtonInput.ButtonE,
                GarbusAction.ButtonN1 or GarbusAction.ButtonN2 => GarbusButtonInput.ButtonN,
                GarbusAction.ButtonW1 or GarbusAction.ButtonW2 => GarbusButtonInput.ButtonW,
                GarbusAction.ButtonS1 or GarbusAction.ButtonS2 => GarbusButtonInput.ButtonS,
                GarbusAction.ButtonL => GarbusButtonInput.ButtonL,
                GarbusAction.ButtonR => GarbusButtonInput.ButtonR,
                _ => throw new System.ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }
    }
}
