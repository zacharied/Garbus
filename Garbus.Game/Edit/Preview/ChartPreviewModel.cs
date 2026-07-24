using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Preview;

internal sealed class ChartPreviewModel
{
    private readonly Dictionary<long, GarbusHitObject> objects = new();

    public event Action? ResyncRequested;

    public GarbusChart Chart { get; private set; } = new();

    public IReadOnlyDictionary<long, GarbusHitObject> Objects => objects;

    internal long Revision { get; private set; }

    public bool ApplyFullState(ChartPreviewFullState state)
    {
        if (!acceptRevision(state.Revision, out bool requestResync))
            return requestResync ? reject() : false;

        try
        {
            GarbusChart decoded = GarbusChartSerializer.Decode(state.ChartJson);

            if (decoded.HitObjects.Count != state.ObjectIds.Length
                || state.ObjectIds.Distinct().Count() != state.ObjectIds.Length)
                return reject();

            decoded.ApplyDefaults();

            var replacements = new Dictionary<long, GarbusHitObject>();
            for (int i = 0; i < state.ObjectIds.Length; i++)
                replacements.Add(state.ObjectIds[i], decoded.HitObjects[i]);

            Chart = decoded;
            objects.Clear();
            foreach ((long id, GarbusHitObject hitObject) in replacements)
                objects.Add(id, hitObject);
            Revision = state.Revision;
            return true;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or ArgumentException)
        {
            return reject();
        }
    }

    public bool ApplyObjectUpsert(ChartPreviewObjectUpsert state)
    {
        if (!acceptRevision(state.Revision, out bool requestResync))
            return requestResync ? reject() : false;

        try
        {
            GarbusHitObject decoded = GarbusChartSerializer.DecodeHitObject(state.ObjectJson);

            if (!objects.TryGetValue(state.ObjectId, out GarbusHitObject? existing))
            {
                decoded.ApplyDefaults();
                Chart.HitObjects.Add(decoded);
                objects.Add(state.ObjectId, decoded);
            }
            else if (!copyState(decoded, existing))
            {
                decoded.ApplyDefaults();
                int index = Chart.HitObjects.IndexOf(existing);
                if (index < 0)
                    return reject();

                Chart.HitObjects[index] = decoded;
                objects[state.ObjectId] = decoded;
            }

            Revision = state.Revision;
            return true;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or ArgumentException)
        {
            return reject();
        }
    }

    public bool ApplyObjectRemove(ChartPreviewObjectRemove state)
    {
        if (!acceptRevision(state.Revision, out bool requestResync))
            return requestResync ? reject() : false;

        if (!objects.Remove(state.ObjectId, out GarbusHitObject? removed)
            || !Chart.HitObjects.Remove(removed))
            return reject();

        Revision = state.Revision;
        return true;
    }

    public bool ApplyStructuralState(ChartPreviewStructuralState state)
    {
        if (!acceptRevision(state.Revision, out bool requestResync))
            return requestResync ? reject() : false;

        try
        {
            GarbusChart decoded = GarbusChartSerializer.Decode(state.ChartJson);
            decoded.HitObjects.Clear();
            decoded.HitObjects.AddRange(Chart.HitObjects);
            Chart = decoded;
            Revision = state.Revision;
            return true;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or ArgumentException)
        {
            return reject();
        }
    }

    private bool acceptRevision(long revision, out bool requestResync)
    {
        // Full replacements and deltas share one sequence so stale object state cannot amend an authoritative snapshot.
        requestResync = revision < Revision;
        return revision > Revision;
    }

    private bool reject()
    {
        ResyncRequested?.Invoke();
        return false;
    }

    internal bool RequestResync() => reject();

    private static bool copyState(GarbusHitObject source, GarbusHitObject target)
    {
        if (source.GetType() != target.GetType())
            return false;

        target.StartTime = source.StartTime;

        switch (source, target)
        {
            case (CardinalNote s, CardinalNote t):
                t.AngleDeg = s.AngleDeg;
                break;

            case (CardinalHoldNote s, CardinalHoldNote t):
                t.AngleDeg = s.AngleDeg;
                t.Duration = s.Duration;
                break;

            case (ShoulderNote s, ShoulderNote t):
                t.Side = s.Side;
                break;

            case (ShoulderHoldNote s, ShoulderHoldNote t):
                t.Side = s.Side;
                t.Duration = s.Duration;
                break;

            case (GarbusSlamCentered s, GarbusSlamCentered t):
                t.AngleDeg = s.AngleDeg;
                t.Side = s.Side;
                break;

            case (GarbusSlamEdge s, GarbusSlamEdge t):
                t.AngleDeg = s.AngleDeg;
                t.Side = s.Side;
                t.Direction = s.Direction;
                break;

            case (SliderBody s, SliderBody t):
                t.AngleDeg = s.AngleDeg;
                t.Side = s.Side;
                t.Path.ControlPoints.Clear();
                foreach (GarbusPathControlPoint controlPoint in s.Path.ControlPoints)
                {
                    t.Path.ControlPoints.Add(new GarbusPathControlPoint
                    {
                        TimeOffset = controlPoint.TimeOffset,
                        RotationOffset = controlPoint.RotationOffset,
                        Smooth = controlPoint.Smooth,
                        SweepEasing = controlPoint.SweepEasing,
                    });
                }
                break;

            default:
                return false;
        }

        target.ApplyDefaults();
        return true;
    }
}
