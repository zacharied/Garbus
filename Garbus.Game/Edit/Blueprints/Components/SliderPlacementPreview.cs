using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>
/// Waiting-state slider preview: a line extending toward future time for a normal slider, or a dot
/// when Ctrl changes the next click into a head-only slider placement.
/// </summary>
internal partial class SliderPlacementPreview : CompositeDrawable
{
    internal Box FadingLine { get; }
    internal Circle HeadOnlyDot { get; }

    internal Colour4 PreviewColour { get; private set; }

    public SliderPlacementPreview()
    {
        Size = new Vector2(36, 80);
        Origin = Anchor.BottomCentre;

        InternalChildren = new Drawable[]
        {
            FadingLine = new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Width = 6,
                RelativeSizeAxes = Axes.Y,
            },
            HeadOnlyDot = new Circle
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.Centre,
                Size = new Vector2(12),
            },
        };
    }

    internal void SetState(bool headOnly, Colour4 colour)
    {
        PreviewColour = colour;

        FadingLine.Alpha = headOnly ? 0 : 1;
        FadingLine.Colour = ColourInfo.GradientVertical(colour.Opacity(0), colour);

        HeadOnlyDot.Alpha = headOnly ? 1 : 0;
        HeadOnlyDot.Colour = colour;
    }
}
