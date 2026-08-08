// Two-square-plus-arc head (as DrawableShoulderNote) plus a transparent CircularProgress
// sector body; judgement/input come from DrawableHoldNote<,>.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Utils;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.UI;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A held shoulder note: the two-square-plus-arc shoulder head plus a transparent sector body that grows
/// outward from the tail radius to the head radius over the 90° quadrant slice.
/// </summary>
public partial class DrawableShoulderHoldNote : DrawableHoldNote<ShoulderHoldNote, HoldNoteHead<ShoulderHoldNote>>
{
    private const float square_size = 80f;
    private const float arc_thickness = 15f;
    private const float sector_alpha = 0.35f;

    private static readonly Colour4 held_colour = Colour4.Purple;
    private static readonly Colour4 dropped_colour = Colour4.Gray;

    private Sprite squareA = null!;
    private Sprite squareB = null!;

    // Two half-arcs so the head arc can grow out of both squares toward the middle during the spawn, as
    // the tap ShoulderNote does; a single contiguous arc cannot leave a gap in its centre while growing.
    private Arc arcA = null!;
    private Arc arcB = null!;
    private CircularProgress sector = null!;

    // Head-hit pop factor, composed with the spawn grow (below) so the two never clobber each other on the
    // squares' shared Scale: UpdateVisuals writes Scale = spawnGrow · headPop every frame, and OnHeadHit
    // animates this bindable rather than the sprites directly.
    private readonly BindableFloat headPopScale = new BindableFloat(1f);

    /// <summary>Current scale factor applied to each spawning head square (0 → 1 across the spawn hold). Test seam.</summary>
    public float SpawnSquareScale => squareA.Scale.X;

    /// <summary>Combined angular coverage (degrees) of the two head half-arcs — 0 collapsed, 90 once they meet. Test seam.</summary>
    public float SpawnArcSpanDeg => spanDeg(arcA) + spanDeg(arcB);

    private static float spanDeg(Arc arc) => MathF.Abs(arc.EndRadians.Value - arc.StartRadians.Value) * 180f / MathF.PI;

    public DrawableShoulderHoldNote(ShoulderHoldNote hitObject)
        : base(hitObject)
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        var squareTexture = textures.Get("square");

        // Transparent body behind the head. CircularProgress fills a 90° wedge (Progress 0.25); Size/InnerRadius
        // are set each frame to grow the annulus between tail and head radii.
        AddInternal(sector = new CircularProgress
        {
            RelativeSizeAxes = Axes.None,
            Size = Vector2.Zero,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Progress = 0.25,
            InnerRadius = 0f,
            Colour = held_colour,
            Alpha = sector_alpha,
        });

        AddInternal(arcA = createArc());
        AddInternal(arcB = createArc());

        AddInternal(squareA = createSquare(squareTexture));
        AddInternal(squareB = createSquare(squareTexture));
    }

    private static Arc createArc() => new Arc(thickness: arc_thickness)
    {
        RelativeSizeAxes = Axes.None,
        Size = Vector2.Zero,
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        Colour = held_colour,
    };

    private static Sprite createSquare(Texture texture) => new Sprite
    {
        Texture = texture,
        Size = new Vector2(square_size),
        FillMode = FillMode.Fit,
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        Colour = held_colour,
    };

    protected override void UpdateVisuals()
    {
        float baseAngleDeg = HitObject.AngleDeg;
        float ring = ScrollingContainer.ScrollLength;
        float outer = Math.Clamp(ScrollingContainer.DistanceFromCentreAtTime(HitObject.StartTime), 0f, ring);
        float inner = Math.Clamp(ScrollingContainer.DistanceFromCentreAtTime(HitObject.EndTime), 0f, ring);

        // Spawn progress across the motionless halo hold, driving the head's in-place grow. LifetimeStart is
        // the halo-spawn time (StartTime − LeadTime); computing per-frame replays correctly on rewind/scrub.
        // Easing.In matches DrawableCardinalNote / the tap ShoulderNote so the head grows identically.
        float eased = SpawnAnimationDuration > 0
            ? (float)Interpolation.ApplyEasing(Easing.In, Math.Clamp((Time.Current - LifetimeStart) / SpawnAnimationDuration, 0d, 1d))
            : 1f;

        // Head: two squares on the ±45° diagonals + the growing arc, at the head radius. The squares grow in
        // place about their own centres; each half-arc grows out of its square toward base, meeting there as
        // the squares reach full scale.
        squareA.Scale = squareB.Scale = new Vector2(eased * headPopScale.Value);
        squareA.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, outer, +1f);
        squareB.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, outer, -1f);

        updateHalfArc(arcA, baseAngleDeg, +1f, outer, eased);
        updateHalfArc(arcB, baseAngleDeg, -1f, outer, eased);

        // Body: the transparent sector fills the annulus [inner, outer] over the 90° slice.
        // CircularProgress.InnerRadius is the fill *thickness* measured inward from the outer edge
        // (0 = invisible, 1 = filled to the centre) — NOT the hole radius. The unfilled hole then has
        // radius (1 - InnerRadius)·outer, which we want to equal the tail distance `inner`; hence the
        // fill fraction is 1 - inner/outer. Using inner/outer directly inverts it, collapsing the sector
        // to a zero-thickness stub while both ends are still held together on the halo during a full hold
        // (inner == outer ⇒ InnerRadius 0).
        sector.Size = new Vector2(2f * outer);
        sector.Rotation = ShoulderNoteGeometry.SectorRotationDeg(baseAngleDeg);
        sector.InnerRadius = outer > 0f ? 1f - inner / outer : 0f;

        if (!Judged)
        {
            var trailColour = HoldActive && !Holding ? dropped_colour : held_colour;
            arcA.Colour = trailColour;
            arcB.Colour = trailColour;
            sector.Colour = trailColour;
        }
    }

    private static void updateHalfArc(Arc arc, float baseAngleDeg, float offsetSign, float radius, float eased)
    {
        arc.Size = new Vector2(2f * radius);
        arc.StartRadians.Value = ShoulderNoteGeometry.ToRadians(baseAngleDeg + offsetSign * ShoulderNoteGeometry.DiagonalOffsetDeg);
        arc.EndRadians.Value = ShoulderNoteGeometry.ToRadians(ShoulderNoteGeometry.SpawnArcInnerAngleDeg(baseAngleDeg, offsetSign, eased));
    }

    protected override void OnHeadHit()
    {
        // Pop the squares via the shared factor (see headPopScale) so UpdateVisuals' per-frame spawn grow
        // doesn't overwrite it. eased is already 1 by the time the head is hit, so this reads as a clean pop.
        this.TransformBindableTo(headPopScale, 1.2f, 80, Easing.OutQuint)
            .Then().TransformBindableTo(headPopScale, 1f, 120, Easing.OutQuint);
    }

    protected override void PrepareForUse()
    {
        base.PrepareForUse();
        headPopScale.Value = 1f;
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
