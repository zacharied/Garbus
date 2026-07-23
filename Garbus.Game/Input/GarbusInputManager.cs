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

        // Resolved when a store is cached (the running game / a test that provides one); null otherwise,
        // so bare-constructed test instances fall back to DefaultKeyBindings.
        [Resolved(canBeNull: true)]
        private KeyBindingStore? store { get; set; }

        /// <summary>The bindings actually in effect: the store's overrides when one is cached, else defaults.</summary>
        public IEnumerable<IKeyBinding> ActiveKeyBindings => KeyBindings ?? DefaultKeyBindings;

        /// <summary>Re-reads the effective bindings from the store. Lets a long-lived input manager (e.g. the
        /// button-test panel sitting beside the rebind view) reflect a rebind without being recreated.</summary>
        public void ReloadBindings() => ReloadMappings();

        // The base class calls this from LoadComplete (and would otherwise reset KeyBindings to the
        // defaults). Pull from the store when present so persisted rebinds take effect.
        protected override void ReloadMappings()
        {
            KeyBindings = store?.GetKeyBindings().ToList() ?? DefaultKeyBindings;
        }
    }
}
