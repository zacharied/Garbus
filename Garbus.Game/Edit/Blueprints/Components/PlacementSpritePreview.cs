using osu.Framework.Graphics;
using Garbus.Game.Core;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>
/// Sprite-backed preview for a hit object being placed. It mirrors the committed editor sprite's
/// colour and orientation while the placement blueprint supplies its timeline position.
/// </summary>
internal partial class PlacementSpritePreview : EditorSpritePiece
{
    private readonly GarbusHitObject hitObject;

    public PlacementSpritePreview(GarbusHitObject hitObject)
        : base(hitObject is GarbusSlamCentered or GarbusSlamEdge ? "arrow" : "square")
    {
        this.hitObject = hitObject;

        RelativeSizeAxes = Axes.None;
        Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE);
        Origin = Anchor.Centre;
        Alpha = 0.75f;
    }

    protected override void Update()
    {
        base.Update();

        switch (hitObject)
        {
            case GarbusSlamCentered slam:
                Colour = slam.Side.ToColour();
                Rotation = 180;
                break;

            case GarbusSlamEdge slam:
                Colour = slam.Side.ToColour();
                Rotation = (slam.Direction == RotationalDirection.Clockwise ? -90 : 90) * EditorAngleMapping.Direction;
                break;

            case ShoulderNote:
            case ShoulderHoldNote:
                Colour = Color4.MediumPurple;
                Rotation = 0;
                break;

            default:
                Colour = Colour4.White;
                Rotation = 0;
                break;
        }
    }
}
