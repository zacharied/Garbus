// Directory selector whose show-hidden-items toggle lives in the hosting dialog's footer.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;

namespace Garbus.Game.Edit.Screens
{
    public partial class GarbusDirectorySelector : BasicDirectorySelector
    {
        /// <summary>
        /// Whether hidden directories are listed. Exposed because the framework keeps
        /// <c>ShowHiddenItems</c> protected, and the toggle now lives outside the selector.
        /// </summary>
        public BindableBool ShowHiddenDirectories => ShowHiddenItems;

        // No in-selector toggle: the hosting dialog owns the show-hidden-items checkbox.
        protected override Drawable CreateHiddenToggleButton() => Empty();
    }
}
