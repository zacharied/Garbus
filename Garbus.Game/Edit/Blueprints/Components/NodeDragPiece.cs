// The border colour is inlined as new Colour4(255, 196, 40, 255), matching EditSquarePiece.

using System;
using Garbus.Game.Edit.Drawables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>A draggable circle handle over a slider control-point node. Fills solid when its node is selected.</summary>
internal partial class NodeDragPiece : CompositeDrawable
{
    /// <summary>Invoked on drag start with the control-point index this handle stands over at that instant —
    /// captured once so the drag stays bound to that node even as <see cref="CpIndex"/> is reshuffled by
    /// later wrap-copy changes.</summary>
    public Action<int>? DragStarted { get; init; }
    public Action<Vector2>? Dragging { get; init; }
    public Action? DragEnded { get; init; }

    /// <summary>Invoked on left mouse-down with (control-point index, Ctrl-pressed) so the blueprint can update node selection.</summary>
    public Action<int, bool>? SelectRequested { get; init; }

    /// <summary>Index of the control point this handle stands over — reassigned every frame as the path changes.</summary>
    public int CpIndex { get; set; }

    /// <summary>The wrap-copy this handle stands on (0 = raw/primary outline copy, non-zero = a ghost-band clone).</summary>
    public int WrapK { get; set; }

    private readonly Box fill;
    private readonly Circle shapeOnlyDot;

    private bool nodeSelected;
    private bool shapeOnly;

    /// <summary>True between a delivered <see cref="OnDragStart"/> and the single <see cref="DragEnded"/> that
    /// balances it — so the callback fires exactly once whether the drag ends normally or this handle is
    /// disposed mid-drag.</summary>
    private bool dragging;

    /// <summary>Whether this handle's node is currently selected; drives the solid fill.</summary>
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

    /// <summary>
    /// Whether this handle's node is shape-only; draws the punch-out dot above the selected fill so a
    /// mixed multi-node selection keeps the judged-vs-shape distinction the fill would otherwise cover.
    /// Reassigned every frame alongside <see cref="CpIndex"/>.
    /// </summary>
    public bool ShapeOnly
    {
        get => shapeOnly;
        set
        {
            if (shapeOnly == value)
                return;

            shapeOnly = value;
            shapeOnlyDot.Alpha = value ? 1 : 0;
        }
    }

    /// <summary>Whether the shape-only dot is currently drawn (test seam for the mixed-selection relation).</summary>
    public bool ShapeOnlyDotVisible => shapeOnlyDot.Alpha > 0;

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
            Children = new Drawable[]
            {
                fill = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    AlwaysPresent = true,
                },
                shapeOnlyDot = new Circle
                {
                    Size = new Vector2(6),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = SliderNodeMarker.PunchOutColour,
                    Alpha = 0,
                },
            },
        };
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return base.OnMouseDown(e);

        // Select on the press so a plain click and a drag both start from this node being selected.
        // Returning true stops BlueprintContainer.performMouseDownActions from re-selecting / cycling the
        // whole slider; the slider is already selected (handles only receive input while it is).
        SelectRequested?.Invoke(CpIndex, e.ControlPressed);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e)
    {
        dragging = true;
        DragStarted?.Invoke(CpIndex);
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
        // The blueprint rebuilds its node handles every frame and disposes the trailing ones when a wrap
        // copy drops (SliderSelectionBlueprint.Update). That can dispose the handle currently being dragged,
        // and the framework never delivers OnDragEnd to a disposed drawable — so end the drag here to fire
        // the balancing DragEnded. Without it, the change transaction opened by beginNodeDrag is stranded
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
