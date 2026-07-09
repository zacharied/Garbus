// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/Components/TimeInfoContainer.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: no OsuColour/OverlayColourProvider/OsuFont/OsuClickableContainer — uses
// plain BasicText and Colour4; no timestamp text-box click-to-edit; shows mm:ss.fff + BPM.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.BottomBar
{
    /// <summary>
    /// Displays the current playhead position (mm:ss.fff) and BPM, updated every frame.
    /// </summary>
    public partial class TimeInfoDisplay : CompositeDrawable
    {
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        private SpriteText timeText = null!;
        private SpriteText bpmText = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(14, 14, 20, 255),
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new osuTK.Vector2(0, 4),
                    Children = new Drawable[]
                    {
                        timeText = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = FontUsage.Default.With(size: 20, fixedWidth: true),
                            Colour = Color4.White,
                            Text = "00:00.000",
                        },
                        bpmText = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = FontUsage.Default.With(size: 12),
                            Colour = new Color4(255, 180, 60, 255),
                            Text = "0 BPM",
                        },
                    },
                },
            };
        }

        private double lastTime = double.MinValue;
        private double lastBpm = double.MinValue;

        protected override void Update()
        {
            base.Update();

            double t = editorClock.CurrentTime;
            double bpm = editorChart.ControlPointInfo.TimingPointAt(t).BPM;

            if (Math.Abs(t - lastTime) >= 1)
            {
                lastTime = t;
                timeText.Text = formatTime(t);
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (bpm != lastBpm)
            {
                lastBpm = bpm;
                bpmText.Text = $"{bpm:0} BPM";
            }
        }

        private static string formatTime(double ms)
        {
            bool negative = ms < 0;
            ms = Math.Abs(ms);

            int totalSec = (int)(ms / 1000);
            int minutes = totalSec / 60;
            int seconds = totalSec % 60;
            int millis = (int)(ms % 1000);

            return $"{(negative ? "-" : "")}{minutes:00}:{seconds:00}.{millis:000}";
        }
    }
}
