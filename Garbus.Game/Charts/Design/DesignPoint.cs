// Base for a time-ranged visual effect applied to the chart during gameplay. Concrete subclasses
// (e.g. TutorialMessage) add their own effect parameters. StartTime/EndTime are bindables so the
// editor's list rows and settings pane react to edits and auto-unbind on disposal.

using osu.Framework.Bindables;

namespace Garbus.Game.Charts.Design
{
    public abstract class DesignPoint
    {
        public readonly BindableDouble StartTimeBindable = new BindableDouble();
        public readonly BindableDouble EndTimeBindable = new BindableDouble();

        public double StartTime
        {
            get => StartTimeBindable.Value;
            set => StartTimeBindable.Value = value;
        }

        public double EndTime
        {
            get => EndTimeBindable.Value;
            set => EndTimeBindable.Value = value;
        }
    }
}
