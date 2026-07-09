// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/SelectionBoxButton.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; OsuColour replaced with ColourGrayF from
// SelectionBoxControl; no osu.Game dependency.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Compose
{
    public sealed partial class SelectionBoxButton : SelectionBoxControl, IHasTooltip
    {
        private SpriteIcon icon = null!;

        private readonly IconUsage iconUsage;

        public Action? Action;

        public event Action? Clicked;

        public event Action? HoverLost;

        public SelectionBoxButton(IconUsage iconUsage, string tooltip)
        {
            this.iconUsage = iconUsage;

            TooltipText = tooltip;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        protected override void LoadComplete()
        {
            Size = new Vector2(20);
            AddInternal(icon = new SpriteIcon
            {
                RelativeSizeAxes = Axes.Both,
                Size = new Vector2(0.5f),
                Icon = iconUsage,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });

            base.LoadComplete();
        }

        protected override bool OnClick(ClickEvent e)
        {
            Clicked?.Invoke();
            TriggerAction();
            return true;
        }

        protected override void UpdateHoverState()
        {
            base.UpdateHoverState();
            icon.FadeColour(!IsHeld && IsHovered ? Color4.White : Color4.Black, TRANSFORM_DURATION, Easing.OutQuint);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            HoverLost?.Invoke();
        }

        public LocalisableString TooltipText { get; }

        public void TriggerAction()
        {
            Circle.FlashColour(ColourGrayF, 300);

            TriggerOperationStarted();
            Action?.Invoke();
            TriggerOperationEnded();
        }
    }
}
