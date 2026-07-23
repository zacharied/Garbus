// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/StickIndicator.cs).

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using Garbus.Game.Core;
using Garbus.Game.Input;
using Garbus.Game.Objects.Drawables;
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
    private StickCentreSpike centreSpike = null!;

    public StickIndicator()
    {
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddRangeInternal([
            centreSpike = new StickCentreSpike(
                Side == HorizontalDirection.Left ? Constants.LeftColour : Constants.RightColour),
            arc = new Arc(thickness: 12)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Size = new Vector2(RadiusScale),
                Colour = Side == HorizontalDirection.Left ? Colour4.Blue : Colour4.Red,
                Alpha = 1f,
            },
        ]);
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

        float ringRadius = MathF.Min(ChildSize.X, ChildSize.Y) / 2;
        centreSpike.SetState(sliderCatcher.Angle, ringRadius, sliderCatcher.Activated);
        arc.Alpha = sliderCatcher.Activated ? 1 : 0;
    }
}
