// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/Components/EditSquarePiece.cs).
// OsuColour resolve removed — the border colour (osu's OsuColour.YellowDark) is inlined as
// new Colour4(255, 196, 40, 255).

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>The yellow outline box used by note blueprints (the Garbus analogue of mania's EditNotePiece).
/// Doubles as the slider head handle: draggable, and fills solid when selected. Non-head uses leave the
/// callbacks null and never receive input, so behaviour there is unchanged.</summary>
internal partial class EditSquarePiece : CompositeDrawable
{
    /// <summary>Invoked on left mouse-down with (control-point index, Ctrl-pressed). The head uses the
    /// sentinel index -1; the blueprint routes it to head selection.</summary>
    public Action<int, bool>? SelectRequested { get; init; }

    public Action? DragStarted { get; init; }
    public Action<int, Vector2>? Dragging { get; init; }
    public Action? DragEnded { get; init; }

    /// <summary>Index this handle stands over (-1 = the head). Reassigned every frame by the blueprint.</summary>
    public int CpIndex { get; set; } = -1;

    /// <summary>The wrap-copy this handle stands on (0 = raw/primary copy, non-zero = a ghost-band clone).</summary>
    public int WrapK { get; set; }

    /// <summary>When false, the handle declines mouse-down/drag (falls through to base) so the event bubbles
    /// to the blueprint container — used for a head-only slider's head, which is the whole object, not an
    /// independent node target. Still hit-testable (keeps the slider selectable and part of a group move).</summary>
    public bool InteractionEnabled { get; set; } = true;

    private readonly Box fill;

    private bool nodeSelected;

    /// <summary>True between a delivered <see cref="OnDragStart"/> and the single <see cref="DragEnded"/> that
    /// balances it — so the callback fires exactly once whether the drag ends normally or this handle is
    /// disposed mid-drag.</summary>
    private bool dragging;

    /// <summary>Whether this handle's node is selected; drives the solid fill.</summary>
    public bool NodeSelected
    {
        get => nodeSelected;
        set
        {
            if (nodeSelected == value)
                return;

            nodeSelected = value;
            fill.Alpha = value ? 1 : 0;
        }
    }

    public EditSquarePiece()
    {
        InternalChild = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            BorderThickness = 3,
            BorderColour = new Colour4(255, 196, 40, 255),
            Child = fill = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                AlwaysPresent = true,
            },
        };
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left || SelectRequested == null || !InteractionEnabled)
            return base.OnMouseDown(e);

        SelectRequested.Invoke(CpIndex, e.ControlPressed);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e)
    {
        if (DragStarted == null || !InteractionEnabled)
            return base.OnDragStart(e);

        dragging = true;
        DragStarted.Invoke();
        return true;
    }

    protected override void OnDrag(DragEvent e)
    {
        base.OnDrag(e);
        Dragging?.Invoke(CpIndex, e.ScreenSpaceMousePosition);
    }

    protected override void OnDragEnd(DragEndEvent e)
    {
        base.OnDragEnd(e);
        endDrag();
    }

    protected override void Dispose(bool isDisposing)
    {
        // The blueprint rebuilds its head handles every frame and disposes the trailing ones when a wrap
        // copy drops (SliderSelectionBlueprint.Update). That can dispose the handle currently being dragged,
        // and the framework never delivers OnDragEnd to a disposed drawable — so end the drag here to fire
        // the balancing DragEnded. Without it, the change transaction opened by the drag is stranded
        // (TransactionActive stuck true) and Undo/Redo lock up.
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
