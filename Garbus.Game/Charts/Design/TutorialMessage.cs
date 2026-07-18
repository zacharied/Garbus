// A design point that dims the gameplay screen with a translucent black overlay and shows a text
// message while active. The reference (first) concrete design-point effect. Overlay opacity is a
// fixed constant, not an authored value.

using osu.Framework.Bindables;

namespace Garbus.Game.Charts.Design
{
    public class TutorialMessage : DesignPoint
    {
        public const float OVERLAY_OPACITY = 0.6f;

        public readonly Bindable<string> TextBindable = new Bindable<string>(string.Empty);

        public string Text
        {
            get => TextBindable.Value;
            set => TextBindable.Value = value;
        }
    }
}
