// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Play/MasterGameplayClockContainer.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: takes a Track directly instead of a WorkingBeatmap; a fixed LEAD_IN_TIME
// count-in replaces osu's storyboard/AudioLeadIn start-time inference (Garbus has neither);
// MusicController, mod adjustments and IBeatSyncProvider removed.

using System;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Logging;

namespace Garbus.Game.Timing
{
    /// <summary>
    /// A <see cref="GameplayClockContainer"/> which uses a chart's <see cref="Track"/> as a source.
    /// <para>
    /// This is the most complete <see cref="GameplayClockContainer"/> which takes into account all user and platform offsets,
    /// and provides implementations for user actions such as skipping or adjusting playback rates that may occur during gameplay.
    /// </para>
    /// </summary>
    public partial class MasterGameplayClockContainer : GameplayClockContainer
    {
        /// <summary>
        /// Duration before gameplay start time required before skip button displays.
        /// </summary>
        public const double MINIMUM_SKIP_TIME = 1000;

        /// <summary>
        /// Silent count-in before gameplay begins. The clock starts this far before
        /// <see cref="GameplayClockContainer.GameplayStartTime"/> and runs through the
        /// negative time on the decoupled clock's realtime reference, coupling to the
        /// audio track exactly at the gameplay start.
        /// </summary>
        public const double LEAD_IN_TIME = 3000;

        public readonly BindableNumber<double> UserPlaybackRate = new BindableDouble(1)
        {
            MinValue = 0.05,
            MaxValue = 2,
            Precision = 0.01,
        };

        /// <summary>
        /// Whether the audio playback rate should be validated.
        /// Mostly disabled for tests.
        /// </summary>
        internal bool ShouldValidatePlaybackRate { get; init; }

        /// <summary>
        /// Whether the audio playback is within acceptable ranges.
        /// Will become false if audio playback is not going as expected.
        /// </summary>
        public IBindable<bool> PlaybackRateValid => playbackRateValid;

        private readonly Bindable<bool> playbackRateValid = new Bindable<bool>(true);

        private Track track;

        /// <summary>
        /// Create a new master gameplay clock container.
        /// </summary>
        /// <param name="track">The chart's audio track.</param>
        /// <param name="gameplayStartTime">The latest time which should be used when introducing gameplay. Will be used when skipping forward.</param>
        public MasterGameplayClockContainer(Track track, double gameplayStartTime)
            : base(track, applyOffsets: true, requireDecoupling: true)
        {
            this.track = track;

            GameplayStartTime = gameplayStartTime;

            // Begin a fixed lead-in before the intended gameplay start. The decoupled clock runs
            // through the negative time (screen empty, audio silent) and couples to the track exactly
            // at GameplayStartTime. Deliberately unconditional (no Math.Min(0, …)) so a positive
            // mid-song start — the editor Test path — is honoured rather than clamped to 0.
            StartTime = gameplayStartTime - LEAD_IN_TIME;
        }

        public override void Seek(double time)
        {
            elapsedValidationTime = null;

            base.Seek(time);
        }

        protected override void StartGameplayClock()
        {
            addAdjustmentsToTrack();
            base.StartGameplayClock();
        }

        /// <summary>
        /// Skip forward to the next valid skip point.
        /// </summary>
        public void Skip()
        {
            if (CurrentTime > GameplayStartTime - MINIMUM_SKIP_TIME)
                return;

            Seek(GameplayStartTime - MINIMUM_SKIP_TIME);
        }

        /// <summary>
        /// Changes the backing clock to avoid using the originally provided track.
        /// </summary>
        public void StopUsingSourceTrack()
        {
            removeAdjustmentsFromTrack();

            track = new TrackVirtual(track.Length);
            track.Seek(CurrentTime);
            if (IsRunning)
                track.Start();
            ChangeSource(track);

            addAdjustmentsToTrack();
        }

        protected override void Update()
        {
            base.Update();
            checkPlaybackValidity();
        }

        #region Clock validation (ensure things are running correctly for local gameplay)

        private double elapsedGameplayClockTime;
        private double? elapsedValidationTime;
        private int playbackDiscrepancyCount;

        private const int allowed_playback_discrepancies = 5;

        private void checkPlaybackValidity()
        {
            if (!ShouldValidatePlaybackRate)
                return;

            if (GameplayClock.IsRunning)
            {
                elapsedGameplayClockTime += GameplayClock.ElapsedFrameTime;

                if (elapsedValidationTime == null)
                    elapsedValidationTime = elapsedGameplayClockTime;
                else
                    elapsedValidationTime += GameplayClock.Rate * Time.Elapsed;

                if (Math.Abs(elapsedGameplayClockTime - elapsedValidationTime!.Value) > 300)
                {
                    if (playbackDiscrepancyCount++ > allowed_playback_discrepancies)
                    {
                        if (playbackRateValid.Value)
                        {
                            playbackRateValid.Value = false;
                            Logger.Log("System audio playback is not working as expected. Please check your audio drivers.", level: LogLevel.Important);
                        }
                    }
                    else
                    {
                        Logger.Log(
                            $"Playback discrepancy detected ({playbackDiscrepancyCount} of allowed {allowed_playback_discrepancies}): {elapsedGameplayClockTime:N1} vs {elapsedValidationTime:N1}");
                    }

                    elapsedValidationTime = null;
                }
            }
        }

        #endregion

        private bool speedAdjustmentsApplied;

        private void addAdjustmentsToTrack()
        {
            if (speedAdjustmentsApplied)
                return;

            track.AddAdjustment(AdjustableProperty.Frequency, UserPlaybackRate);

            speedAdjustmentsApplied = true;
        }

        private void removeAdjustmentsFromTrack()
        {
            if (!speedAdjustmentsApplied)
                return;

            track.RemoveAdjustment(AdjustableProperty.Frequency, UserPlaybackRate);

            speedAdjustmentsApplied = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            removeAdjustmentsFromTrack();
        }
    }
}
