// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/Compose/Components/BeatDivisorControl.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: rewritten on osu-framework primitives with hardcoded colours (drops
// OverlayColourProvider/OsuColour/OsuAnimatedButton/IconButton); the graphical tick row is
// display-only (osu's interactive TickSliderBar is intentionally dropped). Divisor and type chevron
// rows plus Shift+number entry are below; the 1/N label gains a custom-divisor popover in a later task.
using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

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
                    new Dimension(),                              // tick display fills remaining
                    new Dimension(GridSizeMode.Absolute, 20),     // divisor row
                    new Dimension(GridSizeMode.Absolute, 20),     // type row
                },
                Content = new[]
                {
                    new Drawable[] { new TickDisplay() },
                    new Drawable[] { buildDivisorRow() },
                    new Drawable[] { buildTypeRow() },
                },
            };
        }

        private SpriteText divisorText = null!;
        private SpriteText typeText = null!;
        private int? lastCustomDivisor;

        private Drawable buildDivisorRow() => new GridContainer
        {
            RelativeSizeAxes = Axes.Both,
            ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.Absolute, 20),
                new Dimension(),
                new Dimension(GridSizeMode.Absolute, 20),
            },
            Content = new[]
            {
                new Drawable[]
                {
                    chevron("divisor-prev", "<", beatDivisor.SelectPrevious),
                    divisorText = centredLabel(20),
                    chevron("divisor-next", ">", beatDivisor.SelectNext),
                },
            },
        };

        private Drawable buildTypeRow() => new GridContainer
        {
            RelativeSizeAxes = Axes.Both,
            ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.Absolute, 20),
                new Dimension(),
                new Dimension(GridSizeMode.Absolute, 20),
            },
            Content = new[]
            {
                new Drawable[]
                {
                    chevron("type-prev", "<", () => cycleDivisorType(-1)),
                    typeText = centredLabel(14),
                    chevron("type-next", ">", () => cycleDivisorType(1)),
                },
            },
        };

        private static BasicButton chevron(string name, string glyph, Action action) => new BasicButton
        {
            Name = name,
            RelativeSizeAxes = Axes.Both,
            Text = glyph,
            Action = action,
            BackgroundColour = new Color4(60, 60, 70, 255),
        };

        private static SpriteText centredLabel(float size) => new SpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Font = osu.Framework.Graphics.Sprites.FontUsage.Default.With(size: size),
        };

        private void cycleDivisorType(int direction)
        {
            int totalTypes = Enum.GetValues<BeatDivisorType>().Length;
            BeatDivisorType currentType = beatDivisor.ValidDivisors.Value.Type;

            cycleOnce();

            // Skip Custom if we have no recorded custom divisor to return to.
            if (lastCustomDivisor == null && currentType == BeatDivisorType.Custom)
                cycleOnce();

            switch (currentType)
            {
                case BeatDivisorType.Common:
                    beatDivisor.SetArbitraryDivisor(4, true);
                    break;

                case BeatDivisorType.Triplets:
                    beatDivisor.SetArbitraryDivisor(6, true);
                    break;

                case BeatDivisorType.Custom:
                    beatDivisor.SetArbitraryDivisor(lastCustomDivisor!.Value);
                    break;
            }

            void cycleOnce() => currentType = (BeatDivisorType)(((int)currentType + totalTypes + direction) % totalTypes);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatDivisor.BindValueChanged(v => divisorText.Text = $"1/{v.NewValue}", true);
            beatDivisor.ValidDivisors.BindValueChanged(valid =>
            {
                typeText.Text = valid.NewValue.Type.ToString().ToLowerInvariant();
                if (valid.NewValue.Type == BeatDivisorType.Custom)
                    lastCustomDivisor = valid.NewValue.Presets.Last();
            }, true);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.ShiftPressed && e.Key >= Key.Number1 && e.Key <= Key.Number9)
            {
                beatDivisor.SetArbitraryDivisor(e.Key - Key.Number0);
                return true;
            }

            return base.OnKeyDown(e);
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
