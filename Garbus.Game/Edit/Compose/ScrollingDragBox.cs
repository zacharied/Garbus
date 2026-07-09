// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/ScrollingDragBox.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; ScrollingPlayfield and Playfield from
// Garbus.Game.Gameplay.UI.Scrolling and Garbus.Game.Gameplay.UI; IScrollingInfo from local namespace.

using System;
using osu.Framework.Input.Events;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Gameplay.UI.Scrolling;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A <see cref="DragBox"/> that scrolls along with the scrolling playfield.
    /// </summary>
    public partial class ScrollingDragBox : DragBox
    {
        public double MinTime { get; private set; }

        public double MaxTime { get; private set; }

        private double? startTime;

        private readonly ScrollingPlayfield playfield;

        public ScrollingDragBox(Playfield playfield)
        {
            this.playfield = playfield as ScrollingPlayfield ?? throw new ArgumentException($"Playfield must be of type {nameof(ScrollingPlayfield)} to use this class.", nameof(playfield));
        }

        public override void HandleDrag(MouseButtonEvent e)
        {
            base.HandleDrag(e);

            startTime ??= playfield.TimeAtScreenSpacePosition(e.ScreenSpaceMouseDownPosition);
            double endTime = playfield.TimeAtScreenSpacePosition(e.ScreenSpaceMousePosition);

            MinTime = Math.Min(startTime.Value, endTime);
            MaxTime = Math.Max(startTime.Value, endTime);

            var startPos = ToLocalSpace(playfield.ScreenSpacePositionAtTime(startTime.Value));
            var endPos = ToLocalSpace(playfield.ScreenSpacePositionAtTime(endTime));

            switch (playfield.ScrollingInfo.Direction.Value)
            {
                case ScrollingDirection.Up:
                case ScrollingDirection.Down:
                    Box.Y = Math.Min(startPos.Y, endPos.Y);
                    Box.Height = Math.Max(startPos.Y, endPos.Y) - Box.Y;
                    break;

                case ScrollingDirection.Left:
                case ScrollingDirection.Right:
                    Box.X = Math.Min(startPos.X, endPos.X);
                    Box.Width = Math.Max(startPos.X, endPos.X) - Box.X;
                    break;
            }
        }

        public override void Hide()
        {
            base.Hide();
            startTime = null;
        }
    }
}
