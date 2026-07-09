using osu.Framework.Platform;
using osu.Framework;
using Garbus.Game;

namespace Garbus.Desktop
{
    public static class Program
    {
        public static void Main()
        {
            using (GameHost host = Host.GetSuitableDesktopHost(@"Garbus"))
            using (osu.Framework.Game game = new GarbusGame())
                host.Run(game);
        }
    }
}
