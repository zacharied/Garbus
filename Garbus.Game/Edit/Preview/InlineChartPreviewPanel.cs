using System;
using Garbus.Game.Configuration;
using Garbus.Game.Gameplay.UI.Scrolling;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Edit.Preview;

internal partial class InlineChartPreviewPanel : CompositeDrawable
{
    public const float SIZE = 190;

    private EditorChart editorChart = null!;
    private EditorClock editorClock = null!;
    private GarbusChartChangeHandler changeHandler = null!;
    private GarbusScrollingInfo scrollingInfo = null!;
    private InlineChartPreviewController? controller;
    private GarbusConfigManager config = null!;
    private Vector2 offset = new Vector2(5);
    private Vector2 dragOrigin;
    private Vector2 dragStartOffset;

    public InlineChartPreviewPanel()
    {
        Anchor = Anchor.BottomRight;
        Origin = Anchor.BottomRight;
        Position = new Vector2(-5);
        Size = new Vector2(SIZE);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 2;
        BorderColour = new Color4(78, 118, 190, 255);
        Alpha = 0;
    }

    internal ChartPreviewContent ViewForTests { get; private set; } = null!;

    public event Action<string>? PreviewFailed;

    [BackgroundDependencyLoader]
    private void load(
        EditorChart editorChart,
        EditorClock editorClock,
        GarbusChartChangeHandler changeHandler,
        GarbusScrollingInfo scrollingInfo,
        GarbusConfigManager config)
    {
        this.editorChart = editorChart;
        this.editorClock = editorClock;
        this.changeHandler = changeHandler;
        this.scrollingInfo = scrollingInfo;
        this.config = config;
        offset = new Vector2(
            Math.Max(0, config.GetBindable<float>(GarbusSetting.MiniPreviewX).Value),
            Math.Max(0, config.GetBindable<float>(GarbusSetting.MiniPreviewY).Value));
        clampOffset();
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        clampOffset();
    }

    protected override void Update()
    {
        base.Update();
        clampOffset();
    }

    private void clampOffset()
    {
        if (Parent == null
            || DrawWidth <= 0
            || DrawHeight <= 0
            || Parent.DrawWidth < DrawWidth
            || Parent.DrawHeight < DrawHeight)
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

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            ensurePreview();
            Alpha = 1;
            controller!.Open();
        }
        else
        {
            controller?.Close();
            Alpha = 0;
        }
    }

    private void ensurePreview()
    {
        if (controller != null)
            return;

        ViewForTests = new ChartPreviewContent
        {
            RelativeSizeAxes = Axes.Both,
        };

        controller = new InlineChartPreviewController(
            editorChart,
            editorClock,
            changeHandler,
            scrollingInfo,
            ViewForTests);
        controller.PreviewFailed += onPreviewFailed;

        AddInternal(new DrawSizePreservingFillContainer
        {
            RelativeSizeAxes = Axes.Both,
            TargetDrawSize = new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE),
            Children = [ViewForTests],
        });
        AddInternal(controller);
    }

    private void onPreviewFailed(string error)
    {
        Alpha = 0;
        PreviewFailed?.Invoke(error);
    }
}
