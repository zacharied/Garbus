// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Beatmaps/FramedBeatmapClock.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: OsuConfigManager → GarbusConfigManager; the realm-backed per-beatmap offset
// subscription is replaced by the plain ChartOffset property (to be fed from the chart file later).

using System;
using System.Diagnostics;
using Garbus.Game.Configuration;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Timing;

namespace Garbus.Game.Timing
{
    /// <summary>
    /// A clock intended to be the single source-of-truth for chart timing.
    ///
    /// It provides some functionality:
    ///  - Optionally applies (and tracks changes of) chart, user, and platform offsets (see ctor argument applyOffsets).
    ///  - Adjusts <see cref="Seek"/> operations to account for any applied offsets, seeking in raw "chart" time values.
    ///  - Allows changing the source to a new track.
    /// </summary>
    public partial class FramedChartClock : Component, IFrameBasedClock, IAdjustableClock, ISourceChangeableClock
    {
        private readonly bool applyOffsets;

        private readonly OffsetCorrectionClock? userGlobalOffsetClock;
        private readonly OffsetCorrectionClock? platformOffsetClock;
        private readonly FramedOffsetClock? chartOffsetClock;

        private readonly IFrameBasedClock finalClockSource;

        private Bindable<double>? userAudioOffset;

        private readonly DecouplingFramedClock decoupledTrack;
        private readonly InterpolatingFramedClock interpolatedTrack;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        private Bindable<bool> experimentalAudio = null!;

        private double chartOffset;

        /// <summary>
        /// Per-chart audio offset in milliseconds (the equivalent of osu's per-beatmap user offset).
        /// </summary>
        public double ChartOffset
        {
            get => chartOffset;
            set
            {
                chartOffset = value;

                if (chartOffsetClock != null)
                    chartOffsetClock.Offset = value;
            }
        }

        /// <summary>
        /// Store seek target for any <see cref="Seek"/> performed before <see cref="LoadState.Loaded"/>.
        /// </summary>
        /// <remarks>
        /// Initial seeks must be delayed until the <see cref="FramedChartClock"/> instance is fully loaded.
        ///
        /// If not, <see cref="TotalAppliedOffset"/> will not be in a correct state and result in a potentially
        /// incorrect seek target.</remarks>
        private double? initialSeek;

        public bool IsRewinding { get; private set; }

        public FramedChartClock(bool applyOffsets, bool requireDecoupling, IClock? source = null)
        {
            this.applyOffsets = applyOffsets;

            decoupledTrack = new DecouplingFramedClock(source) { AllowDecoupling = requireDecoupling };

            // An interpolating clock is used to ensure precise time values even when the host audio subsystem is not reporting
            // high precision times (on windows there's generally only 5-10ms reporting intervals, as an example).
            interpolatedTrack = new InterpolatingFramedClock(decoupledTrack)
            {
                DriftRecoveryHalfLife = 80,
            };

            if (applyOffsets)
            {
                platformOffsetClock = new OffsetCorrectionClock(interpolatedTrack);

                // User global offset (set in settings) should also be applied.
                userGlobalOffsetClock = new OffsetCorrectionClock(platformOffsetClock);

                // Per-chart offset will be applied to this final clock.
                finalClockSource = chartOffsetClock = new FramedOffsetClock(userGlobalOffsetClock);
            }
            else
            {
                finalClockSource = interpolatedTrack;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (applyOffsets)
            {
                Debug.Assert(userGlobalOffsetClock != null);

                userAudioOffset = config.GetBindable<double>(GarbusSetting.AudioOffset);
                userAudioOffset.BindValueChanged(offset => userGlobalOffsetClock.Offset = offset.NewValue, true);

                experimentalAudio = audioManager.UseExperimentalWasapi.GetBoundCopy();
                experimentalAudio.BindValueChanged(_ => updatePlatformOffset(), true);
            }

            if (initialSeek != null)
            {
                Seek(initialSeek.Value);
                initialSeek = null;
            }
        }

        /// <summary>
        /// Audio timings in general with newer BASS versions don't match stable.
        /// This only seems to be required on windows. We need to eventually figure out why, with a bit of luck.
        /// </summary>
        public const double WINDOWS_BASE_AUDIO_OFFSET = 15;

        /// <summary>
        /// An additional offset applied to account for experimental mode being much better.
        /// </summary>
        public const double WINDOWS_EXPERIMENTAL_AUDIO_OFFSET = -25;

        private void updatePlatformOffset()
        {
            if (!applyOffsets)
                return;

            Debug.Assert(platformOffsetClock != null);

            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.Windows:
                    platformOffsetClock.Offset = WINDOWS_BASE_AUDIO_OFFSET;

                    if (audioManager.UseExperimentalWasapi.Value)
                        platformOffsetClock.Offset += WINDOWS_EXPERIMENTAL_AUDIO_OFFSET;
                    return;

                default:
                    platformOffsetClock.Offset = 0;
                    break;
            }
        }

        protected override void Update()
        {
            base.Update();

            finalClockSource.ProcessFrame();

            if (Clock.ElapsedFrameTime != 0)
                IsRewinding = Clock.ElapsedFrameTime < 0;
        }

        public double TotalAppliedOffset
        {
            get
            {
                if (!applyOffsets)
                    return 0;

                Debug.Assert(userGlobalOffsetClock != null);
                Debug.Assert(chartOffsetClock != null);
                Debug.Assert(platformOffsetClock != null);

                return userGlobalOffsetClock.RateAdjustedOffset + chartOffsetClock.Offset + platformOffsetClock.RateAdjustedOffset;
            }
        }

        #region Delegation of IAdjustableClock / ISourceChangeableClock to decoupled clock.

        public void ChangeSource(IClock? source) => decoupledTrack.ChangeSource(source);

        public IClock Source => decoupledTrack.Source;

        public void Reset()
        {
            decoupledTrack.Reset();
            finalClockSource.ProcessFrame();
        }

        public void Start()
        {
            decoupledTrack.Start();
            finalClockSource.ProcessFrame();
        }

        public void Stop()
        {
            decoupledTrack.Stop();
            finalClockSource.ProcessFrame();
        }

        public bool Seek(double position)
        {
            // TotalAppliedOffset will not be correct until fully loaded.
            if (applyOffsets && !IsLoaded)
            {
                initialSeek = position;
                return true;
            }

            bool success = decoupledTrack.Seek(position - TotalAppliedOffset);
            finalClockSource.ProcessFrame();

            return success;
        }

        public void ResetSpeedAdjustments() => decoupledTrack.ResetSpeedAdjustments();

        public double Rate
        {
            get => decoupledTrack.Rate;
            set => decoupledTrack.Rate = value;
        }

        #endregion

        #region Delegation of IFrameBasedClock to clock with all offsets applied

        public double CurrentTime => initialSeek ?? finalClockSource.CurrentTime;

        public bool IsRunning => finalClockSource.IsRunning;

        public void ProcessFrame()
        {
            // Noop to ensure an external consumer doesn't process the internal clock an extra time.
        }

        public double ElapsedFrameTime => finalClockSource.ElapsedFrameTime;

        public double FramesPerSecond => finalClockSource.FramesPerSecond;

        #endregion

        public string GetSnapshot()
        {
            return
                $"originalSource: {output(Source)}\n" +
                $"userGlobalOffsetClock: {output(userGlobalOffsetClock)}\n" +
                $"platformOffsetClock: {output(platformOffsetClock)}\n" +
                $"chartOffsetClock: {output(chartOffsetClock)}\n" +
                $"interpolatedTrack: {output(interpolatedTrack)}\n" +
                $"decoupledTrack: {output(decoupledTrack)}\n" +
                $"finalClockSource: {output(finalClockSource)}\n";

            string output(IClock? clock)
            {
                if (clock == null)
                    return "null";

                if (clock is IFrameBasedClock framed)
                    return $"current: {clock.CurrentTime:N2} running: {clock.IsRunning} rate: {clock.Rate} elapsed: {framed.ElapsedFrameTime:N2}";

                return $"current: {clock.CurrentTime:N2} running: {clock.IsRunning} rate: {clock.Rate}";
            }
        }
    }
}
