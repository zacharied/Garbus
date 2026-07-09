// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Play/OffsetCorrectionClock.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.

using osu.Framework.Timing;

namespace Garbus.Game.Timing
{
    public class OffsetCorrectionClock : FramedOffsetClock
    {
        private double offset;

        public new double Offset
        {
            get => offset;
            set
            {
                if (value == offset)
                    return;

                offset = value;

                updateOffset();
            }
        }

        public double RateAdjustedOffset => base.Offset;

        public OffsetCorrectionClock(IClock source)
            : base(source)
        {
        }

        public override void ProcessFrame()
        {
            base.ProcessFrame();
            updateOffset();
        }

        private void updateOffset()
        {
            // we always want to apply the same real-time offset, so it should be adjusted by the difference in playback rate (from realtime) to achieve this.
            base.Offset = Offset * Rate;
        }
    }
}
