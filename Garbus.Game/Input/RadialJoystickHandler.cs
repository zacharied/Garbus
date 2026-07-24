// A drop-in replacement for osu-framework's JoystickHandler that deadzones the analog sticks on the
// stick *vector* (radially) rather than per-axis.
//
// Why: the stock handler applies its DeadzoneThreshold to each axis independently, zeroing whichever
// of X/Y is individually below the threshold. That flattens a wedge around each cardinal onto exactly
// N/E/S/W — the sticks "snap". Setting the stock threshold to 0 avoids the snap but reintroduces a
// worse bug: a drifting stick never reads exactly 0, and the framework simulates a JoystickButton for
// ANY non-zero axis value (JoystickAxisInput -> getAxisButtonForInput). That phantom axis-button stays
// held forever, polluting every global key-combination, so the framework debug key-bindings (Ctrl+F11
// frame statistics, Ctrl+F1 draw visualiser, F11/Alt+Enter fullscreen, …) silently stop matching.
//
// A radial deadzone only suppresses the near-centre region: below the threshold both axes read exactly
// 0 (clean idle state, no phantom button, no event flood), and above it the raw values pass through
// unchanged so the true angle is never distorted.
//
// osu-framework exposes the raw per-axis stick values only on the internal ISDLWindow, and its platform
// GameHosts have internal constructors — so neither this handler nor a custom host can subscribe through
// the public API. We bind to the window's joystick events by reflection ONCE in Initialize; the
// per-event path is a plain delegate call with no reflection. GarbusGameBase swaps this handler into
// Host.AvailableInputHandlers (which UserInputManager reads live) in place of the stock JoystickHandler.

using System;
using System.Linq;
using System.Reflection;
using osu.Framework.Input;
using osu.Framework.Input.Handlers;
using osu.Framework.Input.StateChanges;
using osu.Framework.Platform;

namespace Garbus.Game.Input
{
    public class RadialJoystickHandler : InputHandler
    {
        /// <summary>
        /// Radial magnitude (0..1) below which a stick reads as centred. Sits comfortably under
        /// <see cref="AnalogInputManager.SliderCatcher.DEADZONE"/> (0.4) so genuine catches are never
        /// affected, while still swallowing typical resting drift.
        /// </summary>
        public float RadialDeadzone { get; set; } = 0.2f;

        // Non-stick axes (triggers) are 1-D, so a per-axis deadzone can't "snap" a direction — a small
        // one just removes resting jitter.
        private const float trigger_deadzone = 0.02f;

        public override bool IsActive => true;
        public override string Description => "Joystick / Gamepad (radial deadzone)";

        // Latest raw value per axis source, so a change on one axis of a stick can be gated against the
        // other axis's current value.
        private readonly float[] rawValues = new float[(int)JoystickAxisSource.AxisCount];

        private object? window;
        private EventInfo? axisChangedEvent;
        private EventInfo? buttonDownEvent;
        private EventInfo? buttonUpEvent;
        private Delegate? axisChangedDelegate;
        private Delegate? buttonDownDelegate;
        private Delegate? buttonUpDelegate;

        public override bool Initialize(GameHost host)
        {
            if (!base.Initialize(host))
                return false;

            window = host.Window;
            if (window == null)
                return false; // headless — nothing to bind to.

            // The joystick events live on the internal ISDLWindow interface. Bind through the interface's
            // EventInfo so this works whether the concrete window implements it implicitly or explicitly.
            Type? sdlWindow = window.GetType().GetInterfaces().FirstOrDefault(i => i.Name == "ISDLWindow");
            axisChangedEvent = sdlWindow?.GetEvent("JoystickAxisChanged");
            buttonDownEvent = sdlWindow?.GetEvent("JoystickButtonDown");
            buttonUpEvent = sdlWindow?.GetEvent("JoystickButtonUp");

            if (axisChangedEvent == null || buttonDownEvent == null || buttonUpEvent == null)
                throw new InvalidOperationException(
                    $"{nameof(RadialJoystickHandler)} could not bind to the window's joystick events " +
                    $"(window type {window.GetType()}). The osu-framework input API may have changed.");

            // Public delegate types, so what we attach is an ordinary compiled method — reflection is only
            // used to locate/attach the handlers, never on the per-event path.
            axisChangedDelegate = new Action<JoystickAxisSource, float>(handleAxisChanged);
            buttonDownDelegate = new Action<JoystickButton>(button => enqueue(new JoystickButtonInput(button, true)));
            buttonUpDelegate = new Action<JoystickButton>(button => enqueue(new JoystickButtonInput(button, false)));

            Enabled.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                {
                    axisChangedEvent.AddEventHandler(window, axisChangedDelegate);
                    buttonDownEvent.AddEventHandler(window, buttonDownDelegate);
                    buttonUpEvent.AddEventHandler(window, buttonUpDelegate);
                }
                else
                {
                    axisChangedEvent.RemoveEventHandler(window, axisChangedDelegate);
                    buttonDownEvent.RemoveEventHandler(window, buttonDownDelegate);
                    buttonUpEvent.RemoveEventHandler(window, buttonUpDelegate);
                }
            }, true);

            return true;
        }

        private void handleAxisChanged(JoystickAxisSource source, float value)
        {
            rawValues[(int)source] = value;

            switch (source)
            {
                case JoystickAxisSource.GamePadLeftStickX:
                case JoystickAxisSource.GamePadLeftStickY:
                    enqueueStick(JoystickAxisSource.GamePadLeftStickX, JoystickAxisSource.GamePadLeftStickY);
                    break;

                case JoystickAxisSource.GamePadRightStickX:
                case JoystickAxisSource.GamePadRightStickY:
                    enqueueStick(JoystickAxisSource.GamePadRightStickX, JoystickAxisSource.GamePadRightStickY);
                    break;

                default:
                    // Trigger / generic axis: 1-D, so only strip resting jitter.
                    float gated = Math.Abs(value) < trigger_deadzone ? 0f : value;
                    enqueue(new JoystickAxisInput(new JoystickAxis(source, gated)));
                    break;
            }
        }

        // Emits both axes of a stick together so the whole vector is gated consistently — otherwise the
        // untouched axis would keep its last non-zero value in the framework state.
        private void enqueueStick(JoystickAxisSource xSource, JoystickAxisSource ySource)
        {
            (float x, float y) = ApplyRadialDeadzone(rawValues[(int)xSource], rawValues[(int)ySource], RadialDeadzone);

            enqueue(new JoystickAxisInput(new[]
            {
                new JoystickAxis(xSource, x),
                new JoystickAxis(ySource, y),
            }));
        }

        /// <summary>
        /// Zeroes the (<paramref name="x"/>, <paramref name="y"/>) stick vector when its magnitude is
        /// below <paramref name="threshold"/>; otherwise returns it unchanged, so the angle is never
        /// distorted above the deadzone.
        /// </summary>
        public static (float X, float Y) ApplyRadialDeadzone(float x, float y, float threshold)
        {
            if (threshold <= 0f)
                return (x, y);

            if (x * x + y * y < threshold * threshold)
                return (0f, 0f);

            return (x, y);
        }

        private void enqueue(IInput input) => PendingInputs.Enqueue(input);
    }
}
