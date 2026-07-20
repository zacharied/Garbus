// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/Components/Timelines/Summary/SummaryTimeline.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: no osu.Game OverlayColourProvider/BreakPart/KiaiPart/BookmarkPart; shows
// timing-point ticks, preview-time marker, and a playhead progress bar; click/drag seeks RAW
// (unsnapped) — beat-snapping is not applied in the summary timeline.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Garbus.Game.Charts.Timing;

namespace Garbus.Game.Edit.Screens.BottomBar
{
    /// <summary>
    /// The full-track overview timeline at the bottom of the editor.
    /// Shows timing-point ticks, a preview-time marker, and a playhead indicator.
    /// Click or drag to seek (raw/unsnapped).
    /// </summary>
    public partial class SummaryTimeline : CompositeDrawable
    {
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorSong editorSong { get; set; } = null!;

        private ControlPointInfo controlPointInfo = null!;

        private Container tickContainer = null!;
        private Drawable previewMarker = null!;
        private Container progressBarContainer = null!;
        private Container playheadLine = null!;

        // Parallel list keeps timing-point times so we can position ticks in Update().
        private readonly List<(double time, Drawable tick)> timingTicks = new List<(double, Drawable)>();

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                // Background
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(20, 20, 28, 255),
                },
                // Tick marks for timing points
                tickContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                // Preview time marker — positioned relative to track length
                previewMarker = new Box
                {
                    Width = 2,
                    RelativeSizeAxes = Axes.Y,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Colour = new Color4(0, 220, 100, 200),
                    Alpha = 0,
                },
                // Playhead progress bar (grows from left)
                progressBarContainer = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(255, 220, 80, 80),
                    },
                },
                // Playhead line
                playheadLine = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 2,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(255, 220, 80, 220),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            bindControlPointInfo();
            editorChart.ChartChanged += onChartChanged;
            rebuildTicks();
            updatePreviewMarker();
        }

        private void onChartChanged(Charts.GarbusChart _, Charts.GarbusChart __) => bindControlPointInfo();

        private void bindControlPointInfo()
        {
            if (controlPointInfo != null)
                controlPointInfo.ControlPointsChanged -= onControlPointsChanged;
            controlPointInfo = editorChart.ControlPointInfo;
            controlPointInfo.ControlPointsChanged += onControlPointsChanged;
            rebuildTicks();
        }

        private void onControlPointsChanged() => rebuildTicks();

        private void rebuildTicks()
        {
            tickContainer.Clear();
            timingTicks.Clear();

            foreach (var tp in controlPointInfo.TimingPoints)
            {
                var tick = new Box
                {
                    Width = 2,
                    RelativeSizeAxes = Axes.Y,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Colour = new Color4(100, 180, 255, 180),
                };
                tickContainer.Add(tick);
                timingTicks.Add((tp.Time, tick));
            }
        }

        private void updatePreviewMarker()
        {
            double? preview = editorSong.Song.PreviewTime;
            previewMarker.Alpha = (preview.HasValue && preview.Value >= 0) ? 1 : 0;
        }

        protected override void Update()
        {
            base.Update();

            double trackLength = editorClock.TrackLength;
            if (trackLength <= 0 || DrawWidth <= 0) return;

            double currentTime = editorClock.CurrentTime;
            float progress = (float)(currentTime / trackLength);

            // Playhead progress bar width
            progressBarContainer.Width = DrawWidth * progress;

            // Playhead line position (X = absolute pixels from left)
            playheadLine.X = DrawWidth * progress;

            // Tick positions
            foreach (var (time, tick) in timingTicks)
                tick.X = (float)(time / trackLength * DrawWidth);

            // Preview marker position
            double? previewTime = editorSong.Song.PreviewTime;
            if (previewTime.HasValue && previewTime.Value >= 0 && previewTime.Value <= trackLength)
            {
                previewMarker.Alpha = 1;
                previewMarker.X = (float)(previewTime.Value / trackLength * DrawWidth);
            }
            else
            {
                previewMarker.Alpha = 0;
            }
        }

        // ---- Seek: raw (unsnapped) ----

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != osuTK.Input.MouseButton.Left)
                return false;

            seekToMousePosition(e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != osuTK.Input.MouseButton.Left)
                return false;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            seekToMousePosition(e.ScreenSpaceMousePosition);
        }

        private void seekToMousePosition(Vector2 screenPos)
        {
            float localX = ToLocalSpace(screenPos).X;
            double seekTime = Math.Clamp(localX / DrawWidth * editorClock.TrackLength, 0, editorClock.TrackLength);
            // Raw seek — no snapping.
            editorClock.Seek(seekTime);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (editorChart != null)
                editorChart.ChartChanged -= onChartChanged;
            if (controlPointInfo != null)
                controlPointInfo.ControlPointsChanged -= onControlPointsChanged;
        }
    }
}
