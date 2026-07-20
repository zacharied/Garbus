// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/Compose/Components/Timeline/Timeline.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: ZoomableScrollContainer subclass; drops WorkingBeatmap/IBeatSnapProvider/
// OsuColour/OverlayColourProvider dependencies; derives zoom range from EditorClock.TrackLength;
// drives composer TimelineTimeRange via BAC's exact formula:
//   TimelineTimeRange = EditorClock.TrackLength / CurrentZoom / 2
// Waveform layer: WaveformGraph fed from the song's shared track resource (null-safe).
// Content width ∝ TrackLength × zoom; CentreMarker is a fixed overlay (non-scrolling).
// Scroll-to-clock when playing; user drag seeks raw throughout — no beat snapping on release.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using Garbus.Game.Charts;
using Garbus.Game.Configuration;

namespace Garbus.Game.Edit.Screens.Timeline
{
    [Cached]
    public partial class TimelineStrip : ZoomableScrollContainer
    {
        public const float HEIGHT = 90;

        /// <summary>
        /// The current zoom level. When changed, updates the composer's
        /// <c>TimelineTimeRange = EditorClock.TrackLength / CurrentZoom / 2</c>.
        /// </summary>
        // Exposed publicly so ComposeTab can subscribe and push TimelineTimeRange into the composer.
        // CurrentZoom is declared on ZoomableScrollContainer; we re-expose it here for clarity.

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private SongFile songFile { get; set; } = null!;

        // ---- Bindable<double> written to the composer's TimelineTimeRange ----
        // Owned externally (ComposeTab) and passed in so the composer can subscribe.
        // See ComposeTab.

        private WaveformGraph waveform = null!;
        private TimelineTickDisplay ticks = null!;
        private TimelineDesignRegionDisplay designRegions = null!;
        private TimelineTimingChangeDisplay timingChanges = null!;
        private TimelineObjectMarkers objectMarkers = null!;

        // Stored as fields so the bindables are not garbage-collected after load() returns.
        private Bindable<bool>? showTicksBindable;
        private Bindable<bool>? showTimingChangesBindable;
        private Bindable<bool>? showDesignRegionsBindable;
        private Bindable<double>? waveformOpacityBindable;

        private double lastScrollPosition;
        private double lastTrackTime;
        private bool handlingDragInput;
        private bool trackWasPlaying;

        private double trackLengthForZoom;

        // The WaveformGraph does not dispose the Waveform it is handed, so we own it here and
        // dispose it when replaced or on teardown. The Waveform in turn disposes its own stream.
        private Waveform? currentWaveform;

        public TimelineStrip()
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;

            ZoomDuration = 200;
            ZoomEasing = Easing.OutQuint;
            ScrollbarVisible = false;
        }

        [BackgroundDependencyLoader]
        private void load(GarbusConfigManager config)
        {
            // Store as fields to prevent GC collection after load() returns.
            waveformOpacityBindable = config.GetBindable<double>(GarbusSetting.EditorWaveformOpacity);
            showTicksBindable = config.GetBindable<bool>(GarbusSetting.EditorShowTicks);
            showTimingChangesBindable = config.GetBindable<bool>(GarbusSetting.EditorShowTimingChanges);
            showDesignRegionsBindable = config.GetBindable<bool>(GarbusSetting.EditorShowDesignRegions);

            var waveformOpacity = waveformOpacityBindable;
            var showTicks = showTicksBindable;
            var showTimingChanges = showTimingChangesBindable;
            var showDesignRegions = showDesignRegionsBindable;

            // Waveform layer — bottom. Populated later via editorClock.TrackChanged → updateWaveform.

            AddRange(new Drawable[]
            {
                waveform = new WaveformGraph
                {
                    RelativeSizeAxes = Axes.Both,
                    BaseColour = Colour4.Blue.Opacity(0.2f),
                    LowColour = Colour4.CornflowerBlue,
                    MidColour = Colour4.DodgerBlue,
                    HighColour = Colour4.SteelBlue,
                    Waveform = null, // populated below if track has waveform
                },
                ticks = new TimelineTickDisplay(),
                designRegions = new TimelineDesignRegionDisplay(),
                timingChanges = new TimelineTimingChangeDisplay(),
                objectMarkers = new TimelineObjectMarkers(),
            });

            // Non-scrolling CentreMarker: AddInternal makes it a fixed viewport overlay rather than a
            // child of the scrolling (and full-track-width auto-sized) ScrollContent, so it stays pinned
            // to the horizontal centre of the visible strip. The timeline scrolls to keep the current
            // time at that centre, so the marker indicates EditorTime. (Matches osu's Timeline.cs.)
            AddInternal(new CentreMarker
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
            });

            // Wire config toggles.
            waveformOpacity.BindValueChanged(e => waveform.Alpha = (float)e.NewValue, true);
            showTicks.BindValueChanged(e => ticks.Alpha = e.NewValue ? 1 : 0, true);
            showTimingChanges.BindValueChanged(e => timingChanges.Alpha = e.NewValue ? 1 : 0, true);
            showDesignRegions.BindValueChanged(e => designRegions.Alpha = e.NewValue ? 1 : 0, true);

            // Try to load waveform from track file.
            editorClock.TrackChanged += updateWaveform;
            updateWaveform();
        }

        protected override void Update()
        {
            base.Update();

            // Reserve half the drawable width as margin on each side so time=0 and time=TrackLength
            // sit at the centre when scrolled to the extremes.
            Content.Margin = new MarginPadding { Horizontal = DrawWidth / 2 };

            // Recalculate zoom range if track length changed.
            if (editorClock.TrackLength != trackLengthForZoom)
                setupZoomFromTrackLength();

            // Follow clock when playing.
            if (editorClock.IsRunning)
                scrollToTrackTime();
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (handlingDragInput)
            {
                seekTrackToCurrent();
            }
            else if (!editorClock.IsRunning)
            {
                if (Current != lastScrollPosition && editorClock.CurrentTime == lastTrackTime && !editorClock.IsSeeking)
                    seekTrackToCurrent();
                else
                    scrollToTrackTime();
            }

            lastScrollPosition = Current;
            lastTrackTime = editorClock.CurrentTime;
        }

        private void setupZoomFromTrackLength()
        {
            double trackLength = editorClock.TrackLength;
            if (trackLength <= 0)
                return;

            // Target ~6000 ms visible at default zoom.
            float defaultZoom = getZoomLevelForVisibleMs(6000, trackLength);
            float minZoom = getZoomLevelForVisibleMs(60000, trackLength);
            float maxZoom = getZoomLevelForVisibleMs(500, trackLength);

            minZoom = Math.Max(1, minZoom);
            maxZoom = Math.Max(minZoom + 1, maxZoom);
            float initialZoom = Math.Clamp(defaultZoom, minZoom, maxZoom);

            SetupZoom(initialZoom, minZoom, maxZoom);

            trackLengthForZoom = trackLength;
        }

        private static float getZoomLevelForVisibleMs(double visibleMs, double trackLength)
            => Math.Max(1f, (float)(trackLength / visibleMs));

        protected override void OnZoomChanged()
        {
            base.OnZoomChanged();
            // TimelineTimeRange sync is handled by ComposeTab subscribing to CurrentZoom.
        }

        // ---- Seek & scroll helpers ----

        private void seekTrackToCurrent()
        {
            double target = TimeAtPosition(Current);
            target = Math.Clamp(target, 0, editorClock.TrackLength);

            // Raw (unsnapped) seek — the waveform display is never subject to beat snapping.
            editorClock.Seek(target);
        }

        private void scrollToTrackTime()
        {
            if (editorClock.TrackLength == 0) return;
            float position = PositionAtTime(editorClock.CurrentTime);
            ScrollTo(position, false);
        }

        public double TimeAtPosition(double x)
            => x / Content.DrawWidth * editorClock.TrackLength;

        public float PositionAtTime(double time)
            => (float)(time / editorClock.TrackLength * Content.DrawWidth);

        // ---- Mouse drag: raw seek throughout, no snap on release ----

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != osuTK.Input.MouseButton.Left)
                return false;

            if (base.OnMouseDown(e))
                beginUserDrag();

            return true;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            endUserDrag();
            base.OnMouseUp(e);
        }

        private void beginUserDrag()
        {
            handlingDragInput = true;
            trackWasPlaying = editorClock.IsRunning;
            editorClock.Stop();
        }

        private void endUserDrag()
        {
            handlingDragInput = false;
            if (trackWasPlaying)
                editorClock.Start();
        }

        // ---- Waveform ----

        private void updateWaveform()
        {
            // Read the raw encoded-audio file from the song directory. Null for an unsaved song,
            // no audio file, a missing file, or a TrackVirtual — WaveformGraph renders nothing then.
            var stream = songFile.GetAudioStream();

            waveform.Waveform = null;
            currentWaveform?.Dispose();
            currentWaveform = stream == null ? null : new Waveform(stream);
            waveform.Waveform = currentWaveform;

            Scheduler.AddOnce(applyVisualOffset);
        }

        // Shift the waveform earlier by GarbusEditor.WAVEFORM_VISUAL_OFFSET so timing points line
        // up with the audio the same way they do in osu's editor (which bakes in an assumption of
        // full system latency). Mirrors osu's Timeline.applyVisualOffset. X is relative to the
        // zoomed content width, which spans the whole track — so the shift is the fraction of the
        // track that the offset represents.
        private void applyVisualOffset()
        {
            waveform.RelativePositionAxes = Axes.X;

            double trackLength = editorClock.TrackLength;
            if (trackLength > 0)
                waveform.X = -(float)(GarbusEditor.WAVEFORM_VISUAL_OFFSET / trackLength);
            else
                // Track length can momentarily read 0 right after a track switch; retry once it populates.
                Scheduler.AddOnce(applyVisualOffset);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (editorClock != null)
                editorClock.TrackChanged -= updateWaveform;
            currentWaveform?.Dispose();
        }
    }
}
