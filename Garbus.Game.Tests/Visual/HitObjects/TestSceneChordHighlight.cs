using System.Linq;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Testing.Input;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Visual.HitObjects
{
    [TestFixture]
    public partial class TestSceneChordHighlight : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private GarbusPlayfield playfield = null!;

        private void buildScene(params GarbusHitObject[] hitObjects)
        {
            manualClock = new ManualClock { Rate = 1 };

            foreach (var h in hitObjects)
                h.ApplyDefaults();

            Child = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = new GarbusInputManager
                    {
                        Child = playfield = new GarbusPlayfield { Size = Vector2.One },
                    },
                },
            };

            foreach (var h in hitObjects)
                playfield.Add(PlayScreen.CreateDrawableRepresentation(h));

            playfield.SetHitObjects(hitObjects);
        }

        private DrawableCardinalNote cardinalAt(int angle) =>
            playfield.AllHitObjects.OfType<DrawableCardinalNote>().Single(d => d.HitObject.AngleDeg == angle);

        [Test]
        public void CoincidentPairIsYellow()
        {
            AddStep("two cardinals at 2000ms", () => buildScene(
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);

            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("both alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));

            AddAssert("north yellow", () => cardinalAt(90).Colour, () => Is.EqualTo((ColourInfo)ChordColours.Highlight));
            AddAssert("south yellow", () => cardinalAt(270).Colour, () => Is.EqualTo((ColourInfo)ChordColours.Highlight));
        }

        [Test]
        public void LoneCardinalIsWhite()
        {
            AddStep("single cardinal", () => buildScene(new CardinalNote { AngleDeg = 90, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);
            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().Any(d => d.IsAlive));

            AddAssert("white", () => cardinalAt(90).Colour, () => Is.EqualTo((ColourInfo)Colour4.White));
        }
    }
}
