// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/Components/NodeDragPiece.cs).
// OsuColour resolve removed — the border colour (osu's OsuColour.YellowDark) is inlined as
// new Colour4(255, 196, 40, 255), matching EditSquarePiece.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>A draggable circle handle over a slider control-point node.</summary>
internal partial class NodeDragPiece : CompositeDrawable
{
    public Action? DragStarted { get; init; }
    public Action<Vector2>? Dragging { get; init; }
    public Action? DragEnded { get; init; }

    public NodeDragPiece()
    {
        Size = new Vector2(16);
        Origin = Anchor.Centre;
        InternalChild = new CircularContainer
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            BorderThickness = 3,
            BorderColour = new Colour4(255, 196, 40, 255),
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                AlwaysPresent = true,
            },
        };
    }

    protected override bool OnDragStart(DragStartEvent e)
    {
        DragStarted?.Invoke();
        return true;
    }

    protected override void OnDrag(DragEvent e)
    {
        base.OnDrag(e);
        Dragging?.Invoke(e.ScreenSpaceMousePosition);
    }

    protected override void OnDragEnd(DragEndEvent e)
    {
        base.OnDragEnd(e);
        DragEnded?.Invoke();
    }
}
