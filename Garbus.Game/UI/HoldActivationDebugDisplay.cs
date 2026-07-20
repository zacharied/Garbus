using System.Linq;
using Garbus.Game.Objects.Drawables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.UI;

/// <summary>
/// Temporary gameplay diagnostic showing the accumulated activation of the cardinal hold whose body
/// is currently active, whether or not its binding is pressed.
/// </summary>
public partial class HoldActivationDebugDisplay : CompositeDrawable
{
    private readonly GarbusPlayfield playfield;
    private readonly Box progressFill;
    private readonly SpriteText label;

    /// <summary>The hold currently represented by the display, or <c>null</c> while hidden.</summary>
    public DrawableCardinalHoldNote? ActiveHold { get; private set; }

    /// <summary>The currently displayed fill proportion. Exposed for visual tests.</summary>
    public double DisplayedProgress { get; private set; }

    public HoldActivationDebugDisplay(GarbusPlayfield playfield)
    {
        this.playfield = playfield;

        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        Margin = new MarginPadding(20);
        Size = new Vector2(280, 58);
        AlwaysPresent = true;
        Alpha = 0;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0, 0, 0, 190),
            },
            label = new SpriteText
            {
                Position = new Vector2(10, 7),
                Font = FontUsage.Default.With(size: 16),
                Colour = Color4.Yellow,
            },
            new Container
            {
                RelativeSizeAxes = Axes.X,
                Position = new Vector2(10, 34),
                Width = 1,
                Height = 14,
                Padding = new MarginPadding { Right = 20 },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(55, 55, 65, 255),
                    },
                    progressFill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Width = 0,
                        Colour = Color4.Yellow,
                    },
                },
            },
        };
    }

    protected override void Update()
    {
        base.Update();

        ActiveHold = playfield.AllHitObjects
                              .OfType<DrawableCardinalHoldNote>()
                              .Where(hold => hold.IsAlive
                                             && Time.Current >= hold.HitObject.StartTime
                                             && Time.Current <= hold.HitObject.EndTime)
                              .OrderBy(hold => hold.HitObject.EndTime)
                              .FirstOrDefault();

        if (ActiveHold == null)
        {
            Alpha = 0;
            DisplayedProgress = 0;
            progressFill.Width = 0;
            return;
        }

        Alpha = 1;
        DisplayedProgress = ActiveHold.ActivationProgress;
        progressFill.Width = (float)DisplayedProgress;
        label.Text = $"HOLD {ActiveHold.HitObject.Direction}: {DisplayedProgress:P1}";
    }
}
