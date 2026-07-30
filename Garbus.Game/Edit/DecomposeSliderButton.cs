// Bespoke for Garbus.
// The inspector's "Decompose into heads" action button, shown when the selection contains a slider with a
// path. Named so tests can locate it via ChildrenOfType<DecomposeSliderButton>(); the Action is wired by
// the Inspector, which owns the chart/change-handler/beat-divisor the operation needs.

using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;

namespace Garbus.Game.Edit
{
    public partial class DecomposeSliderButton : BasicButton
    {
        public DecomposeSliderButton()
        {
            RelativeSizeAxes = Axes.X;
            Height = 26;
            Text = "Decompose into heads";
            BackgroundColour = new Colour4(70, 70, 82, 255);
        }
    }
}
