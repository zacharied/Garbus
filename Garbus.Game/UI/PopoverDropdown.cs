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
    /// <summary>
    /// Cap on the open menu's height — ten of the framework's 24px item rows.
    /// </summary>
    private const float max_menu_height = 240;

    public PopoverDropdown()
    {
        Menu.BypassAutoSizeAxes = Axes.Y;

        // A menu left at the framework default (unbounded) sizes itself to its whole item list: nothing
        // overflows its scroll container, so the wheel has nothing to move. For a list as long as the
        // editor's easing enum that menu is taller than the window, and the entries past the bottom edge
        // are unreachable — no wheel, no scrollbar. Capping the height gives the scroll container an
        // extent to work with.
        Menu.MaxHeight = max_menu_height;
    }
}
