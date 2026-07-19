// Replaces BigAssCircle's RulesetInputManager<BigAssCircleAction>. A plain framework
// KeyBindingContainer over GarbusAction. Defaults live in the shared DefaultBindings map so the
// KeyBindingStore can overlay per-action overrides on the same source of truth.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
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

        // One physical button per action. The d-pad drives the "…1" actions and the physically-matching
        // face button drives "…2"; each direction sits at its matching on-screen position. The controller
        // is opened as an SDL gamepad, so face buttons arrive as X=Joystick1, A=Joystick2, B=Joystick3,
        // Y=Joystick4 and the d-pad as JoystickHat1*.
        public static readonly IReadOnlyDictionary<GarbusAction, InputKey> DefaultBindings = new Dictionary<GarbusAction, InputKey>
        {
            // Screen up -> North  (D-pad Up = N1, Y = N2)
            { GarbusAction.ButtonN1, InputKey.JoystickHat1Up },
            { GarbusAction.ButtonN2, InputKey.Joystick4 },

            // Screen right -> East  (D-pad Right = E1, B = E2)
            { GarbusAction.ButtonE1, InputKey.JoystickHat1Right },
            { GarbusAction.ButtonE2, InputKey.Joystick3 },

            // Screen down -> South  (D-pad Down = S1, A = S2)
            { GarbusAction.ButtonS1, InputKey.JoystickHat1Down },
            { GarbusAction.ButtonS2, InputKey.Joystick2 },

            // Screen left -> West  (D-pad Left = W1, X = W2)
            { GarbusAction.ButtonW1, InputKey.JoystickHat1Left },
            { GarbusAction.ButtonW2, InputKey.Joystick1 },

            // Left and right shoulder buttons
            { GarbusAction.ButtonL, InputKey.Joystick5 },
            { GarbusAction.ButtonR, InputKey.Joystick6 },
        };

        public override IEnumerable<IKeyBinding> DefaultKeyBindings =>
            DefaultBindings.Select(b => new KeyBinding(b.Value, b.Key)).ToArray();

        // Assigned from the cached KeyBindingStore when one is present; otherwise the base class falls
        // back to DefaultKeyBindings. canBeNull keeps bare-constructed test instances working.
        [BackgroundDependencyLoader(true)]
        private void load(KeyBindingStore store)
        {
            if (store != null)
                KeyBindings = store.GetKeyBindings().ToList();
        }
    }
}
