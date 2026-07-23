using Garbus.Game.Objects.Drawables;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;

namespace Garbus.Game.UI;

/// <summary>
/// A centre-origin wedge that follows one analog stick and fits inside a slider contact spike's gap.
/// </summary>
public partial class StickCentreSpike : Container
{
    public const float HalfWidthDeg = 0.5f;

    public bool Active { get; private set; }
    public float AngleRadians { get; private set; }
    public ColourInfo SideColour { get; }
    public SpikeBlade Blade { get; private set; } = null!;

    public StickCentreSpike(ColourInfo sideColour)
    {
        SideColour = sideColour;
        RelativeSizeAxes = Axes.Both;
        AlwaysPresent = true;
        Alpha = 0;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(Blade = new SpikeBlade(SideColour));
    }

    public void SetState(float angleRadians, float ringRadius, bool active)
    {
        AngleRadians = angleRadians;
        Active = active && ringRadius > 0;
        Alpha = Active ? 1 : 0;

        if (Active)
            Blade.SetGeometry(angleRadians, 0, ringRadius, HalfWidthDeg, 0.95f);
    }
}
