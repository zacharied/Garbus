// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/BigAssCircleHitObjectComposer.cs).
// BigAssCircleHitObjectComposer → GarbusHitObjectComposer; the base is Garbus's vendored
// ScrollingHitObjectComposer<GarbusHitObject> (Edit/Compose), which has NO DrawableRuleset — so instead
// of osu's CreateDrawableRuleset the composer implements CreatePlayfield() + CreateDrawableRepresentation()
// directly (matching the drawable factory that lived on DrawableBigAssCircleEditorRuleset). The `: base(ruleset)`
// ctor is dropped (no Ruleset). BAC synced scroll speed to the timeline zoom by resolving
// EditorScreenWithTimeline; here the composer holds the constant TimelineTimeRange default (5000 ms) until
// Task 17's timeline writes it — so the Update() zoom-sync loop is gone. BacSnapResult → GarbusSnapResult;
// BacBlueprintContainer/BacBeatSnapGrid → GarbusBlueprintContainer/GarbusBeatSnapGrid; BacEditorPlayfield →
// GarbusEditorPlayfield; icons in the composition tools became text labels. Public surface (AngleSnap,
// FindSnappedAngleTimeAndPosition, typed Playfield) is preserved verbatim.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Edit.Tools;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit;

[Cached]
public partial class GarbusHitObjectComposer : ScrollingHitObjectComposer<GarbusHitObject>
{
    /// <summary>The x-axis snapping increment, in degrees of absolute angle.</summary>
    public readonly BindableInt AngleSnap = new BindableInt(45);

    /// <summary>
    /// Whether placing a hit object should automatically seek the editor clock to its start time.
    /// Wired from <see cref="Configuration.GarbusSetting.EditorAutoSeekOnPlacement"/> via ComposeTab (Task 17).
    /// Placement blueprints bind their own <c>AutoSeekOnPlacement</c> to this when the composer is
    /// in their DI hierarchy.
    /// </summary>
    [Cached]
    public readonly Bindable<bool> AutoSeekOnPlacement = new Bindable<bool>(true);

    private static readonly int[] angle_snap_options = { 5, 15, 45, 90 };

    private FlipPivotOverlay flipPivotOverlay = null!;

    public GarbusHitObjectComposer()
    {
        // BAC synced this to the timeline zoom (mania-style). The timeline arrives in Task 17; until then
        // hold a constant visible time range.
        TimelineTimeRange.Value = 5000;
    }

    public new GarbusEditorPlayfield Playfield => (GarbusEditorPlayfield)base.Playfield;

    protected override Playfield CreatePlayfield() => new GarbusEditorPlayfield();

    protected override DrawableHitObject? CreateDrawableRepresentation(GarbusHitObject hitObject) => hitObject switch
    {
        SliderBody slider => new EditorDrawableSliderBody(slider),
        HoldNote hold => new EditorDrawableHoldNote(hold),
        CardinalNote note => new EditorDrawableCardinalNote(note),
        ShoulderNote shoulder => new EditorDrawableShoulderNote(shoulder),
        GarbusSlamCentered slam => new EditorDrawableGarbusSlamCentered(slam),
        GarbusSlamEdge slam => new EditorDrawableGarbusSlamEdge(slam),
        _ => null,
    };

    protected override ComposeBlueprintContainer CreateBlueprintContainer() => new GarbusBlueprintContainer(this);

    protected override BeatSnapGrid CreateBeatSnapGrid() => new GarbusBeatSnapGrid();

    protected override IReadOnlyList<CompositionTool> CompositionTools => new CompositionTool[]
    {
        new CardinalNoteCompositionTool(),
        new HoldNoteCompositionTool(),
        new ShoulderNoteCompositionTool(),
        new SlamCenteredCompositionTool(),
        new SlamEdgeCompositionTool(),
        new SliderCompositionTool(),
    };

    [BackgroundDependencyLoader]
    private void load()
    {
        EditorRadioButtonCollection angleSnapButtons;

        LeftToolbox.Add(new EditorToolboxGroup("angle snap")
        {
            Child = angleSnapButtons = new EditorRadioButtonCollection
            {
                RelativeSizeAxes = Axes.X,
                Items = angle_snap_options.Select(v => new RadioButton($"{v}°", () => AngleSnap.Value = v)).ToList(),
            },
        });

        angleSnapButtons.Items[Array.IndexOf(angle_snap_options, AngleSnap.Value)].Select();

        PlayfieldContentContainer.Add(flipPivotOverlay = new FlipPivotOverlay(x => EditorAngleMapping.SnapX(x, AngleSnap.Value)));
    }

    /// <summary>
    /// The Garbus snap: time via the base scrolling snap (beat divisor), x via the angle grid — snapped in
    /// the unwrapped band domain so ghost-band cursors stay put visually, with the wrapped angle
    /// reported on the returned <see cref="GarbusSnapResult"/>.
    /// </summary>
    public SnapResult FindSnappedAngleTimeAndPosition(Vector2 screenSpacePosition)
    {
        var timeSnapped = FindSnappedPositionAndTime(screenSpacePosition);

        if (timeSnapped.Playfield is not GarbusEditorPlayfield playfield)
            return timeSnapped;

        // The base scrolling snap recentres x to the playfield middle (columns don't care about x, we
        // do) — so take the snapped y from it but the angle from the original cursor position.
        var local = playfield.ToLocalSpace(screenSpacePosition);
        (float xFrac, int angleDeg) = EditorAngleMapping.SnapX(local.X / playfield.DrawWidth, AngleSnap.Value);
        local.X = xFrac * playfield.DrawWidth;
        local.Y = playfield.ToLocalSpace(timeSnapped.ScreenSpacePosition).Y;

        return new GarbusSnapResult(playfield.ToScreenSpace(local), timeSnapped.Time, angleDeg, playfield);
    }

    /// <summary>Enters interactive "flip around angle" mode: the overlay picks a pivot; <paramref name="onCommit"/> receives 2·pivot.</summary>
    public void BeginFlipAroundAngle(Action<int> onCommit) => flipPivotOverlay.Begin(onCommit);
}
