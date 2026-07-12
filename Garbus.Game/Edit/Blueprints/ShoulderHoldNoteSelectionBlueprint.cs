// Shoulder hold selection: outline over the duration with draggable head/tail handles (as the cardinal hold
// selection), positioned in the side's shoulder lane strip.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Blueprints;

internal partial class ShoulderHoldNoteSelectionBlueprint : GarbusSelectionBlueprint<ShoulderHoldNote>
{
    [Resolved]
    private IEditorChangeHandler? changeHandler { get; set; }

    [Resolved]
    private EditorChart? editorChart { get; set; }

    [Resolved]
    private GarbusHitObjectComposer? composer { get; set; }

    private HoldEndDragPiece head = null!;

    public ShoulderHoldNoteSelectionBlueprint(ShoulderHoldNote hold)
        : base(hold)
    {
        Width = EditorDrawableCardinalNote.NOTE_SIZE;
        Origin = Anchor.BottomCentre;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        InternalChildren = new Drawable[]
        {
            new EditSquarePiece { RelativeSizeAxes = Axes.Both },
            head = new HoldEndDragPiece
            {
                RelativeSizeAxes = Axes.X,
                Height = EditorDrawableCardinalNote.NOTE_SIZE,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.Centre,
                DragStarted = () => changeHandler?.BeginChange(),
                Dragging = pos =>
                {
                    double endTime = HitObject.EndTime;
                    double proposedStartTime = timeAt(pos);

                    if (proposedStartTime >= endTime)
                        return;

                    HitObject.StartTime = proposedStartTime;
                    HitObject.Duration = endTime - proposedStartTime;
                    editorChart?.Update(HitObject);
                },
                DragEnded = () => changeHandler?.EndChange(),
            },
            new HoldEndDragPiece
            {
                RelativeSizeAxes = Axes.X,
                Height = 10,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                DragStarted = () => changeHandler?.BeginChange(),
                Dragging = pos =>
                {
                    double proposedEndTime = timeAt(pos);

                    if (HitObject.StartTime >= proposedEndTime)
                        return;

                    HitObject.Duration = proposedEndTime - HitObject.StartTime;
                    editorChart?.Update(HitObject);
                },
                DragEnded = () => changeHandler?.EndChange(),
            },
        };
    }

    private double timeAt(Vector2 screenSpacePosition) =>
        composer?.FindSnappedAngleTimeAndPosition(screenSpacePosition).Time ?? HitObjectContainer.TimeAtScreenSpacePosition(screenSpacePosition);

    protected override void Update()
    {
        base.Update();

        Height = HitObjectContainer.LengthAtTime(HitObject.StartTime, HitObject.EndTime);
    }

    protected override float ComputeXFraction() => GarbusEditorPlayfield.ShoulderXFraction(HitObject.Side);

    protected override float? TwinXFraction() => null;

    public override Quad SelectionQuad => ScreenSpaceDrawQuad;

    public override Vector2 ScreenSpaceSelectionPoint => head.ScreenSpaceDrawQuad.Centre;
}
