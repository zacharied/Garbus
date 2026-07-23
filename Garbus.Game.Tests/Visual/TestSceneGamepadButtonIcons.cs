using System;
using Garbus.Game.Input;
using NUnit.Framework;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneGamepadButtonIcons : GarbusTestScene
    {
        public TestSceneGamepadButtonIcons()
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Full,
                Spacing = new Vector2(20),
                Padding = new MarginPadding(40),
            };

            Enum.GetValues<GamepadButton>().ForEach(button => flow.Add(new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(6),
                Children = new Drawable[]
                {
                    new GamepadButtonSprite(button)
                    {
                        Size = new Vector2(80),
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                    new SpriteText
                    {
                        Text = button.ToString(),
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                },
            }));

            Add(flow);
        }
    }
}
