// Replaces BigAssCircle's RulesetInputManager<BigAssCircleAction> (osu.Game's ruleset input plumbing —
// realm-backed bindings and replay handling — is deliberately not vendored). A plain framework
// KeyBindingContainer over GarbusAction with hardcoded defaults; config-backed rebinding arrives with
// the Phase 5 settings screen.

using System.Collections.Generic;
using osu.Framework.Input.Bindings;

namespace Garbus.Game.Input
{
    public partial class GarbusInputManager : KeyBindingContainer<GarbusAction>
    {
        public GarbusInputManager()
            : base(SimultaneousBindingMode.All, KeyCombinationMatchingMode.Any)
        {
            RelativeSizeAxes = osu.Framework.Graphics.Axes.Both;
        }

        public override IEnumerable<IKeyBinding> DefaultKeyBindings => new[]
        {
            // Gamepad — verbatim from BigAssCircleRuleset.GetDefaultKeyBindings:
            // two gamepad buttons per direction, each bound to its own action: the d-pad button drives
            // the "…1" action and the physically-matching face button drives the "…2" action.
            //
            // Each cardinal direction sits at its matching on-screen position: an action is drawn at
            // (cosθ, -sinθ) where θ = direction.ToRadians(), so East = right, North = up, West = left,
            // South = down. Each physical button maps to the direction at that same screen position.
            //
            // The controller is opened as an SDL gamepad, so face buttons arrive as
            // X=Joystick1, A=Joystick2, B=Joystick3, Y=Joystick4 and the d-pad as JoystickHat1*.

            // Screen up -> North  (D-pad Up = N1, Y = N2)
            new KeyBinding(InputKey.JoystickHat1Up, GarbusAction.ButtonN1),
            new KeyBinding(InputKey.Joystick4, GarbusAction.ButtonN2),

            // Screen right -> East  (D-pad Right = E1, B = E2)
            new KeyBinding(InputKey.JoystickHat1Right, GarbusAction.ButtonE1),
            new KeyBinding(InputKey.Joystick3, GarbusAction.ButtonE2),

            // Screen down -> South  (D-pad Down = S1, A = S2)
            new KeyBinding(InputKey.JoystickHat1Down, GarbusAction.ButtonS1),
            new KeyBinding(InputKey.Joystick2, GarbusAction.ButtonS2),

            // Screen left -> West  (D-pad Left = W1, X = W2)
            new KeyBinding(InputKey.JoystickHat1Left, GarbusAction.ButtonW1),
            new KeyBinding(InputKey.Joystick1, GarbusAction.ButtonW2),

            // Left and right shoulder buttons
            new KeyBinding(InputKey.Joystick5, GarbusAction.ButtonL),
            new KeyBinding(InputKey.Joystick6, GarbusAction.ButtonR),
        };
    }
}
