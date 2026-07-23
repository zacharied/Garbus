// Visual + behaviour test for the song-select detail panel: showing a card populates its fields and
// reports no background for a textureless card; the empty state clears the displayed card.

using System.Linq;
using Garbus.Game.Input;
using Garbus.Game.Screens.SongSelect;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneChartDetailPanel : GarbusTestScene
    {
        private ChartDetailPanel panel = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create panel", () => Child = panel = new ChartDetailPanel { RelativeSizeAxes = Axes.Both });
        }

        [Test]
        public void TestShowCardPopulatesFields()
        {
            var card = new ChartCard { Source = null!, Locator = "l", GroupKey = "g", Title = "My Song", Artist = "My Artist", ChartName = "Insane", Level = 7 };

            AddStep("show card", () => panel.Show(card, null));
            AddAssert("displayed card set", () => panel.DisplayedCard == card);
            AddAssert("no background (placeholder)", () => !panel.HasBackground);
            AddAssert("title rendered", () => this.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == "My Song"));
            AddAssert("artist rendered", () => this.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == "My Artist"));
        }

        [Test]
        public void TestPlayButtonShowsGamepadGlyph()
        {
            // The play prompt embeds the live face-south (Cross) glyph, and its texture actually resolves.
            AddAssert("play prompt shows face-south glyph", () =>
            {
                var sprite = panel.ChildrenOfType<GamepadButtonSprite>().SingleOrDefault();
                return sprite != null && sprite.Texture != null;
            });
        }

        [Test]
        public void TestEmptyStateClearsCard()
        {
            var card = new ChartCard { Source = null!, Locator = "l", GroupKey = "g", Title = "My Song", Artist = "My Artist", Level = 1 };

            AddStep("show card", () => panel.Show(card, null));
            AddStep("show empty", () => panel.Show(null, null));
            AddAssert("no displayed card", () => panel.DisplayedCard == null);
        }
    }
}
