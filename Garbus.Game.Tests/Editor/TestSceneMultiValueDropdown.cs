using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osu.Framework.Utils;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneMultiValueDropdown : GarbusTestScene
    {
        private MultiValueEnumDropdown<HorizontalDirection> dropdown = null!;
        private HorizontalDirection? lastChange;

        private void build(MultiValue<HorizontalDirection> state)
        {
            lastChange = null;
            Child = dropdown = new MultiValueEnumDropdown<HorizontalDirection>(state, v => lastChange = v)
            {
                RelativeSizeAxes = Axes.X,
            };
        }

        [Test]
        public void Mixed_SelectsMixedSentinel()
        {
            AddStep("build mixed", () => build(new MultiValue<HorizontalDirection>(isMixed: true, default)));
            AddAssert("current is Mixed", () => dropdown.Current.Value.IsMixed);
            AddAssert("items include Mixed", () => dropdown.Items.Any(i => i.IsMixed));
        }

        [Test]
        public void Shared_SelectsValue_NoSentinel()
        {
            AddStep("build shared Right",
                () => build(new MultiValue<HorizontalDirection>(isMixed: false, HorizontalDirection.Right)));
            AddAssert("current is Right",
                () => !dropdown.Current.Value.IsMixed && dropdown.Current.Value.Value == HorizontalDirection.Right);
            AddAssert("no Mixed item", () => dropdown.Items.All(i => !i.IsMixed));
        }

        [Test]
        public void SelectingValue_FiresOnChange()
        {
            AddStep("build mixed", () => build(new MultiValue<HorizontalDirection>(isMixed: true, default)));
            AddStep("pick Right",
                () => dropdown.Current.Value = new MultiValueEnumDropdown<HorizontalDirection>.Choice(HorizontalDirection.Right));
            AddAssert("onChange got Right", () => lastChange == HorizontalDirection.Right);
        }

        // The Easing dropdown carries every osu-framework easing (dozens of entries). An unbounded menu
        // grows to its full content height: nothing overflows its scroll container, so the wheel has
        // nothing to move and the entries past the window edge are unreachable.
        private MultiValueEnumDropdown<Easing> easingDropdown = null!;
        private ManualInputManager input = null!;

        private ScrollContainer<Drawable> menuScroll => easingDropdown.ChildrenOfType<ScrollContainer<Drawable>>().First();

        private void buildEasingDropdownAndOpen()
        {
            AddStep("build easing dropdown", () =>
            {
                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new Container
                    {
                        Width = 200,
                        AutoSizeAxes = Axes.Y,
                        Child = easingDropdown = new MultiValueEnumDropdown<Easing>(
                            new MultiValue<Easing>(isMixed: false, Easing.None), _ => { })
                        {
                            RelativeSizeAxes = Axes.X,
                        },
                    },
                };
            });

            AddUntilStep("dropdown loaded", () => easingDropdown.IsLoaded);

            AddStep("open menu", () =>
            {
                input.MoveMouseTo(easingDropdown.ChildrenOfType<DropdownHeader>().Single());
                input.Click(MouseButton.Left);
            });

            AddUntilStep("menu open", () => menuScroll.DrawHeight > 0);
        }

        [Test]
        public void OpenEasingMenu_FitsOnScreenAndHasSomethingToScroll()
        {
            buildEasingDropdownAndOpen();

            AddAssert("menu shorter than its item list", () => menuScroll.ScrollableExtent, () => Is.GreaterThan(0));
            AddAssert("menu bottom within the scene", () =>
                easingDropdown.ChildrenOfType<Menu>().First().ScreenSpaceDrawQuad.BottomLeft.Y,
                () => Is.LessThanOrEqualTo(Content.ScreenSpaceDrawQuad.BottomLeft.Y));
        }

        [Test]
        public void MouseWheelOverOpenEasingMenu_ScrollsIt()
        {
            buildEasingDropdownAndOpen();

            AddStep("wheel down over the menu", () =>
            {
                input.MoveMouseTo(menuScroll.ToScreenSpace(new Vector2(menuScroll.DrawWidth / 2, menuScroll.DrawHeight / 2)));
                input.ScrollVerticalBy(-3);
            });

            // Asserted after the scroll settles: an unscrollable container still takes the wheel and
            // overshoots elastically (ClampExtension), then springs straight back to 0.
            AddUntilStep("scroll settles", () => Precision.AlmostEquals(menuScroll.Current, menuScroll.Target));
            AddWaitStep("stay settled", 5);
            AddAssert("menu stays scrolled down", () => menuScroll.Current, () => Is.GreaterThan(0));
        }

        [Test]
        public void MixedEntryRendersAsMultipleText()
        {
            Assert.That(
                MultiValueEnumDropdown<HorizontalDirection>.FormatChoice(MultiValueEnumDropdown<HorizontalDirection>.Choice.Mixed).ToString(),
                Is.EqualTo("<multiple>"));
        }
    }
}
