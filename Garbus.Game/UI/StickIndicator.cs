// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/StickIndicator.cs).

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using Garbus.Game.Core;
using Garbus.Game.Input;
using osuTK;

namespace Garbus.Game.UI;

public partial class StickIndicator : Container
{
    [Resolved]
    private AnalogInputManager analogInputManager { get; set; } = null!;

    /// <summary>
    /// Radius of the arc relative to the playfield ring (1 = on the ring, &gt;1 = outside it).
    /// </summary>
    public float RadiusScale = 1.06f;

    public required HorizontalDirection Side { get; init; }

    private Arc arc = null!;

    public StickIndicator()
    {
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(arc = new Arc(thickness: 12)
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both,
            Size = new Vector2(RadiusScale),
            Colour = Side == HorizontalDirection.Left ? Colour4.Blue : Colour4.Red,
            Alpha = 1f,
        });
    }

    protected override void Update()
    {
        base.Update();

        var sliderCatcher = analogInputManager.SliderCatchers[Side];

        if (sliderCatcher.Activated)
        {
            arc.StartRadians.Value = sliderCatcher.Angle - sliderCatcher.Size / 2;
            arc.EndRadians.Value = sliderCatcher.Angle + sliderCatcher.Size / 2;
        }

        arc.Alpha = sliderCatcher.Activated ? 1 : 0;
    }
}
