using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace Garbus.Game.Configuration
{
    public class GarbusConfigManager : IniConfigManager<GarbusSetting>
    {
        protected override string Filename => @"garbus.ini";

        public GarbusConfigManager(Storage storage)
            : base(storage)
        {
        }

        protected override void InitialiseDefaults()
        {
            SetDefault(GarbusSetting.AudioOffset, 0.0, -500.0, 500.0);

            // Editor view defaults.
            SetDefault(GarbusSetting.EditorShowTimingChanges, true);
            SetDefault(GarbusSetting.EditorShowTicks, true);
            SetDefault(GarbusSetting.EditorWaveformOpacity, 0.25, 0.0, 1.0);
            SetDefault(GarbusSetting.EditorAutoSeekOnPlacement, true);
            SetDefault(GarbusSetting.EditorContractSidebars, false);
            SetDefault(GarbusSetting.EditorLastFileDirectory, string.Empty);
        }
    }
}
