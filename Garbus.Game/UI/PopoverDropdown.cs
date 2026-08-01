using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;

namespace Garbus.Game.UI;

/// <summary>
/// A <see cref="BasicDropdown{T}"/> whose open menu pops over the UI below it, combo-box style,
/// instead of growing the dropdown's bounding box and reflowing everything south of it. Any
/// container that stacks content below one of these must draw earlier children in front of later
/// ones — see <see cref="FrontFirstFillFlowContainer"/> — or the open menu draws underneath the
/// content it overlaps.
/// </summary>
public partial class PopoverDropdown<T> : BasicDropdown<T>
{
    public PopoverDropdown()
    {
        Menu.BypassAutoSizeAxes = Axes.Y;
    }
}
