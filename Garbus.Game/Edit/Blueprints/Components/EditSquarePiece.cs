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

    private readonly Box fill;

    private bool nodeSelected;

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
        if (e.Button != MouseButton.Left || SelectRequested == null)
            return base.OnMouseDown(e);

        SelectRequested.Invoke(CpIndex, e.ControlPressed);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e)
    {
        if (DragStarted == null)
            return base.OnDragStart(e);

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
        DragEnded?.Invoke();
    }
}
