// Two square sprites on the ±45° quadrant diagonals joined by a growing circular arc;
// self-positions each frame instead of being point-placed.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.UI;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A shoulder note — the analog-shoulder counterpart of <see cref="DrawableCardinalNote"/>. It is still
/// judged as a single timed press (see <see cref="DrawableNote{T}"/>), but is drawn as two purple square
/// sprites riding outward along the ±45° diagonals of its side's quadrant (East for a right shoulder,
/// West for a left one), joined by a circular arc whose radius grows with the note's travel distance.
///
/// Implements <see cref="ISelfPosition"/> so the scrolling container skips point-positioning it; the
/// drawable instead fills the playfield and places its children in playfield-centre polar coordinates
/// every frame from <see cref="GarbusScrollingHitObjectContainer.DistanceFromCentreAtTime(double)"/>.
/// </summary>
public partial class DrawableShoulderNote : DrawableNote<ShoulderNote>, ISelfPosition
{
    private const float square_size = 80f;
    private const float arc_thickness = 15f;

    private Sprite squareA = null!;
    private Sprite squareB = null!;
    private Arc arc = null!;

    [Resolved]
    private GarbusScrollingHitObjectContainer scrollingContainer { get; set; } = null!;

    public DrawableShoulderNote(ShoulderNote hitObject)
        : base(hitObject)
    {
        RelativeSizeAxes = Axes.Both;
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
        Colour = Colour4.Purple;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        var squareTexture = textures.Get("square");

        // Arc radius is driven by its own size (Arc draws at min(ChildSize)/2), so size it each frame to
        // grow with the note's travel. Angles are set each frame too; start collapsed (no span, no size).
        AddInternal(arc = new Arc(thickness: arc_thickness)
        {
            RelativeSizeAxes = Axes.None,
            Size = Vector2.Zero,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        });

        AddInternal(squareA = createSquare(squareTexture));
        AddInternal(squareB = createSquare(squareTexture));
    }

    private static Sprite createSquare(Texture texture) => new Sprite
    {
        Texture = texture,
        Size = new Vector2(square_size),
        FillMode = FillMode.Fit,
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
    };

    protected override void Update()
    {
        base.Update();

        float baseAngleDeg = HitObject.AngleDeg;

        // Clamp travel to [0, ring]: below 0 before the arc emerges, and pinned at the ring after StartTime
        // so the squares/arc stay on the ring while the hit/miss fade plays instead of overshooting past it
        // (matches how cardinal notes rest at the ring).
        float radius = Math.Clamp(scrollingContainer.DistanceFromCentreAtTime(HitObject.StartTime), 0f, scrollingContainer.ScrollLength);

        squareA.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, radius, +1f);
        squareB.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, radius, -1f);

        arc.Size = new Vector2(2f * radius);
        arc.StartRadians.Value = ShoulderNoteGeometry.ToRadians(baseAngleDeg - ShoulderNoteGeometry.DiagonalOffsetDeg);
        arc.EndRadians.Value = ShoulderNoteGeometry.ToRadians(baseAngleDeg + ShoulderNoteGeometry.DiagonalOffsetDeg);
    }

    protected override void PrepareForUse()
    {
        // Spawn pop, scaled about the playfield centre (this drawable's centre origin).
        this.ScaleTo(0).ScaleTo(1, 125, Easing.In);
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        const double duration = 1000;

        switch (state)
        {
            case ArmedState.Hit:
                this.FadeOut(350, Easing.OutQuint)
                    .ScaleTo(new Vector2(1.4f), 350, Easing.OutQuint)
                    .OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                this.FadeColour(Color4.Red, duration);
                this.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
        }
    }
}
