// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/WaveformComparisonDisplay.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: no WorkingBeatmap — the Waveform is decoded here from the song's audio
// stream (the same route TimelineStrip takes) and shared by all rows; OverlayColourProvider /
// OsuSpriteText replaced with explicit colours and SpriteText; the selected group arrives on the
// SelectedGroup bindable rather than a DI-cached one; the next timing point comes from
// EditorChart.ControlPointInfo. Deviation: a scale change re-arms the graph's resampling (see
// WaveformRow.WaveformScale).

using System;
using System.Globalization;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Slices the song's waveform into one row per beat of the selected timing point, stacked so the
    /// beats line up vertically under a centre playhead. A timing point is correct when every row's
    /// transient sits on that line.
    /// </summary>
    /// <remarks>
    /// Hovering scrubs the display along the track; clicking locks it so the rows stop following the
    /// editor clock (useful while nudging offset/BPM against a fixed reference).
    /// </remarks>
    public partial class WaveformComparisonDisplay : CompositeDrawable
    {
        private const int total_waveforms = 8;

        private const float corner_radius = 6;

        private static readonly Color4 background_colour = new Color4(28, 28, 36, 255);
        private static readonly Color4 main_row_colour = new Color4(50, 50, 64, 255);
        private static readonly Color4 beat_index_colour = new Color4(170, 170, 190, 255);
        private static readonly Color4 locked_colour = new Color4(220, 60, 60, 255);

        /// <summary>
        /// Milliseconds of audio shown across the width of a single row.
        /// </summary>
        /// <remarks>
        /// Vendored from osu's reasoning: a fixed window is a pretty usable number across all BPMs.
        /// Scaling it with the BPM would be expensive to resample and harder to track while making
        /// realtime adjustments, so the window stays constant and the rows move under it.
        /// </remarks>
        public float VisibleWidth
        {
            get => visibleWidth;
            set
            {
                if (visibleWidth == value)
                    return;

                visibleWidth = value;
                Scheduler.AddOnce(regenerateDisplay, false);
            }
        }

        private float visibleWidth = 300;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private SongFile songFile { get; set; } = null!;

        /// <summary>Bind to TimingPointList.SelectedGroup.</summary>
        public readonly Bindable<ControlPointGroup?> SelectedGroup = new Bindable<ControlPointGroup?>();

        private readonly BindableDouble beatLength = new BindableDouble();
        private readonly BindableBool displayLocked = new BindableBool();

        private WaveformRow[] rows = null!;
        private LockedOverlay lockedOverlay = null!;

        private TimingControlPoint timingPoint = new TimingControlPoint();

        // NaN so the first showFromTime always regenerates — a real time of 0 would otherwise compare
        // equal to the initial value and the display would never draw until the clock moved.
        private double displayedTime = double.NaN;

        private double selectedGroupStartTime;
        private double selectedGroupEndTime;

        // The WaveformGraph does not dispose the Waveform it is handed, so we own the single instance
        // shared by every row and dispose it when replaced or on teardown (it disposes its own stream).
        private Waveform? currentWaveform;

        private ControlPointInfo controlPointInfo = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = corner_radius;

            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = background_colour,
            });

            rows = new WaveformRow[total_waveforms];

            for (int i = 0; i < total_waveforms; i++)
            {
                AddInternal(rows[i] = new WaveformRow(i == total_waveforms / 2)
                {
                    RelativeSizeAxes = Axes.Both,
                    RelativePositionAxes = Axes.Both,
                    Height = 1f / total_waveforms,
                    Y = (float)i / total_waveforms,
                });
            }

            AddInternal(new Circle
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Colour = Color4.White,
                RelativeSizeAxes = Axes.Y,
                Width = 3,
            });

            AddInternal(lockedOverlay = new LockedOverlay());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            bindControlPointInfo();
            editorChart.ChartChanged += onChartChanged;

            editorClock.TrackChanged += updateWaveform;
            updateWaveform();

            SelectedGroup.BindValueChanged(_ => updateTimingGroup(), true);

            beatLength.BindValueChanged(_ => Scheduler.AddOnce(regenerateDisplay, true), true);

            displayLocked.BindValueChanged(locked =>
            {
                if (locked.NewValue)
                    lockedOverlay.Show();
                else
                    lockedOverlay.Hide();
            }, true);
        }

        private void onChartChanged(GarbusChart _, GarbusChart __)
        {
            bindControlPointInfo();
            updateTimingGroup();
        }

        private void bindControlPointInfo()
        {
            if (controlPointInfo != null)
                controlPointInfo.ControlPointsChanged -= updateTimingGroup;

            controlPointInfo = editorChart.ControlPointInfo;
            controlPointInfo.ControlPointsChanged += updateTimingGroup;
        }

        private void updateTimingGroup()
        {
            beatLength.UnbindBindings();

            var tcp = SelectedGroup.Value?.ControlPoints.OfType<TimingControlPoint>().FirstOrDefault();

            if (tcp == null)
            {
                timingPoint = new TimingControlPoint();
                // Moving a control point's offset is implemented as a remove followed by an insert at
                // the new time, so this branch is hit momentarily mid-move. selectedGroupStartTime is
                // deliberately left alone — the re-insert below needs the previous value to work out
                // how far the display should travel.
                selectedGroupEndTime = editorClock.TrackLength;
                return;
            }

            timingPoint = tcp;
            beatLength.BindTo(timingPoint.BeatLengthBindable);

            double? newStartTime = SelectedGroup.Value?.Time;
            double? offsetChange = newStartTime - selectedGroupStartTime;

            var nextTimingPoint = controlPointInfo.TimingPoints
                                                  .SkipWhile(t => !ReferenceEquals(t, tcp))
                                                  .Skip(1)
                                                  .FirstOrDefault();

            selectedGroupStartTime = newStartTime ?? 0;
            selectedGroupEndTime = nextTimingPoint?.Time ?? editorClock.TrackLength;

            if (newStartTime.HasValue && offsetChange.HasValue)
            {
                // The selected point's offset may have moved. Travel with it, so a locked display
                // keeps showing the same audio while the user nudges the point onto it.
                showFromTime(displayedTime + offsetChange.Value, true);
            }
        }

        private void updateWaveform()
        {
            // Null for an unsaved song, no audio file, a missing file, or a virtual track — the rows
            // then render their beat indices over empty backgrounds rather than failing.
            var stream = songFile.GetAudioStream();

            foreach (var row in rows)
                row.Waveform = null;

            currentWaveform?.Dispose();
            currentWaveform = stream == null ? null : new Waveform(stream);

            foreach (var row in rows)
                row.Waveform = currentWaveform;

            Scheduler.AddOnce(regenerateDisplay, false);
        }

        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (!displayLocked.Value)
            {
                double trackLength = editorClock.TrackLength;
                int totalBeatsAvailable = (int)((trackLength - timingPoint.Time) / timingPoint.BeatLength);

                Scheduler.AddOnce(showFromBeat, (int)(e.MousePosition.X / DrawWidth * totalBeatsAvailable));
            }

            return base.OnMouseMove(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            displayLocked.Toggle();
            return true;
        }

        protected override void Update()
        {
            base.Update();

            if (!IsHovered && !displayLocked.Value)
            {
                int currentBeat = (int)Math.Floor((editorClock.CurrentTimeAccurate - selectedGroupStartTime) / timingPoint.BeatLength);

                showFromBeat(currentBeat);
            }
        }

        private void showFromBeat(int beatIndex) =>
            showFromTime(selectedGroupStartTime + beatIndex * timingPoint.BeatLength, false);

        private void showFromTime(double time, bool animated)
        {
            if (displayedTime == time)
                return;

            displayedTime = time;
            Scheduler.AddOnce(regenerateDisplay, animated);
        }

        private void regenerateDisplay(bool animated)
        {
            float trackLength = (float)editorClock.TrackLength;

            // Track length reads 0 until the track loads. Drop back to the "nothing shown yet"
            // sentinel so the next Update() re-attempts, rather than rescheduling ourselves forever.
            if (trackLength <= 0 || double.IsNaN(displayedTime))
            {
                displayedTime = double.NaN;
                return;
            }

            double index = (displayedTime - selectedGroupStartTime) / timingPoint.BeatLength;

            // Each row is VisibleWidth ms wide, so the whole track drawn at this scale spans
            // trackLength / VisibleWidth row widths.
            float scale = trackLength / VisibleWidth;

            // Start displaying from before the current beat, so it lands on the main (centre) row.
            index -= total_waveforms / 2;

            foreach (var row in rows)
            {
                // Offset to the required beat index.
                double time = selectedGroupStartTime + index * timingPoint.BeatLength;

                float offset = (float)(time - VisibleWidth / 2 + GarbusEditor.WAVEFORM_VISUAL_OFFSET) / trackLength * scale;

                row.Alpha = time < selectedGroupStartTime || time > selectedGroupEndTime ? 0.2f : 1;
                row.WaveformScale = new Vector2(scale, 1);
                row.WaveformOffsetTo(-offset, animated);
                row.BeatIndex = (int)Math.Round(index);

                index++;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editorChart.IsNotNull())
                editorChart.ChartChanged -= onChartChanged;

            if (controlPointInfo.IsNotNull())
                controlPointInfo.ControlPointsChanged -= updateTimingGroup;

            if (editorClock.IsNotNull())
                editorClock.TrackChanged -= updateWaveform;

            currentWaveform?.Dispose();
        }

        private partial class LockedOverlay : CompositeDrawable
        {
            private SpriteText text = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = corner_radius;
                BorderColour = locked_colour;
                BorderThickness = 3;
                Alpha = 0;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        // Masking traces the border around the composite's children, so an invisible
                        // always-present child is what gives that border an area to follow.
                        AlwaysPresent = true,
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                    new Container
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        AutoSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                Colour = locked_colour,
                                RelativeSizeAxes = Axes.Both,
                            },
                            text = new SpriteText
                            {
                                Name = "waveform comparison lock indicator",
                                Colour = Color4.White,
                                Text = "Locked",
                                Margin = new MarginPadding(5),
                                Font = FontUsage.Default.With(size: 12),
                            },
                        },
                    },
                };
            }

            public override void Show()
            {
                this.FadeIn(100, Easing.OutQuint);

                text
                    .FadeIn().Then().Delay(600)
                    .FadeOut().Then().Delay(600)
                    .Loop();
            }

            public override void Hide()
            {
                this.FadeOut(100, Easing.OutQuint);
            }
        }

        private partial class WaveformRow : CompositeDrawable
        {
            private readonly bool isMainRow;

            private SpriteText beatIndexText = null!;
            private WaveformGraph waveformGraph = null!;

            public WaveformRow(bool isMainRow)
            {
                this.isMainRow = isMainRow;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        Colour = main_row_colour,
                        Alpha = isMainRow ? 1 : 0,
                        RelativeSizeAxes = Axes.Both,
                    },
                    waveformGraph = new WaveformGraph
                    {
                        RelativeSizeAxes = Axes.Both,
                        RelativePositionAxes = Axes.Both,
                        Resolution = 1,

                        // The same frequency palette the timeline strip uses, so a transient reads
                        // the same way in both places.
                        BaseColour = Colour4.Blue.Opacity(0.2f),
                        LowColour = Colour4.CornflowerBlue,
                        MidColour = Colour4.DodgerBlue,
                        HighColour = Colour4.SteelBlue,
                    },
                    beatIndexText = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Padding = new MarginPadding(5),
                        Font = FontUsage.Default.With(size: 12),
                        Colour = beat_index_colour,
                    },
                };
            }

            public Waveform? Waveform
            {
                set => waveformGraph.Waveform = value;
            }

            public int BeatIndex
            {
                set => beatIndexText.Text = value.ToString(CultureInfo.InvariantCulture);
            }

            public Vector2 WaveformScale
            {
                set
                {
                    if (waveformGraph.Scale == value)
                        return;

                    waveformGraph.Scale = value;

                    // Deviation from osu: WaveformGraph derives its resampled point count from
                    // DrawWidth * Scale.X, but only re-resamples when Waveform, Resolution or
                    // DrawSize change — a scale change alone leaves it drawing points generated for
                    // the old (much smaller) width, which reads as a blocky smear. Re-assigning the
                    // waveform re-arms the resample; the graph's own scheduler coalesces it, and this
                    // only runs when the scale actually moves (a BPM or track-length change).
                    var current = waveformGraph.Waveform;
                    waveformGraph.Waveform = null;
                    waveformGraph.Waveform = current;
                }
            }

            public void WaveformOffsetTo(float value, bool animated) =>
                this.TransformTo(nameof(waveformOffset), value, animated ? 300 : 0, Easing.OutQuint);

            private float waveformOffset
            {
                get => waveformGraph.X;
                set => waveformGraph.X = value;
            }
        }
    }
}
