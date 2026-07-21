// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/Components/HoldEndDragPiece.cs).
// Verbatim aside from namespace.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osuTK;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>A draggable end handle on a hold note selection (the Garbus analogue of mania's EditHoldNoteEndPiece).</summary>
internal partial class HoldEndDragPiece : CompositeDrawable
{
    public Action? DragStarted { get; init; }
    public Action<Vector2>? Dragging { get; init; }
    public Action? DragEnded { get; init; }

    /// <summary>True between a delivered <see cref="OnDragStart"/> and the single <see cref="DragEnded"/> that
    /// balances it — so the callback fires exactly once whether the drag ends normally or this handle is
    /// disposed mid-drag.</summary>
    private bool dragging;

    public HoldEndDragPiece()
    {
        InternalChild = new EditSquarePiece { RelativeSizeAxes = Axes.Both };
    }

    protected override bool OnDragStart(DragStartEvent e)
    {
        dragging = true;
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
        endDrag();
    }

    protected override void Dispose(bool isDisposing)
    {
        // If this handle is torn down mid-drag (its blueprint disposed while a drag is live), the framework
        // never delivers OnDragEnd to the disposed drawable — so end the drag here to fire the balancing
        // DragEnded. Without it, the change transaction opened by the drag is stranded (TransactionActive
        // stuck true) and Undo/Redo lock up.
        endDrag();
        base.Dispose(isDisposing);
    }

    private void endDrag()
    {
        if (!dragging)
            return;

        dragging = false;
        DragEnded?.Invoke();
    }
}
