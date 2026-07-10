// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/EditorClock.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: FramedBeatmapClock → FramedChartClock; IBeatmap.ControlPointInfo →
// constructor-injected settable ControlPointInfo property (no osu.Game.Beatmaps dependency);
// constructor takes (ControlPointInfo, double trackLength, BindableBeatDivisor); TrackLength
// uses injected fallback when track is not yet loaded; IBeatSyncProvider/kiai plumbing removed
// (no osu.Game.Rulesets.Edit types in Garbus); applyOffsets: false on the inner clock (editor
// needs no gameplay latency corrections); namespace Garbus.Game.Edit.

#nullable disable

using System;
using System.Diagnostics;
using System.Linq;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Timing;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Transforms;
using osu.Framework.Timing;
using osu.Framework.Utils;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// A decoupled clock which adds editor-specific functionality, such as snapping to a user-defined beat divisor.
    /// </summary>
    public partial class EditorClock : CompositeComponent, IFrameBasedClock, IAdjustableClock, ISourceChangeableClock
    {
        public event Action TrackChanged;

        private readonly Bindable<Track> track = new Bindable<Track>();

        private readonly double fallbackTrackLength;

        public double TrackLength => track.Value?.IsLoaded == true ? track.Value.Length : fallbackTrackLength;

        public AudioAdjustments AudioAdjustments { get; } = new AudioAdjustments();

        /// <summary>
        /// The control point info for the current chart. Can be swapped by the editor when a chart loads.
        /// </summary>
        public ControlPointInfo ControlPointInfo { get; set; }

        private readonly BindableBeatDivisor beatDivisor;

        private readonly FramedChartClock underlyingClock;

        private bool playbackFinished;

        public IBindable<bool> SeekingOrStopped => seekingOrStopped;

        private readonly Bindable<bool> seekingOrStopped = new Bindable<bool>(true);

        /// <summary>
        /// Whether a seek is currently in progress. True for the duration of a seek performed via <see cref="SeekSmoothlyTo"/>.
        /// </summary>
        public bool IsSeeking { get; private set; }

        public EditorClock(ControlPointInfo controlPointInfo, double trackLength, BindableBeatDivisor beatDivisor = null)
        {
            ControlPointInfo = controlPointInfo;
            fallbackTrackLength = trackLength;

            this.beatDivisor = beatDivisor ?? new BindableBeatDivisor();

            underlyingClock = new FramedChartClock(applyOffsets: false, requireDecoupling: true);
            AddInternal(underlyingClock);

            track.BindValueChanged(_ => TrackChanged?.Invoke());
        }

        /// <summary>
        /// Seek to the closest snappable beat from a time.
        /// </summary>
        /// <param name="position">The raw position which should be seeked around.</param>
        /// <returns>Whether the seek could be performed.</returns>
        public bool SeekSnapped(double position)
        {
            var timingPoint = ControlPointInfo.TimingPointAt(position);
            double beatSnapLength = timingPoint.BeatLength / beatDivisor.Value;

            // We will be snapping to beats within the timing point
            position -= timingPoint.Time;

            // Determine the index from the current timing point of the closest beat to position
            int closestBeat = (int)Math.Round(position / beatSnapLength);
            position = timingPoint.Time + closestBeat * beatSnapLength;

            // Depending on beatSnapLength, we may snap to a beat that is beyond timingPoint's end time, but we want to instead snap to
            // the next timing point's start time
            var nextTimingPoint = ControlPointInfo.TimingPoints.FirstOrDefault(t => t.Time > timingPoint.Time);
            if (position > nextTimingPoint?.Time)
                position = nextTimingPoint.Time;

            return Seek(position);
        }

        /// <summary>
        /// Seeks backwards by one beat length.
        /// </summary>
        /// <param name="snapped">Whether to snap to the closest beat after seeking.</param>
        /// <param name="amount">The relative amount (magnitude) which should be seeked.</param>
        public void SeekBackward(bool snapped = false, double amount = 1) => seek(-1, snapped, amount);

        /// <summary>
        /// Seeks forwards by one beat length.
        /// </summary>
        /// <param name="snapped">Whether to snap to the closest beat after seeking.</param>
        /// <param name="amount">The relative amount (magnitude) which should be seeked.</param>
        public void SeekForward(bool snapped = false, double amount = 1) => seek(1, snapped, amount);

        private void seek(int direction, bool snapped, double amount = 1)
        {
            double current = CurrentTimeAccurate;

            if (amount <= 0) throw new ArgumentException("Value should be greater than zero", nameof(amount));

            var timingPoint = ControlPointInfo.TimingPointAt(current);

            if (IsRunning)
            {
                // when track is playing, seek a bit more than usual.
                // the amount adjusted matches stable, because don't-break-what-works.
                // https://github.com/peppy/osu-stable-reference/blob/7519cafd1823f1879c0d9c991ba0e5c7fd3bfa02/osu!/GameModes/Edit/Editor.cs#L1639-L1640
                //
                // ReSharper disable once PossibleLossOfFraction
                amount *= 1 + 250 / (int)timingPoint.BeatLength;
            }

            if (direction < 0 && timingPoint.Time == current)
                // When going backwards and we're at the boundary of two timing points, we compute the seek distance with the timing point which we are seeking into
                timingPoint = ControlPointInfo.TimingPointAt(current - 1);

            double seekAmount = timingPoint.BeatLength / beatDivisor.Value * amount;
            double seekTime = current + seekAmount * direction;

            if (!snapped || ControlPointInfo.TimingPoints.Count == 0)
            {
                SeekSmoothlyTo(seekTime);
                return;
            }

            // We will be snapping to beats within timingPoint
            seekTime -= timingPoint.Time;

            // Determine the index from timingPoint of the closest beat to seekTime, accounting for scrolling direction
            int closestBeat;
            if (direction > 0)
                closestBeat = (int)Math.Floor(seekTime / seekAmount);
            else
                closestBeat = (int)Math.Ceiling(seekTime / seekAmount);

            seekTime = timingPoint.Time + closestBeat * seekAmount;

            // limit forward seeking to only up to the next timing point's start time.
            var nextTimingPoint = ControlPointInfo.TimingPointAfter(timingPoint.Time);
            if (seekTime > nextTimingPoint?.Time)
                seekTime = nextTimingPoint.Time;

            // Due to the rounding above, we may end up on the current beat. This will effectively cause 0 seeking to happen, but we don't want this.
            // Instead, we'll go to the next beat in the direction when this is the case
            if (Precision.AlmostEquals(current, seekTime, 0.5f))
            {
                closestBeat += direction > 0 ? 1 : -1;
                seekTime = timingPoint.Time + closestBeat * seekAmount;
            }

            if (seekTime < timingPoint.Time && !ReferenceEquals(timingPoint, ControlPointInfo.TimingPoints.First()))
                seekTime = timingPoint.Time;

            SeekSmoothlyTo(seekTime);
        }

        /// <summary>
        /// The current time of this clock, including any active transform seeks performed via <see cref="SeekSmoothlyTo"/>.
        /// </summary>
        public double CurrentTimeAccurate =>
            Transforms.OfType<TransformSeek>().LastOrDefault()?.EndValue ?? CurrentTime;

        public double CurrentTime => underlyingClock.CurrentTime;

        public void Reset()
        {
            ClearTransforms();
            underlyingClock.Reset();
        }

        public void Start()
        {
            ClearTransforms();

            if (playbackFinished)
                underlyingClock.Seek(0);

            underlyingClock.Start();
        }

        public void Stop()
        {
            seekingOrStopped.Value = true;
            underlyingClock.Stop();
        }

        public bool Seek(double position)
        {
            seekingOrStopped.Value = IsSeeking = true;

            ClearTransforms();

            // Ensure the sought point is within the boundaries
            position = Math.Clamp(position, 0, TrackLength);
            return underlyingClock.Seek(position);
        }

        /// <summary>
        /// Seek smoothly to the provided destination, if within a certain proximity to the current viewport.
        /// Use <see cref="Seek"/> to perform an immediate seek.
        /// </summary>
        /// <param name="seekDestination"></param>
        public void SeekSmoothlyTo(double seekDestination)
        {
            seekingOrStopped.Value = true;

            // The whole point of seeking smoothly is to maintain continuity for the user.
            // Above a certain proximity, there's little reason to do this as the jump is already huge.
            const double smooth_seek_max_proximity = 5000;

            if (IsRunning || Math.Abs(seekDestination - currentTime) > smooth_seek_max_proximity)
            {
                Seek(seekDestination);
                return;
            }

            transformSeekTo(seekDestination, transform_time, Easing.OutQuint);
        }

        public void BindAdjustments() => track.Value?.BindAdjustments(AudioAdjustments);

        public void UnbindAdjustments() => track.Value?.UnbindAdjustments(AudioAdjustments);

        public void ResetSpeedAdjustments()
        {
            AudioAdjustments.RemoveAllAdjustments(AdjustableProperty.Frequency);
            AudioAdjustments.RemoveAllAdjustments(AdjustableProperty.Tempo);
            underlyingClock.ResetSpeedAdjustments();
        }

        double IAdjustableClock.Rate
        {
            get => underlyingClock.Rate;
            set => underlyingClock.Rate = value;
        }

        double IClock.Rate => underlyingClock.Rate;

        public bool IsRunning => underlyingClock.IsRunning;

        public void ProcessFrame()
        {
            // Noop to ensure an external consumer doesn't process the internal clock an extra time.
        }

        public double ElapsedFrameTime => underlyingClock.ElapsedFrameTime;

        public double FramesPerSecond => underlyingClock.FramesPerSecond;

        public void ChangeSource(IClock source)
        {
            UnbindAdjustments();

            track.Value = source as Track;

            // Route the audio track through a coalescing, non-blocking seek wrapper so interactive scrubbing
            // doesn't stall the update thread on per-frame track seeks (see BackgroundSeekingClock).
            IClock clockSource = source is IAdjustableClock adjustable ? new BackgroundSeekingClock(adjustable) : source;
            underlyingClock.ChangeSource(clockSource);

            BindAdjustments();
        }

        public IClock Source => underlyingClock.Source;

        private const double transform_time = 300;

        protected override void Update()
        {
            base.Update();

            // EditorClock wasn't being added in many places. This gives us more certainty that it is.
            Debug.Assert(underlyingClock.LoadState > LoadState.NotLoaded);

            playbackFinished = CurrentTime >= TrackLength;

            if (playbackFinished)
            {
                if (IsRunning)
                    underlyingClock.Stop();

                if (CurrentTime > TrackLength)
                    underlyingClock.Seek(TrackLength);
            }

            updateSeekingState();
        }

        private void updateSeekingState()
        {
            if (seekingOrStopped.Value)
            {
                IsSeeking &= Transforms.Any();

                if (!IsRunning)
                {
                    // seeking in the editor can happen while the track isn't running.
                    // in this case we always want to expose ourselves as seeking (to avoid sample playback).
                    return;
                }

                // we are either running a seek tween or doing an immediate seek.
                // in the case of an immediate seek the seeking bool will be set to false after one update.
                // this allows for silencing hit sounds and the likes.
                seekingOrStopped.Value = IsSeeking;
            }
        }

        private void transformSeekTo(double seek, double duration = 0, Easing easing = Easing.None)
            => this.TransformTo(this.PopulateTransform(new TransformSeek(), Math.Clamp(seek, 0, TrackLength), duration, easing));

        private double currentTime
        {
            get => underlyingClock.CurrentTime;
            set => underlyingClock.Seek(value);
        }

        private class TransformSeek : Transform<double, EditorClock>
        {
            public override string TargetMember => nameof(currentTime);

            protected override void Apply(EditorClock clock, double time) => clock.currentTime = valueAt(time);

            private double valueAt(double time)
            {
                if (time < StartTime) return StartValue;
                if (time >= EndTime) return EndValue;

                return Interpolation.ValueAt(time, StartValue, EndValue, StartTime, EndTime, Easing);
            }

            protected override void ReadIntoStartValue(EditorClock clock) => StartValue = clock.currentTime;
        }
    }
}
