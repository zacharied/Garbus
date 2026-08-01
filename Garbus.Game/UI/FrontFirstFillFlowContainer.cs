using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace Garbus.Game.UI;

/// <summary>
/// A fill flow whose earlier children draw in front of later ones, so a drawable that spills past
/// its own bounds — an open <see cref="PopoverDropdown{T}"/> menu — covers the children after it
/// instead of being drawn underneath them. Flow order is unaffected: a
/// <see cref="FillFlowContainer"/> lays out by layout position and insertion order, never by depth.
/// Input follows draw order, so the spilling drawable also receives clicks before what it covers.
/// </summary>
public partial class FrontFirstFillFlowContainer : FillFlowContainer
{
    private int addedCount;

    public override void Add(Drawable drawable)
    {
        drawable.Depth = addedCount++;
        base.Add(drawable);
    }
}
