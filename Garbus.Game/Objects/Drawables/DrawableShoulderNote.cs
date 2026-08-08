// Two square sprites on the ±45° quadrant diagonals joined by a growing circular arc;
// self-positions each frame instead of being point-placed.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
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
///
/// Spawn animation (see docs/superpowers/specs/2026-08-08-shoulder-note-spawn-tween-design.md): rather
/// than scaling the whole drawable about the playfield centre — which would slide the squares out of the
/// centre — the two squares grow in place like a cardinal note, and the joining arc grows out of each
/// square toward the midpoint between them. Both are driven per-frame from the same eased spawn progress
/// so the arc halves meet exactly as the squares reach full scale.
/// </summary>
public partial class DrawableShoulderNote : DrawableNote<ShoulderNote>, ISelfPosition
{
    private const float square_size = 80f;
    private const float arc_thickness = 15f;

    private Sprite squareA = null!;
    private Sprite squareB = null!;

    // Two half-arcs rather than one span so the arc can grow out of both squares toward the middle and
    // meet there; a single contiguous arc cannot leave a gap in its centre while both ends grow inward.
    private Arc arcA = null!;
    private Arc arcB = null!;

    [Resolved]
    private GarbusScrollingHitObjectContainer scrollingContainer { get; set; } = null!;

    /// <summary>Current scale factor applied to each spawning square (0 → 1 across the spawn hold). Test seam.</summary>
    public float SpawnSquareScale => squareA.Scale.X;

    /// <summary>Combined angular coverage (degrees) of the two half-arcs — 0 collapsed, 90 once they meet. Test seam.</summary>
    public float SpawnArcSpanDeg => spanDeg(arcA) + spanDeg(arcB);

    private static float spanDeg(Arc arc) => MathF.Abs(arc.EndRadians.Value - arc.StartRadians.Value) * 180f / MathF.PI;

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
    };

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

        // Clamp travel to [0, ring]: the lower bound is inert (the radius map floors at haloRadius, always
        // >= 0, so travel never actually reaches 0) but is kept as an explicit floor rather than removed,
        // so this reads the same as every other radius clamp in this file. The upper bound does real work,
        // pinning the arc at the ring after StartTime so the squares/arc stay there while the hit/miss fade
        // plays instead of overshooting past it (matches how cardinal notes rest at the ring).
        float radius = Math.Clamp(scrollingContainer.DistanceFromCentreAtTime(HitObject.StartTime), 0f, scrollingContainer.ScrollLength);

        // Spawn progress across the motionless halo hold. LifetimeStart is the halo-spawn time
        // (StartTime − LeadTime); driving this per-frame (rather than via transforms) replays correctly on
        // rewind/scrub. Easing.In matches DrawableCardinalNote so the shoulder squares grow identically.
        float eased = SpawnAnimationDuration > 0
            ? (float)Interpolation.ApplyEasing(Easing.In, Math.Clamp((Time.Current - LifetimeStart) / SpawnAnimationDuration, 0d, 1d))
            : 1f;

        // Squares grow in place about their own centres (like a cardinal note), not out of the playfield centre.
        squareA.Scale = squareB.Scale = new Vector2(eased);
        squareA.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, radius, +1f);
        squareB.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, radius, -1f);

        // Each half-arc is pinned at its square (base ± 45°); its inner end sweeps toward base as the spawn
        // eases in, so the two halves meet on the base angle exactly when the squares reach full scale.
        updateHalfArc(arcA, baseAngleDeg, +1f, radius, eased);
        updateHalfArc(arcB, baseAngleDeg, -1f, radius, eased);
    }

    private static void updateHalfArc(Arc arc, float baseAngleDeg, float offsetSign, float radius, float eased)
    {
        arc.Size = new Vector2(2f * radius);
        arc.StartRadians.Value = ShoulderNoteGeometry.ToRadians(baseAngleDeg + offsetSign * ShoulderNoteGeometry.DiagonalOffsetDeg);
        arc.EndRadians.Value = ShoulderNoteGeometry.ToRadians(ShoulderNoteGeometry.SpawnArcInnerAngleDeg(baseAngleDeg, offsetSign, eased));
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
