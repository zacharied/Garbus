// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/Components/NodeDragPiece.cs).
// OsuColour resolve removed — the border colour (osu's OsuColour.YellowDark) is inlined as
// new Colour4(255, 196, 40, 255), matching EditSquarePiece.

using System;
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
    public Action? DragStarted { get; init; }
    public Action<int, Vector2>? Dragging { get; init; }
    public Action? DragEnded { get; init; }

    /// <summary>Invoked on left mouse-down with (control-point index, Ctrl-pressed) so the blueprint can update node selection.</summary>
    public Action<int, bool>? SelectRequested { get; init; }

    /// <summary>Index of the control point this handle stands over — reassigned every frame as the path changes.</summary>
    public int CpIndex { get; set; }

    /// <summary>The wrap-copy this handle stands on (0 = raw/primary outline copy, non-zero = a ghost-band clone).</summary>
    public int WrapK { get; set; }

    private readonly Box fill;

    private bool nodeSelected;

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
        DragStarted?.Invoke();
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
        DragEnded?.Invoke();
    }
}
