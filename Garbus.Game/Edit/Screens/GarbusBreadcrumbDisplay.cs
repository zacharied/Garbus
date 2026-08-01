// Breadcrumb trail without the framework's caption — the path pieces already read as a path.

using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;

namespace Garbus.Game.Edit.Screens
{
    public partial class GarbusBreadcrumbDisplay : BasicDirectorySelectorBreadcrumbDisplay
    {
        protected override Drawable? CreateCaption() => null;
    }
}
