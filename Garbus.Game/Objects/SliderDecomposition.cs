using System;
using System.Collections.Generic;
using osu.Framework.Bindables;

namespace Garbus.Game.Objects;

/// <summary>
/// Splits a slider into a run of head-only sliders (each an empty-path <see cref="SliderBody"/>) by sampling
/// its swept angle at a fixed time step. The first head sits on the slider's own head; further heads step by
/// <paramref name="step"/> until the slider's end. The end is only represented if it lands on a step
/// (grid-steps-only sampling) — an off-grid end gets no head.
/// </summary>
public static class SliderDecomposition
{
    public static List<SliderBody> DecomposeIntoHeads(SliderBody slider, double step)
    {
        var heads = new List<SliderBody> { makeHead(slider, slider.StartTime) };

        // A non-positive step can't advance — the head alone is the whole decomposition (guards a hang).
        if (step <= 0)
            return heads;

        double endTime = slider.EndTime;

        // Include a step that lands on the end despite float drift (e.g. 3 * (500/3) ≠ 500 exactly); the
        // tolerance scales with magnitude and stays far below any musical step, so it never adds an extra one.
        double tolerance = Math.Abs(endTime) * 1e-9 + 1e-9;

        // i * step (rather than accumulating += step) keeps every sample time drift-free.
        for (int i = 1; ; i++)
        {
            double time = slider.StartTime + i * step;
            if (time > endTime + tolerance)
                break;

            heads.Add(makeHead(slider, time));
        }

        return heads;
    }

    private static SliderBody makeHead(SliderBody slider, double time) => new SliderBody
    {
        StartTime = time,
        AngleDeg = (int)Math.Round(slider.AngleDegAt(time), MidpointRounding.AwayFromZero),
        Side = slider.Side,
        Path = new GarbusPath { ControlPoints = new BindableList<GarbusPathControlPoint>() },
    };
}
