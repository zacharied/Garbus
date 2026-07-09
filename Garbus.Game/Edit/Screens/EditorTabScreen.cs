// Base class for the four editor tab screens. Shown/hidden by the shell without unloading.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace Garbus.Game.Edit.Screens
{
    /// <summary>Base for the four tab screens: shown/hidden by the shell, never unloaded.</summary>
    public abstract partial class EditorTabScreen : VisibilityContainer
    {
        protected override void PopIn() => this.FadeIn(200);
        protected override void PopOut() => this.FadeOut(200);
    }
}
