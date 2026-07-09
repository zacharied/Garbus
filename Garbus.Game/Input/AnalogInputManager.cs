// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Input/AnalogInputManager.cs).

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using Garbus.Game.Core;

namespace Garbus.Game.Input;

public partial class AnalogInputManager : Drawable
{
    public readonly ImmutableDictionary<HorizontalDirection, SliderCatcher> SliderCatchers = new Dictionary<HorizontalDirection, SliderCatcher> {
        [HorizontalDirection.Left] = new(JoystickAxisSource.GamePadLeftStickX, JoystickAxisSource.GamePadLeftStickY, HorizontalDirection.Left),
        [HorizontalDirection.Right] = new(JoystickAxisSource.GamePadRightStickX, JoystickAxisSource.GamePadRightStickY, HorizontalDirection.Right),
    }.ToImmutableDictionary();

    protected override bool OnJoystickAxisMove(JoystickAxisMoveEvent e)
    {
        if (SliderCatchers[HorizontalDirection.Left].OnJoystickAxisMove(e))
            return true;
        if (SliderCatchers[HorizontalDirection.Right].OnJoystickAxisMove(e))
            return true;

        return false;
    }

    public class SliderCatcher
    {
        public const float DEADZONE = 0.4f;

        public int SizeDeg => 72;
        public bool Activated { get; private set; } = false;

        public HorizontalDirection Side { get; private set; }
        public float Angle { get; private set; }

        private readonly JoystickAxisSource xAxis, yAxis;
        private float xAxisLast, yAxisLast;

        private Vector2 joystickPosition => new Vector2(xAxisLast, yAxisLast);
        public float Size => SizeDeg * MathF.PI / 180f;

        public SliderCatcher(JoystickAxisSource xAxis, JoystickAxisSource yAxis, HorizontalDirection side)
        {
            this.xAxis = xAxis;
            this.yAxis = yAxis;
            this.Side = side;
        }

        public bool OnJoystickAxisMove(JoystickAxisMoveEvent e)
        {
            bool ret = false;

            if (e.Axis.Source == xAxis)
            {
                xAxisLast = e.Axis.Value;
                ret = true;
            }
            else if (e.Axis.Source == yAxis)
            {
                yAxisLast = e.Axis.Value;
                ret = true;
            }

            Angle = MathF.Atan2(-joystickPosition.Y, joystickPosition.X);
            Activated = joystickPosition.Length() > DEADZONE;

            return ret;
        }

        public bool IsCatchingAt(int angleDeg)
        {
            if (!Activated)
                return false;

            float target = angleDeg * MathF.PI / 180f;

            // shortest signed angular distance between the catcher and the target, wrapped to (-π, π]
            float delta = target - Angle;
            delta -= MathF.Tau * MathF.Floor((delta + MathF.PI) / MathF.Tau);

            return MathF.Abs(delta) < Size / 2f;
        }
    }
}
