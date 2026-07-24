// The draggable docked chrome for the Mini preview: bottom-right anchored, clamped to the compose
// workspace, with persisted right/bottom offsets. Lazily hosts a MiniPreview on first show.

using System;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Edit.Preview
{
    /// <summary>
    /// The draggable docked chrome for the Mini preview: bottom-right anchored, clamped to the compose
    /// workspace, with persisted right/bottom offsets. Lazily hosts a <see cref="MiniPreview"/> on first show.
    /// </summary>
    public partial class InlineChartPreviewPanel : CompositeDrawable
    {
        public const float SIZE = 190;

        private GarbusConfigManager config = null!;
        private MiniPreview? preview;

        // Distance from the parent's right/bottom edges. Position is derived from this every frame
        // via clampOffset(), which also keeps the panel inside the parent bounds.
        private Vector2 offset = new Vector2(5);
        private Vector2 dragOrigin;
        private Vector2 dragStartOffset;

        internal Vector2 OffsetForTests => offset;

        internal void SetOffsetForTests(Vector2 value)
        {
            offset = value;
            clampOffset();
        }

        public InlineChartPreviewPanel()
        {
            Anchor = Anchor.BottomRight;
            Origin = Anchor.BottomRight;
            Size = new Vector2(SIZE);
            Masking = true;
            CornerRadius = 8;
            BorderThickness = 2;
            BorderColour = new Color4(78, 118, 190, 255);
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(GarbusConfigManager config)
        {
            this.config = config;
            offset = new Vector2(
                Math.Max(0, config.Get<float>(GarbusSetting.MiniPreviewX)),
                Math.Max(0, config.Get<float>(GarbusSetting.MiniPreviewY)));
            clampOffset();
        }

        protected override void Update()
        {
            base.Update();
            clampOffset();
        }

        private void clampOffset()
        {
            if (Parent == null || DrawWidth <= 0 || DrawHeight <= 0
                || Parent.DrawWidth < DrawWidth || Parent.DrawHeight < DrawHeight)
                return;

            offset.X = Math.Clamp(offset.X, 0, Math.Max(0, Parent.DrawWidth - DrawWidth));
            offset.Y = Math.Clamp(offset.Y, 0, Math.Max(0, Parent.DrawHeight - DrawHeight));
            Position = -offset;
        }

        protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left || Parent == null)
                return false;

            dragOrigin = Parent.ToLocalSpace(e.ScreenSpaceMouseDownPosition);
            dragStartOffset = offset;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (Parent == null)
                return;

            Vector2 current = Parent.ToLocalSpace(e.ScreenSpaceMousePosition);
            offset = dragStartOffset - (current - dragOrigin);
            clampOffset();
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            config.SetValue(GarbusSetting.MiniPreviewX, offset.X);
            config.SetValue(GarbusSetting.MiniPreviewY, offset.Y);
        }

        protected override bool OnScroll(ScrollEvent e) => true;

        /// <summary>Shows or hides the panel, lazily constructing the hosted <see cref="MiniPreview"/> on first show.</summary>
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                if (preview == null)
                    AddInternal(preview = new MiniPreview { RelativeSizeAxes = Axes.Both });
                Alpha = 1;
            }
            else
            {
                Alpha = 0;
            }
        }
    }
}
