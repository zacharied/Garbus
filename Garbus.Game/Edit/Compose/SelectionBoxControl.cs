// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/SelectionBoxControl.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; OsuColour DI replaced with inline Color4
// constants (YellowDark=#eeaa00, Red=#ed1121, GrayF=#fff) so there is no osu.Game dependency.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// Represents the base appearance for UI controls of the <see cref="SelectionBox"/>,
    /// such as buttons.
    /// </summary>
    public abstract partial class SelectionBoxControl : CompositeDrawable
    {
        public const double TRANSFORM_DURATION = 100;

        // Inlined from OsuColour (osu.Game dependency dropped).
        protected static readonly Color4 ColourYellowDark = Color4Extensions.FromHex(@"eeaa00");
        protected static readonly Color4 ColourRed = Color4Extensions.FromHex(@"ed1121");
        protected static readonly Color4 ColourGrayF = Color4Extensions.FromHex(@"fff");

        public event Action? OperationStarted;
        public event Action? OperationEnded;

        protected Circle Circle { get; private set; } = null!;

        /// <summary>
        /// Whether the user is currently holding the control with mouse.
        /// </summary>
        public bool IsHeld { get; private set; }

        protected override void LoadComplete()
        {
            Origin = Anchor.Centre;

            InternalChildren = new Drawable[]
            {
                Circle = new Circle
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
            };

            base.LoadComplete();

            UpdateHoverState();
            FinishTransforms(true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            UpdateHoverState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            UpdateHoverState();
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            IsHeld = true;
            UpdateHoverState();
            return true;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            IsHeld = false;
            UpdateHoverState();
        }

        protected virtual void UpdateHoverState()
        {
            if (IsHeld)
                Circle.FadeColour(ColourGrayF, TRANSFORM_DURATION, Easing.OutQuint);
            else
                Circle.FadeColour(IsHovered ? ColourRed : ColourYellowDark, TRANSFORM_DURATION, Easing.OutQuint);

            this.ScaleTo(IsHeld || IsHovered ? 1.5f : 1, TRANSFORM_DURATION, Easing.OutQuint);
        }

        protected void TriggerOperationStarted() => OperationStarted?.Invoke();

        protected void TriggerOperationEnded() => OperationEnded?.Invoke();
    }
}
