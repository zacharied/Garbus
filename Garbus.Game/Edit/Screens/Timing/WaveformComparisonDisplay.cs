// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/WaveformComparisonDisplay.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: WaveformGraph requires WorkingBeatmap (osu.Game) which is not available in
// Garbus; replaced with a lightweight beat-grid overlay that shows the current beat position as
// coloured bars. The interaction semantics (hover/lock/scrub) are preserved.

using System;
using Garbus.Game.Charts.Timing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Beat-grid display that shows beat positions relative to the selected timing point.
    /// Acts as a lightweight stand-in for a full waveform display.
    /// </summary>
    public partial class WaveformComparisonDisplay : CompositeDrawable
    {
        private const int visible_bars = 8;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        /// <summary>Bind to TimingPointList.SelectedGroup.</summary>
        public readonly Bindable<ControlPointGroup?> SelectedGroup = new Bindable<ControlPointGroup?>();

        private TimingControlPoint? timingPoint;
        private Container barContainer = null!;
        private SpriteText lockLabel = null!;
        private BeatBar[] beatBars = null!;
        private bool displayLocked;
        private double displayedBeat = double.MinValue;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 6;

            beatBars = new BeatBar[visible_bars];

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(30, 30, 40, 255),
                },
                barContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new Box
                {
                    // Centre playhead marker.
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    Width = 2,
                    Colour = Color4.White,
                },
                lockLabel = new SpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Text = "Locked",
                    Colour = Color4.OrangeRed,
                    Padding = new MarginPadding(4),
                    Font = FontUsage.Default.With(size: 12),
                    Alpha = 0,
                },
            };

            // Pre-populate bars with absolute positioning.
            for (int i = 0; i < visible_bars; i++)
            {
                beatBars[i] = new BeatBar(i == visible_bars / 2);
                barContainer.Add(beatBars[i]);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            SelectedGroup.BindValueChanged(_ => onGroupChanged(), true);
        }

        private void onGroupChanged()
        {
            timingPoint = null;

            if (SelectedGroup.Value == null) return;

            foreach (var cp in SelectedGroup.Value.ControlPoints)
            {
                if (cp is TimingControlPoint tp)
                {
                    timingPoint = tp;
                    break;
                }
            }

            displayedBeat = double.MinValue; // force refresh
        }

        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (!displayLocked && timingPoint != null)
            {
                double trackLength = editorClock.TrackLength;
                double groupStart = SelectedGroup.Value?.Time ?? 0;
                int totalBeats = Math.Max(1, (int)((trackLength - groupStart) / timingPoint.BeatLength));
                int targetBeat = (int)(e.MousePosition.X / DrawWidth * totalBeats);
                showFromBeat(targetBeat);
            }
            return base.OnMouseMove(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            displayLocked = !displayLocked;
            lockLabel.FadeTo(displayLocked ? 1 : 0, 100, Easing.OutQuint);
            return true;
        }

        protected override void Update()
        {
            base.Update();

            // Position bars to fill width correctly.
            float barWidth = DrawWidth / visible_bars;
            for (int i = 0; i < visible_bars; i++)
            {
                beatBars[i].X = i * barWidth;
                beatBars[i].Width = barWidth - 1;
                beatBars[i].Height = DrawHeight;
            }

            if (!IsHovered && !displayLocked && timingPoint != null)
            {
                double groupStart = SelectedGroup.Value?.Time ?? 0;
                int currentBeat = (int)Math.Floor((editorClock.CurrentTimeAccurate - groupStart) / timingPoint.BeatLength);
                showFromBeat(currentBeat);
            }
        }

        private void showFromBeat(int beat)
        {
            if (displayedBeat == beat) return;
            displayedBeat = beat;
            regenerateBars();
        }

        private void regenerateBars()
        {
            if (timingPoint == null) return;

            double groupStart = SelectedGroup.Value?.Time ?? 0;
            int numerator = timingPoint.TimeSignature.Numerator;
            int startBeat = (int)displayedBeat - visible_bars / 2;

            for (int barIdx = 0; barIdx < visible_bars; barIdx++)
            {
                int beatNum = startBeat + barIdx;
                double beatTime = groupStart + beatNum * timingPoint.BeatLength;
                bool isDownbeat = ((beatNum % numerator) + numerator) % numerator == 0;
                bool isCurrent = barIdx == visible_bars / 2;

                beatBars[barIdx].SetState(beatNum, isDownbeat, isCurrent, beatTime < 0 || beatTime > editorClock.TrackLength);
            }
        }

        private partial class BeatBar : CompositeDrawable
        {
            private readonly bool isMain;
            private Box fill = null!;
            private SpriteText label = null!;

            public BeatBar(bool isMain)
            {
                this.isMain = isMain;
                // Absolutely positioned within barContainer; size set in parent's Update().
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    fill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = isMain ? 0.4f : 0.15f,
                        Colour = Color4.White,
                    },
                    label = new SpriteText
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Font = FontUsage.Default.With(size: 10),
                        Colour = Color4.White,
                        Alpha = 0.6f,
                    },
                };
            }

            public void SetState(int beatNum, bool isDownbeat, bool isCurrent, bool outOfRange)
            {
                fill.Colour = outOfRange
                    ? new Color4(60, 60, 60, 255)
                    : isDownbeat
                        ? new Color4(240, 200, 80, 255)
                        : new Color4(100, 150, 220, 255);

                fill.Alpha = outOfRange ? 0.1f : isCurrent ? 0.6f : isDownbeat ? 0.5f : 0.2f;
                label.Text = beatNum.ToString();
            }
        }
    }
}
