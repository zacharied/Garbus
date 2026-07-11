// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/Compose/Components/BeatDivisorControl.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: rewritten on osu-framework primitives with hardcoded colours (drops
// OverlayColourProvider/OsuColour/OsuAnimatedButton/IconButton); the graphical tick row is
// display-only (osu's interactive TickSliderBar is intentionally dropped). Further rows added in
// later tasks: divisor +/- selector with custom-divisor popover, and type +/- selector.
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Compose
{
    public partial class GarbusBeatDivisorControl : CompositeDrawable
    {
        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Masking = true;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(), // tick display fills the remaining height
                },
                Content = new[]
                {
                    new Drawable[] { new TickDisplay() },
                },
            };
        }

        /// <summary>
        /// Display-only readout: one tick per beat index across the active preset collection, plus a
        /// marker at the current divisor. Selection happens via the chevron/type rows and keys, not here.
        /// </summary>
        internal partial class TickDisplay : CompositeDrawable
        {
            [Resolved]
            private BindableBeatDivisor beatDivisor { get; set; } = null!;

            private Container ticks = null!;
            private EquilateralTriangle marker = null!;

            public TickDisplay()
            {
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 5 },
                    Children = new Drawable[]
                    {
                        ticks = new Container { RelativeSizeAxes = Axes.Both },
                        marker = new EquilateralTriangle
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomCentre,
                            RelativePositionAxes = Axes.X,
                            Size = new Vector2(8, 6.5f),
                            Colour = Color4.White,
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                beatDivisor.ValidDivisors.BindValueChanged(_ => rebuild(), true);
                beatDivisor.BindValueChanged(v => marker.X = mappedPosition(v.NewValue), true);
            }

            private void rebuild()
            {
                ticks.Clear();

                int[] presets = beatDivisor.ValidDivisors.Value.Presets.ToArray();
                int largest = presets.Last();

                for (int i = 0; i <= largest; i++)
                {
                    int divisor = BindableBeatDivisor.GetDivisorForBeatIndex(i, largest, presets);

                    ticks.Add(new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        RelativePositionAxes = Axes.X,
                        RelativeSizeAxes = Axes.Y,
                        Width = 2f,
                        Height = BeatDivisorColours.HeightFor(divisor),
                        X = i / (float)largest,
                        Colour = BeatDivisorColours.ColourFor(divisor),
                    });
                }

                marker.X = mappedPosition(beatDivisor.Value);
            }

            // Matches osu's TickSliderBar.getMappedPosition: 1/1 -> 0, finer divisors -> toward 1.
            private static float mappedPosition(int divisor) => 1 - 1f / divisor;
        }
    }
}
