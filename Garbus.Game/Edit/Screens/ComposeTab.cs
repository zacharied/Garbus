// The Compose tab: hosts the GarbusHitObjectComposer (editor timeline playfield + toolbox + blueprints).
// The editor shell (GarbusEditor) DI-caches EditorChart / EditorClock / IEditorChangeHandler /
// BindableBeatDivisor, which the composer tree resolves. The top timeline strip slot stays empty until
// Task 17 wires the timeline + zoom sync.

using Garbus.Game.Edit;
using osu.Framework.Graphics;

namespace Garbus.Game.Edit.Screens
{
    public partial class ComposeTab : EditorTabScreen
    {
        public ComposeTab()
        {
            RelativeSizeAxes = Axes.Both;

            // Timeline strip slot (top) stays empty until Task 17; the composer fills the rest.
            AddInternal(new GarbusHitObjectComposer { RelativeSizeAxes = Axes.Both });
        }
    }
}
