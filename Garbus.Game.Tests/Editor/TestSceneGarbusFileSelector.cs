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
        private string? tempDirectory;

        [TearDown]
        public void TearDown()
        {
            if (tempDirectory != null && Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void TestSelectedFileHasVisibleHighlight()
        {
            GarbusFileSelector selector = null!;
            ManualInputManager input = null!;
            BasicButton hiddenToggle = null!;

            AddStep("create selector with two files", () =>
            {
                string directory = tempDirectory = Directory.CreateTempSubdirectory("garbus-files-").FullName;
                File.WriteAllText(Path.Combine(directory, "first.mp3"), string.Empty);
                File.WriteAllText(Path.Combine(directory, "second.mp3"), string.Empty);

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = selector = new GarbusFileSelector(directory, new[] { ".mp3" })
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            });

            AddUntilStep("file rows loaded", () => fileItems(selector).Count == 2);
            AddStep("capture hidden toggle", () => hiddenToggle = selector.ChildrenOfType<BasicButton>().Single());
            AddAssert("toggle offers to show hidden files", () => hiddenToggle.Text.ToString() == "Show hidden files");
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

            DirectorySelectorItem selectedRowBeforeRefresh = null!;
            AddStep("capture selected row", () => selectedRowBeforeRefresh = fileItem(selector, "second.mp3"));
            AddStep("show hidden files", () => hiddenToggle.TriggerClick());
            AddUntilStep("file rows rebuilt", () => !ReferenceEquals(selectedRowBeforeRefresh, fileItem(selector, "second.mp3")));
            AddAssert("toggle offers to hide hidden files", () => hiddenToggle.Text.ToString() == "Hide hidden files");
            AddUntilStep("selected file remains highlighted", () => isHighlighted(fileItem(selector, "second.mp3")));

            AddStep("hide hidden files", () => hiddenToggle.TriggerClick());
            AddAssert("toggle offers to show hidden files again", () => hiddenToggle.Text.ToString() == "Show hidden files");
            AddUntilStep("selected file still highlighted", () => isHighlighted(fileItem(selector, "second.mp3")));
        }

        private static System.Collections.Generic.List<DirectorySelectorItem> fileItems(GarbusFileSelector selector) =>
            selector.ChildrenOfType<DirectorySelectorItem>()
                    .Where(item => item.ChildrenOfType<SpriteText>().Any(text => text.Text.ToString().EndsWith(".mp3")))
                    .ToList();

        private static DirectorySelectorItem fileItem(GarbusFileSelector selector, string name) =>
            fileItems(selector).Single(item => item.ChildrenOfType<SpriteText>().Any(text => text.Text.ToString() == name));

        private static bool isHighlighted(DirectorySelectorItem item)
        {
            var background = item.ChildrenOfType<Box>().Single();
            var itemBounds = item.ScreenSpaceDrawQuad.AABBFloat;
            var backgroundBounds = background.ScreenSpaceDrawQuad.AABBFloat;

            return background.Colour.Equals((ColourInfo)GarbusFileSelector.SELECTION_COLOUR)
                   && System.Math.Abs(backgroundBounds.Left - itemBounds.Left) < 1
                   && System.Math.Abs(backgroundBounds.Right - itemBounds.Right) < 1
                   && System.Math.Abs(backgroundBounds.Top - itemBounds.Top) < 1
                   && System.Math.Abs(backgroundBounds.Bottom - itemBounds.Bottom) < 1;
        }
    }
}
