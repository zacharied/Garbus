// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/MetronomeDisplay.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: BeatSyncedContainer not available (osu.Game only); beat scheduling done via
// Update() polling against EditorClock; osu.Game graphics/colour providers replaced with Basic*;
// pendulum animation kept (swing container rotate, weight position); metronome samples from
// Garbus.Resources/Samples/Editor/metronome-tick.wav and metronome-downbeat.wav.

using System;
using Garbus.Game.Charts.Timing;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Visual + audio metronome that ticks per beat of the selected timing point while the
    /// editor clock is running. Tick/downbeat samples live at Samples/Editor/metronome-*.wav.
    /// </summary>
    public partial class MetronomeDisplay : CompositeDrawable
    {
        /// <summary>
        /// Air this display keeps around its body. Exposed so controls stacked beneath it can offset
        /// themselves by the same amount and sit flush with the body rather than with the padding.
        /// </summary>
        public const float PADDING = 8;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        /// <summary>Bind to TimingPointList.SelectedGroup.</summary>
        public readonly Bindable<ControlPointGroup?> SelectedGroup = new Bindable<ControlPointGroup?>();

        /// <summary>When false, samples are not played (used while tap-to-BPM is capturing, or when
        /// muted via the click toggle).</summary>
        public readonly BindableBool EnableClicking = new BindableBool(true);

        private Sample? sampleTick;
        private Sample? sampleDownbeat;

        // Pendulum visual elements.
        private Container swing = null!;
        private Drawable stick = null!;
        private Drawable weight = null!;
        private SpriteText bpmLabel = null!;

        // Beat tracking state.
        private double nextBeatTime = double.MaxValue;
        private int beatIndex;
        private bool swingRight = true;
        private bool wasRunning;

        private TimingControlPoint? currentTimingPoint;

        // Stored so we can unsubscribe before re-binding on group change (prevents accumulation).
        private TimingControlPoint? boundTimingPoint;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            sampleTick = audio.Samples.Get("Editor/metronome-tick");
            sampleDownbeat = audio.Samples.Get("Editor/metronome-downbeat");

            AutoSizeAxes = Axes.Both;
            Padding = new MarginPadding(PADDING);

            const float body_width = 80;
            const float body_height = 120;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Size = new Vector2(body_width, body_height),
                    Colour = new osuTK.Graphics.Color4(50, 50, 60, 255),
                },
                swing = new Container
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -20,
                    Size = new Vector2(4, 80),
                    Children = new Drawable[]
                    {
                        stick = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new osuTK.Graphics.Color4(200, 200, 220, 255),
                        },
                        weight = new Box
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(10, 10),
                            RelativePositionAxes = Axes.Y,
                            Y = 0.4f,
                            Colour = new osuTK.Graphics.Color4(240, 200, 80, 255),
                        },
                    },
                },
                bpmLabel = new SpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -4,
                    Colour = new osuTK.Graphics.Color4(220, 220, 240, 255),
                    Font = FontUsage.Default.With(size: 14),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SelectedGroup.BindValueChanged(_ => onGroupChanged(), true);

            EnableClicking.BindValueChanged(e =>
            {
                if (!e.NewValue)
                    swing.RotateTo(0, 600, Easing.OutQuint);
            });
        }

        private void onGroupChanged()
        {
            // Unbind from the previously-bound timing point to prevent subscription accumulation.
            if (boundTimingPoint != null)
            {
                boundTimingPoint.BeatLengthBindable.ValueChanged -= onBeatLengthChanged;
                boundTimingPoint = null;
            }

            var tp = SelectedGroup.Value?.ControlPoints is { } cps
                ? getTimingPoint(cps)
                : null;

            currentTimingPoint = tp;
            beatIndex = 0;
            nextBeatTime = double.MaxValue;

            updateBpmLabel();
        }

        private static TimingControlPoint? getTimingPoint(
            osu.Framework.Bindables.IBindableList<ControlPoint> controlPoints)
        {
            foreach (var cp in controlPoints)
            {
                if (cp is TimingControlPoint tp) return tp;
            }
            return null;
        }

        protected override void Update()
        {
            base.Update();

            bool running = editorClock.IsRunning;

            if (!running)
            {
                if (wasRunning)
                {
                    // Clock stopped — latch pendulum back to centre.
                    swing.RotateTo(0, 600, Easing.OutQuint);
                }
                wasRunning = false;
                return;
            }

            if (!wasRunning)
            {
                // Clock just started — compute first beat time.
                initBeatTracking();
            }

            wasRunning = true;

            double currentTime = editorClock.CurrentTime;

            while (currentTime >= nextBeatTime && currentTimingPoint != null)
            {
                onBeat();
                nextBeatTime += currentTimingPoint.BeatLength;
                beatIndex++;
            }
        }

        private void initBeatTracking()
        {
            if (currentTimingPoint == null) return;

            double current = editorClock.CurrentTime;
            double tp_time = currentTimingPoint.Time;
            double bl = currentTimingPoint.BeatLength;

            if (bl <= 0) return;

            // Find how many beats have elapsed since the timing point.
            double elapsedBeats = Math.Floor((current - tp_time) / bl);
            nextBeatTime = tp_time + elapsedBeats * bl;
            beatIndex = (int)(elapsedBeats % currentTimingPoint.TimeSignature.Numerator);

            // If we're already past nextBeatTime, advance.
            if (nextBeatTime <= current)
            {
                nextBeatTime += bl;
                beatIndex++;
            }
        }

        private void onBeat()
        {
            if (currentTimingPoint == null) return;

            if (!EnableClicking.Value) return;

            bool isDownbeat = (beatIndex % currentTimingPoint.TimeSignature.Numerator) == 0;

            if (isDownbeat)
                sampleDownbeat?.Play();
            else
                sampleTick?.Play();

            // Pendulum swing.
            float angle = 25f;
            swing.RotateTo(swingRight ? angle : -angle, currentTimingPoint.BeatLength * 0.9, Easing.InOutSine);
            swingRight = !swingRight;
        }

        private void updateBpmLabel()
        {
            if (currentTimingPoint == null)
            {
                bpmLabel.Text = string.Empty;
                return;
            }

            double bpm = 60000.0 / currentTimingPoint.BeatLength;
            bpmLabel.Text = $"{bpm:0.#}";

            // Subscribe with a named method so we can unsubscribe precisely in onGroupChanged/Dispose.
            if (boundTimingPoint != currentTimingPoint)
            {
                currentTimingPoint.BeatLengthBindable.ValueChanged += onBeatLengthChanged;
                boundTimingPoint = currentTimingPoint;
            }
        }

        private void onBeatLengthChanged(ValueChangedEvent<double> _) => updateBpmLabel();

        protected override void Dispose(bool isDisposing)
        {
            if (boundTimingPoint != null)
            {
                boundTimingPoint.BeatLengthBindable.ValueChanged -= onBeatLengthChanged;
                boundTimingPoint = null;
            }

            base.Dispose(isDisposing);
        }
    }
}
