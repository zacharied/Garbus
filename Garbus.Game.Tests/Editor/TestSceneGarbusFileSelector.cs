using System.IO;
using System.Linq;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osuTK.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneGarbusFileSelector : GarbusTestScene
    {
        [Test]
        public void TestSelectedFileHasVisibleHighlight()
        {
            string tempDirectory = null!;
            GarbusFileSelector selector = null!;
            ManualInputManager input = null!;

            AddStep("create selector with two files", () =>
            {
                tempDirectory = Directory.CreateTempSubdirectory("garbus-files-").FullName;
                File.WriteAllText(Path.Combine(tempDirectory, "first.mp3"), string.Empty);
                File.WriteAllText(Path.Combine(tempDirectory, "second.mp3"), string.Empty);

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = selector = new GarbusFileSelector(tempDirectory, new[] { ".mp3" })
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            });

            AddUntilStep("file rows loaded", () => fileItems(selector).Count == 2);
            AddAssert("no row initially highlighted", () => fileItems(selector).All(item => !isHighlighted(item)));

            AddStep("click first file", () =>
            {
                input.MoveMouseTo(fileItem(selector, "first.mp3"));
                input.Click(MouseButton.Left);
            });
            AddUntilStep("first file highlighted", () =>
                isHighlighted(fileItem(selector, "first.mp3")));
            AddAssert("second file not highlighted", () =>
                !isHighlighted(fileItem(selector, "second.mp3")));

            AddStep("click second file", () =>
            {
                input.MoveMouseTo(fileItem(selector, "second.mp3"));
                input.Click(MouseButton.Left);
            });
            AddUntilStep("selection highlight moved", () =>
                !isHighlighted(fileItem(selector, "first.mp3"))
                && isHighlighted(fileItem(selector, "second.mp3")));

            AddStep("cleanup", () => Directory.Delete(tempDirectory, true));
        }

        private static System.Collections.Generic.List<DirectorySelectorItem> fileItems(GarbusFileSelector selector) =>
            selector.ChildrenOfType<DirectorySelectorItem>()
                    .Where(item => item.ChildrenOfType<SpriteText>().Any(text => text.Text.ToString().EndsWith(".mp3")))
                    .ToList();

        private static DirectorySelectorItem fileItem(GarbusFileSelector selector, string name) =>
            fileItems(selector).Single(item => item.ChildrenOfType<SpriteText>().Any(text => text.Text.ToString() == name));

        private static bool isHighlighted(DirectorySelectorItem item) =>
            item.ChildrenOfType<Box>().Single().Colour.Equals((ColourInfo)GarbusFileSelector.SELECTION_COLOUR);
    }
}
