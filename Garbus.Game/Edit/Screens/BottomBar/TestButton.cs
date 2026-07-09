// Bespoke for Garbus.
// The "Test" button in the editor bottom bar. Enabled only when the chart has a real track
// (i.e., not a TrackVirtual). Wired to GarbusEditor.StartTestMode() in Task 19.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;

namespace Garbus.Game.Edit.Screens.BottomBar
{
    /// <summary>
    /// A named wrapper around <see cref="BasicButton"/> for the editor bottom-bar Test button.
    /// Named so tests can locate it via <c>ChildrenOfType&lt;TestButton&gt;()</c>.
    /// </summary>
    public partial class TestButton : BasicButton
    {
        public TestButton()
        {
            RelativeSizeAxes = Axes.Both;
            Text = "Test";
        }
    }
}
