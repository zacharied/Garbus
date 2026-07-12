using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit;

/// <summary>
/// A transient full-playfield overlay for "Flip around angle…". While active it draws a vertical pivot
/// bar snapped to the angle grid under the cursor; a left-click commits the reflection about that angle
/// via the supplied callback, and right-click or Escape cancels. Inactive (and input-transparent) at all
/// other times so it never steals clicks from the blueprint stack it sits above.
/// </summary>
public partial class FlipPivotOverlay : CompositeDrawable
{
    private readonly Func<float, (float xFrac, int angleDeg)> snap;
    private readonly Box bar;

    private Action<int>? onCommit;
    private bool active;
    private bool moved;
    private int pivotAngle;

    public FlipPivotOverlay(Func<float, (float xFrac, int angleDeg)> snap)
    {
        this.snap = snap;
        RelativeSizeAxes = Axes.Both;
        Alpha = 0;

        InternalChild = bar = new Box
        {
            RelativeSizeAxes = Axes.Y,
            Width = 2,
            Colour = new Colour4(255, 204, 34, 255), // osu Yellow, matching selection accents.
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopCentre,
        };
    }

    public void Begin(Action<int> commit)
    {
        onCommit = commit;
        active = true;
        moved = false;
        Alpha = 1;
    }

    private void end()
    {
        active = false;
        moved = false;
        Alpha = 0;
        onCommit = null;
    }

    public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => active;

    protected override bool OnMouseMove(MouseMoveEvent e)
    {
        if (!active)
            return false;

        float localX = ToLocalSpace(e.ScreenSpaceMousePosition).X;
        (float xFrac, int angleDeg) = snap(localX / DrawWidth);
        pivotAngle = angleDeg;
        moved = true;
        bar.X = xFrac * DrawWidth;
        return true;
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (active && e.Button == MouseButton.Right)
        {
            end();
            return true;
        }

        return false; // let a left press flow through to OnClick.
    }

    protected override bool OnClick(ClickEvent e)
    {
        if (!active)
            return false;

        if (e.Button == MouseButton.Left && moved)
            onCommit?.Invoke(pivotAngle);

        end();
        return true;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (active && e.Key == Key.Escape)
        {
            end();
            return true;
        }

        return base.OnKeyDown(e);
    }
}
