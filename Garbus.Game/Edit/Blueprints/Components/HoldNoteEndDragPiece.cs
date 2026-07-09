// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/Components/HoldNoteEndDragPiece.cs).
// Verbatim aside from namespace.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osuTK;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>A draggable end handle on a hold note selection (the Garbus analogue of mania's EditHoldNoteEndPiece).</summary>
internal partial class HoldNoteEndDragPiece : CompositeDrawable
{
    public Action? DragStarted { get; init; }
    public Action<Vector2>? Dragging { get; init; }
    public Action? DragEnded { get; init; }

    public HoldNoteEndDragPiece()
    {
        InternalChild = new EditSquarePiece { RelativeSizeAxes = Axes.Both };
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
