// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/SelectionBox.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; SelectionRotationHandler/SelectionScaleHandler
// and all scale/rotation handle wiring removed entirely (BAC doesn't use them); OsuColour replaced
// with inline Color4 constants (YellowDark=#eeaa00, Gray0=#000); OsuSpriteText/OsuFont replaced with
// plain SpriteText (framework); CanFlipX/CanFlipY/CanReverse properties kept for completeness but
// BAC currently sets none of them; [Cached] attribute kept so BlueprintContainer can resolve the box.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Edit.Compose
{
    [Cached]
    public partial class SelectionBox : CompositeDrawable
    {
        public const float BORDER_RADIUS = 3;

        private const float button_padding = 5;

        // Inlined from OsuColour (osu.Game dependency dropped).
        private static readonly Color4 colour_yellow_dark = Color4Extensions.FromHex(@"eeaa00");
        private static readonly Color4 colour_gray0 = Color4Extensions.FromHex(@"000");

        public Func<Direction, bool, bool>? OnFlip;
        public Func<bool>? OnReverse;

        public Action? OperationStarted;
        public Action? OperationEnded;

        private SelectionBoxButton? reverseButton;

        private bool canReverse;

        /// <summary>
        /// Whether pattern reversing support should be enabled.
        /// </summary>
        public bool CanReverse
        {
            get => canReverse;
            set
            {
                if (canReverse == value) return;

                canReverse = value;
                recreateButtons();
            }
        }

        private bool canFlipX;

        /// <summary>
        /// Whether horizontal flipping support should be enabled.
        /// </summary>
        public bool CanFlipX
        {
            get => canFlipX;
            set
            {
                if (canFlipX == value) return;

                canFlipX = value;
                recreateButtons();
            }
        }

        private bool canFlipY;

        /// <summary>
        /// Whether vertical flipping support should be enabled.
        /// </summary>
        public bool CanFlipY
        {
            get => canFlipY;
            set
            {
                if (canFlipY == value) return;

                canFlipY = value;
                recreateButtons();
            }
        }

        private string text = string.Empty;

        public string Text
        {
            get => text;
            set
            {
                if (value == text)
                    return;

                text = value;
                if (selectionDetailsText != null)
                    selectionDetailsText.Text = value;
            }
        }

        private FillFlowContainer<SelectionBoxButton> buttons = null!;
        private SpriteText? selectionDetailsText;

        protected override void LoadComplete()
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    Name = "info text",
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Colour = colour_yellow_dark,
                            RelativeSizeAxes = Axes.Both,
                        },
                        selectionDetailsText = new SpriteText
                        {
                            Padding = new MarginPadding(2),
                            Colour = colour_gray0,
                            Font = FrameworkFont.Regular.With(size: 11),
                            Text = text,
                        }
                    }
                },
                new Container
                {
                    Masking = true,
                    BorderThickness = BORDER_RADIUS,
                    BorderColour = colour_yellow_dark,
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            AlwaysPresent = true,
                            Alpha = 0
                        },
                    }
                },
                buttons = new FillFlowContainer<SelectionBoxButton>
                {
                    AutoSizeAxes = Axes.X,
                    Height = 30,
                    Direction = FillDirection.Horizontal,
                    Margin = new MarginPadding(button_padding),
                }
            };

            base.LoadComplete();

            recreateButtons();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat || !e.ControlPressed)
                return false;

            switch (e.Key)
            {
                case Key.G:
                    if (!CanReverse || reverseButton == null)
                        return false;

                    reverseButton.TriggerAction();
                    return true;
            }

            return base.OnKeyDown(e);
        }

        protected override void Update()
        {
            base.Update();
            ensureButtonsOnScreen();
        }

        private void recreateButtons()
        {
            if (LoadState < LoadState.Loading)
                return;

            clearButtons();

            if (CanFlipX)
                addButton(FontAwesome.Solid.ArrowsAltH, "Flip horizontally", () => OnFlip?.Invoke(Direction.Horizontal, false));

            if (CanFlipY)
                addButton(FontAwesome.Solid.ArrowsAltV, "Flip vertically", () => OnFlip?.Invoke(Direction.Vertical, false));

            if (CanReverse)
                reverseButton = addButton(FontAwesome.Solid.Backward, "Reverse pattern (Ctrl-G)", () => OnReverse?.Invoke());
        }

        private SelectionBoxButton addButton(IconUsage icon, string tooltip, Action action)
        {
            var button = new SelectionBoxButton(icon, tooltip)
            {
                Action = action
            };

            button.Clicked += freezeButtonPosition;
            button.HoverLost += unfreezeButtonPosition;

            button.OperationStarted += operationStarted;
            button.OperationEnded += operationEnded;

            buttons.Add(button);

            return button;
        }

        private void clearButtons()
        {
            foreach (var button in buttons)
            {
                button.Clicked -= freezeButtonPosition;
                button.HoverLost -= unfreezeButtonPosition;

                button.OperationStarted -= operationStarted;
                button.OperationEnded -= operationEnded;
            }

            unfreezeButtonPosition();
            buttons.Clear();
        }

        private int activeOperations;

        private void operationEnded()
        {
            if (--activeOperations == 0)
                OperationEnded?.Invoke();
        }

        private void operationStarted()
        {
            if (activeOperations++ == 0)
                OperationStarted?.Invoke();
        }

        private Vector2? frozenButtonsPosition;

        private void freezeButtonPosition()
        {
            frozenButtonsPosition = buttons.ScreenSpaceDrawQuad.TopLeft;
        }

        private void unfreezeButtonPosition()
        {
            if (frozenButtonsPosition != null)
            {
                frozenButtonsPosition = null;
                ensureButtonsOnScreen(true);
            }
        }

        private void ensureButtonsOnScreen(bool animated = false)
        {
            if (frozenButtonsPosition != null)
            {
                buttons.Anchor = Anchor.TopLeft;
                buttons.Origin = Anchor.TopLeft;

                buttons.Position = ToLocalSpace(frozenButtonsPosition.Value) - new Vector2(button_padding);
                return;
            }

            if (!animated && buttons.Transforms.Any())
                return;

            var thisQuad = ScreenSpaceDrawQuad;

            // Shrink the parent quad to give a bit of padding so the buttons don't stick *right* on the border.
            var parentQuad = Parent!.ScreenSpaceDrawQuad.AABBFloat.Shrink(ToLocalSpace(thisQuad.TopLeft + new Vector2(button_padding * 2)));

            float topExcess = thisQuad.TopLeft.Y - parentQuad.TopLeft.Y;
            float bottomExcess = parentQuad.BottomLeft.Y - thisQuad.BottomLeft.Y;
            float leftExcess = thisQuad.TopLeft.X - parentQuad.TopLeft.X;
            float rightExcess = parentQuad.TopRight.X - thisQuad.TopRight.X;

            float minHeight = buttons.ScreenSpaceDrawQuad.Height;

            Anchor targetAnchor;
            Anchor targetOrigin;
            Vector2 targetPosition = Vector2.Zero;

            if (topExcess < minHeight && bottomExcess < minHeight)
            {
                targetAnchor = Anchor.BottomCentre;
                targetOrigin = Anchor.BottomCentre;
                targetPosition.Y = Math.Min(0, ToLocalSpace(Parent!.ScreenSpaceDrawQuad.BottomLeft).Y - DrawHeight);
            }
            else if (topExcess > bottomExcess)
            {
                targetAnchor = Anchor.TopCentre;
                targetOrigin = Anchor.BottomCentre;
            }
            else
            {
                targetAnchor = Anchor.BottomCentre;
                targetOrigin = Anchor.TopCentre;
            }

            targetPosition.X += ToLocalSpace(thisQuad.TopLeft - new Vector2(Math.Min(0, leftExcess)) + new Vector2(Math.Min(0, rightExcess))).X;

            if (animated)
            {
                var originalPosition = ToLocalSpace(buttons.ScreenSpaceDrawQuad.TopLeft);

                buttons.Origin = targetOrigin;
                buttons.Anchor = targetAnchor;
                buttons.Position = targetPosition;

                var newPosition = ToLocalSpace(buttons.ScreenSpaceDrawQuad.TopLeft);

                var delta = newPosition - originalPosition;

                buttons.Position -= delta;

                buttons.MoveTo(targetPosition, 300, Easing.OutQuint);
            }
            else
            {
                buttons.Anchor = targetAnchor;
                buttons.Origin = targetOrigin;
                buttons.Position = targetPosition;
            }
        }
    }
}
