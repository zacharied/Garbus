using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Input;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects.Judgement;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableSlamCentered : DrawableGarbusHitObject<GarbusSlamCentered>
{
    private readonly Sprite sprite;

    [Resolved]
    private AnalogInputManager analogInput { get; set; } = null!;

    public DrawableSlamCentered(GarbusSlamCentered hitObject)
        : base(hitObject)
    {
        Size = new Vector2(80);
        sprite = new Sprite
        {
            RelativeSizeAxes = Axes.Both,
            FillMode = FillMode.Fit,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = hitObject.Side == HorizontalDirection.Left ? Constants.LeftColour : Constants.RightColour
        };
        Origin = Anchor.Centre;
        Rotation = 90 - HitObject.AngleDeg;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        sprite.Texture = textures.Get("arrow");
        AddInternal(sprite);
    }

    protected override void PrepareForUse()
    {
        // Apply note spawn effect
        sprite.ScaleTo(0).ScaleTo(1, 125, Easing.In);
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        const double duration = 1000;

        switch (state)
        {
            case ArmedState.Hit:
                sprite
                    .Spin(700, RotationDirection.Clockwise)
                    .FadeOut(350, Easing.OutQuint)
                    .ScaleTo(new Vector2(2), 350, Easing.OutQuint)
                    .OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                sprite.FadeColour(Color4.Red, duration);
                sprite.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
        }
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset < -SlamHitWindows.PERFECT_WINDOW)
            return; // early-permissive watch window not open yet

        if (analogInput.StickGestureTrackers[HitObject.Side].TryGetFlickTime(
                HitObject.AngleDeg,
                HitObject.StartTime - SlamHitWindows.PERFECT_WINDOW,
                out double gestureTime))
        {
            HitResult result = HitObject.HitWindows.ResultFor(gestureTime - HitObject.StartTime);
            if (result != HitResult.None)
            {
                ApplyResult(result);
                return;
            }
        }

        if (timeOffset > SlamHitWindows.LATE_NEAR_WINDOW)
            ApplyMinResult(); // Miss — window elapsed with no flick
    }
}
