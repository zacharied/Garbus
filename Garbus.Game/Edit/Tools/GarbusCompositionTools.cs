// Composition tools use plain text labels rather than icon glyphs (editor visuals don't matter here).

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using Garbus.Game.Edit.Blueprints;
using Garbus.Game.Edit.Compose;

namespace Garbus.Game.Edit.Tools;

public class CardinalHoldNoteCompositionTool : CompositionTool
{
    public CardinalHoldNoteCompositionTool()
        : base("Hold")
    {
    }

    public override Drawable CreateIcon() => new SpriteText { Text = "H" };

    public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new CardinalHoldNotePlacementBlueprint();
}

public class ShoulderNoteCompositionTool : CompositionTool
{
    public ShoulderNoteCompositionTool()
        : base("Shoulder")
    {
    }

    public override Drawable CreateIcon() => new SpriteText { Text = "Sh" };

    public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new ShoulderNotePlacementBlueprint();
}

public class ShoulderHoldNoteCompositionTool : CompositionTool
{
    public ShoulderHoldNoteCompositionTool()
        : base("Shoulder Hold")
    {
    }

    public override Drawable CreateIcon() => new SpriteText { Text = "ShH" };

    public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new ShoulderHoldNotePlacementBlueprint();
}

public class SlamCenteredCompositionTool : CompositionTool
{
    public SlamCenteredCompositionTool()
        : base("Center Slam")
    {
    }

    public override Drawable CreateIcon() => new SpriteText { Text = "Sc" };

    public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new SlamCenteredPlacementBlueprint();
}

public class SlamEdgeCompositionTool : CompositionTool
{
    public SlamEdgeCompositionTool()
        : base("Edge Slam")
    {
    }

    public override Drawable CreateIcon() => new SpriteText { Text = "Se" };

    public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new SlamEdgePlacementBlueprint();
}

public class SliderCompositionTool : CompositionTool
{
    public SliderCompositionTool()
        : base("Slider")
    {
        TooltipText = "Left click places the start and each node; right click commits (needs at least one node).";
    }

    public override Drawable CreateIcon() => new SpriteText { Text = "Sl" };

    public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new SliderPlacementBlueprint();
}
