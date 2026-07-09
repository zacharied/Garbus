using osu.Framework.iOS;
using Garbus.Game;

namespace Garbus.iOS
{
    /// <inheritdoc />
    public class AppDelegate : GameApplicationDelegate
    {
        /// <inheritdoc />
        protected override osu.Framework.Game CreateGame() => new GarbusGame();
    }
}
