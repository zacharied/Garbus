// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/SliderPlacementBlueprint.cs).
// BacPath/BacPathControlPoint → GarbusPath/GarbusPathControlPoint; HorizontalDirection from
// Garbus.Game.Core; OsuColour resolve dropped — the preview colour (osu's colours.Yellow) is inlined as
// Colour4.Yellow; EditorDrawableCardinalNote from Edit.Drawables; SnapResult/GarbusSnapResult from Edit;
// Composer/HitObject via GarbusPlacementBlueprint. Logic identical.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Input.Events;
using Garbus.Game.Core;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints;

/// <summary>
/// Multi-click slider placement: the first left click sets the body (start time + angle), each further
/// left click appends a control-point node at the snapped cursor (which must be later in time than the
/// previous node), and a right click commits — requiring at least one node, per the format's contract.
/// A rubber-band segment previews the next node at the cursor.
/// </summary>
internal partial class SliderPlacementBlueprint : GarbusPlacementBlueprint<SliderBody>
{
    private readonly Container previewPaths;
    private readonly EditSquarePiece cursorPiece;

    // Paths are buffered drawables (each owns a framebuffer sized to its bounds), so they are pooled and
    // reused rather than recreated per frame — newing them up every Update allocated a fresh framebuffer
    // each frame, which ran memory into the tens of GB once a wide seam-crossing path was involved.
    private readonly List<SmoothPath> previewPool = new List<SmoothPath>();

    private int cursorAngleDeg;
    private double cursorTime;

    protected override bool IsValidForPlacement =>
        base.IsValidForPlacement && HitObject.Path.ControlPoints.Count > 0 && HitObject.Duration > 0;

    public SliderPlacementBlueprint()
        : base(new SliderBody
        {
            AngleDeg = 0,
            Side = HorizontalDirection.Left,
            Path = new GarbusPath { ControlPoints = new BindableList<GarbusPathControlPoint>() },
        })
    {
        InternalChildren = new Drawable[]
        {
            // masked to the timeline bounds so preview lines don't spill outside it (they still show in
            // the ghost bands, which lie within the bounds).
            previewPaths = new Container { RelativeSizeAxes = Axes.Both, Masking = true, Colour = Colour4.Yellow },
            cursorPiece = new EditSquarePiece
            {
                Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE),
                Origin = Anchor.Centre,
            },
        };
    }

    public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
    {
        var result = base.UpdateTimeAndPosition(screenSpacePosition, fallbackTime);

        if (result is GarbusSnapResult garbus)
            cursorAngleDeg = garbus.AngleDeg;
        if (result.Time is double time)
            cursorTime = time;

        cursorPiece.Position = ToLocalSpace(result.ScreenSpacePosition);

        return result;
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        switch (e.Button)
        {
            case MouseButton.Left:
                if (PlacementActive == PlacementState.Waiting)
                    BeginPlacement(true);
                else
                    tryAddNode();
                return true;

            case MouseButton.Right:
                if (PlacementActive == PlacementState.Active)
                    EndPlacement(HitObject.Path.ControlPoints.Count > 0);
                return true;
        }

        return false;
    }

    private void tryAddNode()
    {
        var controlPoints = HitObject.Path.ControlPoints;

        double timeOffset = cursorTime - HitObject.StartTime;
        var previous = controlPoints.Count > 0 ? controlPoints[^1] : null;

        // Reject unless the prospective path stays ordered: non-decreasing, with at most one
        // zero-length link in a row (a single horizontal arc). The duration > 0 half is deferred to
        // IsValidForPlacement so a leading zero-arc can still be built up node by node.
        var prospective = new List<double>(controlPoints.Count + 1);
        foreach (var cp in controlPoints)
            prospective.Add(cp.TimeOffset);
        prospective.Add(timeOffset);

        if (!GarbusSliderPath.AreTimesOrdered(prospective))
            return;

        int previousRotation = previous?.RotationOffset ?? 0;
        int previousAbsolute = EditorAngleMapping.NormalizeDeg(HitObject.AngleDeg + previousRotation);

        controlPoints.Add(new GarbusPathControlPoint
        {
            TimeOffset = timeOffset,
            RotationOffset = previousRotation + EditorAngleMapping.MinimalDiff(previousAbsolute, cursorAngleDeg),
        });

        ApplyDefaultsToHitObject();
    }

    protected override void Update()
    {
        base.Update();

        if (Composer == null)
            return;

        var container = Composer.Playfield.HitObjectContainer;
        float pxPerDeg = DrawWidth / EditorAngleMapping.TOTAL_DEGREES;

        var vertices = new List<Vector2>();
        int minOffset = 0, maxOffset = 0;

        if (PlacementActive == PlacementState.Active)
        {
            float headX = EditorAngleMapping.ToX(HitObject.AngleDeg) * DrawWidth;

            vertices.Add(new Vector2(headX, ToLocalSpace(container.ScreenSpacePositionAtTime(HitObject.StartTime)).Y));

            int lastRotation = 0;

            foreach (var cp in HitObject.Path.ControlPoints)
            {
                vertices.Add(new Vector2(
                    headX + EditorAngleMapping.GridOffset(cp.RotationOffset) * pxPerDeg,
                    ToLocalSpace(container.ScreenSpacePositionAtTime(HitObject.StartTime + cp.TimeOffset)).Y));

                minOffset = Math.Min(minOffset, cp.RotationOffset);
                maxOffset = Math.Max(maxOffset, cp.RotationOffset);
                lastRotation = cp.RotationOffset;
            }

            // rubber-band to the cursor when it would form a valid next node — at the UNWRAPPED
            // continuation the commit would produce (MinimalDiff from the last node), not the raw cursor
            // x, so previewing across the wrap seam goes the short way; a wrap copy lands it on the cursor.
            if (cursorTime - HitObject.StartTime >= (HitObject.Path.ControlPoints.Count > 0 ? HitObject.Path.ControlPoints[^1].TimeOffset : 0))
            {
                int lastAbsolute = EditorAngleMapping.NormalizeDeg(HitObject.AngleDeg + lastRotation);
                int rubberOffset = lastRotation + EditorAngleMapping.MinimalDiff(lastAbsolute, cursorAngleDeg);

                vertices.Add(new Vector2(headX + EditorAngleMapping.GridOffset(rubberOffset) * pxPerDeg, cursorPiece.Position.Y));

                minOffset = Math.Min(minOffset, rubberOffset);
                maxOffset = Math.Max(maxOffset, rubberOffset);
            }
        }

        int used = 0;

        if (vertices.Count >= 2)
        {
            float headGridDeg = EditorAngleMapping.ToGridDegrees(HitObject.AngleDeg);

            foreach (int k in EditorAngleMapping.VisibleWrapCopiesForOffsets(headGridDeg, minOffset, maxOffset))
            {
                var path = poolPath(used++);
                path.Vertices = vertices;
                // undo the auto-size bounding-box offset (so vertices land in local space), then shift by the wrap copy.
                path.Position = -path.PositionInBoundingBox(Vector2.Zero) + new Vector2(-k * 360 * pxPerDeg, 0);
            }
        }

        // clear (but keep) any pooled paths not needed this frame — an empty path draws nothing.
        for (int i = used; i < previewPool.Count; i++)
            previewPool[i].ClearVertices();
    }

    private SmoothPath poolPath(int index)
    {
        while (previewPool.Count <= index)
        {
            var path = new SmoothPath { PathRadius = 3 };
            previewPool.Add(path);
            previewPaths.Add(path);
        }

        return previewPool[index];
    }
}
