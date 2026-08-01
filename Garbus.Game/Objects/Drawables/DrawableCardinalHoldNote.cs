// Judgement/input live in DrawableHoldNote<,>; this holds only the cardinal visuals.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects; // ChordColours, ChordHighlighter
using Garbus.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A held cardinal note: a square head sprite trailing a straight radial line (the hold body). The head
/// emerges from the centre and reaches the ring at StartTime; the trailing line runs inward toward the tail.
/// </summary>
public partial class DrawableCardinalHoldNote : DrawableHoldNote<CardinalHoldNote, HoldNoteHead<CardinalHoldNote>>
{
    private const float body_thickness = 20f;
    private const float head_size = 80f;

    private static readonly Colour4 held_colour = Colour4.White;
    private static readonly Colour4 dropped_colour = Colour4.Gray;

    private readonly PersistentSprite headSprite;
    private readonly PersistentSmoothPath body;

    [Resolved]
    private ChordHighlighter chords { get; set; } = null!;

    public DrawableCardinalHoldNote(CardinalHoldNote hitObject)
        : base(hitObject)
    {
        body = new PersistentSmoothPath
        {
            Anchor = Anchor.Centre,
            PathRadius = body_thickness / 2,
            Colour = held_colour,
        };

        headSprite = new PersistentSprite
        {
            Size = new Vector2(head_size),
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        };
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        headSprite.Texture = textures.Get("square");

        AddInternal(body);
        AddInternal(headSprite);
    }

    protected override void PrepareForUse()
    {
        base.PrepareForUse();

        Colour = chords.IsInChord(HitObject) ? ChordColours.Highlight : Colour4.White;
    }

    protected override void Update()
    {
        base.Update();

        // Under autoHit (the editor Mini preview) chord membership can change live while spawned;
        // PrepareForUse fires only once. Recompute the tint each frame (matches EditorDrawableCardinalNote).
        // No cost in normal gameplay (AutoHitActive is false; PrepareForUse suffices for the static chart).
        if (AutoHitActive)
            Colour = chords.IsInChord(HitObject) ? ChordColours.Highlight : Colour4.White;
    }

    protected override void UpdateInitialTransforms()
    {
        base.UpdateInitialTransforms();

        headSprite.ScaleTo(0).ScaleTo(1, SpawnAnimationDuration, Easing.In);
        body.FadeInFromZero(SpawnAnimationDuration, Easing.In);
    }

    protected override void OnHeadHit()
    {
        headSprite.ScaleTo(1.2f, 80, Easing.OutQuint).Then().ScaleTo(1f, 120, Easing.OutQuint);
    }

    protected override void UpdateVisuals()
    {
        float ring = ScrollingContainer.ScrollLength;
        float radians = MathUtils.DegToRad(HitObject.AngleDeg);

        float headProgress = ScrollingContainer.ProgressAtTime(HitObject.StartTime);
        headSprite.Position = polarToCartesian(radians, headProgress);

        float outer = Math.Clamp(ScrollingContainer.DistanceFromCentreAtTime(HitObject.StartTime), 0f, ring);
        float inner = Math.Clamp(ScrollingContainer.DistanceFromCentreAtTime(HitObject.EndTime), 0f, ring);

        if (outer - inner > 1f)
        {
            body.Vertices = new[]
            {
                polarToCartesian(radians, inner),
                polarToCartesian(radians, outer),
            };

            body.Position = -body.PositionInBoundingBox(Vector2.Zero);
        }
        else
        {
            body.Vertices = Array.Empty<Vector2>();
        }

        if (!Judged)
            body.Colour = HoldActive && !Holding ? dropped_colour : held_colour;
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        const double duration = 1000;

        switch (state)
        {
            case ArmedState.Hit:
                body.FadeOut(350, Easing.OutQuint);
                headSprite.Spin(700, RotationDirection.Clockwise)
                          .FadeOut(350, Easing.OutQuint)
                          .ScaleTo(new Vector2(2), 350, Easing.OutQuint)
                          .OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                body.FadeColour(Color4.Red, duration);
                body.FadeOut(duration, Easing.InQuint);
                headSprite.FadeColour(Color4.Red, duration);
                headSprite.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
        }
    }

    private static Vector2 polarToCartesian(float radians, float radius)
        => new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);
}
