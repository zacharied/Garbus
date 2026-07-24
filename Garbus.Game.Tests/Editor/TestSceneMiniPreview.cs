// The editor Mini preview: a non-interactive autoHit playfield hosted over the compose workspace,
// mirroring the editor's live hit objects on a clock slaved to the EditorClock.

using System.Linq;
using Garbus.Game.Input;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osuTK;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneMiniPreview : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        [Test]
        public void TestNonInteractivePlayfieldInstallsNoInput()
        {
            GarbusPlayfield preview = null!;
            AddStep("add non-interactive playfield", () =>
                Child = preview = new GarbusPlayfield(interactive: false) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("loaded", () => preview.IsLoaded);
            AddAssert("no analog input manager", () => !preview.ChildrenOfType<AnalogInputManager>().Any());
            AddAssert("no stick indicators", () => !preview.ChildrenOfType<StickIndicator>().Any());
        }

        [Test]
        public void TestInteractivePlayfieldStillInstallsInput()
        {
            GarbusPlayfield gameplay = null!;
            AddStep("add interactive playfield", () =>
                Child = gameplay = new GarbusPlayfield(interactive: true) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("loaded", () => gameplay.IsLoaded);
            AddAssert("has analog input manager", () => gameplay.ChildrenOfType<AnalogInputManager>().Any());
            AddAssert("has two stick indicators", () => gameplay.ChildrenOfType<StickIndicator>().Count() == 2);
        }
    }
}
