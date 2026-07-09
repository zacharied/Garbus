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
        }
    }
}
