// Pure-geometry tests for the shared editor slider polyline builder used by both the compose-view
// slider drawable (SliderPolylineVisual) and the selection outline (SliderSelectionBlueprint). Plain
// NUnit — no game host. Verifies the polyline is subdivided and eased, and that node dots stay on nodes.

using System.Collections.Generic;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class EditorSliderPolylineTest
    {
        private const float px_per_deg = 2f;
        private const float centre_x = 100f;
        private const float draw_height = 200f;
        private const double duration = 100.0;

        private static (List<Vector2> polyline, List<Vector2> nodes) build(GarbusPathControlPoint cp)
        {
            var polyline = new List<Vector2>();
            var nodes = new List<Vector2>();
            EditorSliderPolyline.Build(new[] { cp }, px_per_deg, centre_x, draw_height, duration, polyline, nodes);
            return (polyline, nodes);
        }

        [Test]
        public void SubdividesEachLinkAndKeepsDotsOnNodes()
        {
            var cp = new GarbusPathControlPoint { TimeOffset = duration, RotationOffset = 90 };
            var (polyline, nodes) = build(cp);

            // Head + SegmentsPerLink sub-points per link; one dot per real node (head + this cp).
            Assert.That(polyline.Count, Is.EqualTo(1 + SliderSweep.SegmentsPerLink));
            Assert.That(nodes.Count, Is.EqualTo(2));

            // Head node at the bottom centre; end node at its (angle -> x, time -> y).
            Assert.That(nodes[0], Is.EqualTo(new Vector2(centre_x, draw_height)));
            Assert.That(nodes[1].X, Is.EqualTo(centre_x + 90 * px_per_deg).Within(1e-3));
            Assert.That(nodes[1].Y, Is.EqualTo(0f).Within(1e-3));

            // Polyline endpoints coincide with the nodes.
            Assert.That(polyline[0], Is.EqualTo(nodes[0]));
            Assert.That(polyline[^1].X, Is.EqualTo(nodes[1].X).Within(1e-3));
            Assert.That(polyline[^1].Y, Is.EqualTo(nodes[1].Y).Within(1e-3));
        }

        [Test]
        public void LinearLinkMidpointIsTheStraightChord()
        {
            var cp = new GarbusPathControlPoint { TimeOffset = duration, RotationOffset = 90, SweepEasing = Easing.None };
            var (polyline, _) = build(cp);

            // Mid sub-vertex (t = 0.5) of a plain link sits on the straight chord: angle 45, time 50.
            Assert.That(polyline[6].X, Is.EqualTo(centre_x + 45 * px_per_deg).Within(1e-3));
            Assert.That(polyline[6].Y, Is.EqualTo(draw_height * 0.5f).Within(1e-3));
        }

        [Test]
        public void EasedLinkMidpointBowsAwayFromTheChord()
        {
            var cp = new GarbusPathControlPoint { TimeOffset = duration, RotationOffset = 90, SweepEasing = Easing.InQuint };
            var (polyline, _) = build(cp);

            // Angle eases (InQuint): x follows 90 * ease(0.5); time stays linear (y unchanged from the chord).
            float easedAngle = (float)(90.0 * Interpolation.ApplyEasing(Easing.InQuint, 0.5));
            Assert.That(polyline[6].X, Is.EqualTo(centre_x + easedAngle * px_per_deg).Within(1e-3));
            Assert.That(polyline[6].Y, Is.EqualTo(draw_height * 0.5f).Within(1e-3));

            // Clearly off the straight-chord midpoint (angle 45).
            Assert.That(polyline[6].X, Is.Not.EqualTo(centre_x + 45 * px_per_deg).Within(1f));
        }

        [Test]
        public void ZeroTimeLinkRendersAsHorizontalSegment()
        {
            // A leading zero-length link: the head (time 0) and a child also at time 0, then a later node so
            // the path has a real duration. The head→child link shares a time, so it must draw as a horizontal
            // segment (all its sub-vertices share y) that sweeps only in angle.
            var polyline = new List<Vector2>();
            var nodes = new List<Vector2>();
            var cps = new[]
            {
                new GarbusPathControlPoint { TimeOffset = 0, RotationOffset = 90 },
                new GarbusPathControlPoint { TimeOffset = duration, RotationOffset = 180 },
            };
            EditorSliderPolyline.Build(cps, px_per_deg, centre_x, draw_height, duration, polyline, nodes);

            // Head and the offset-0 child both sit at the bottom edge (time 0 → y = drawHeight).
            Assert.That(nodes[0].Y, Is.EqualTo(draw_height).Within(1e-3));
            Assert.That(nodes[1].Y, Is.EqualTo(draw_height).Within(1e-3));

            // The first link's sub-vertices (indices 0..SegmentsPerLink) all share that y — a horizontal line.
            for (int i = 0; i <= SliderSweep.SegmentsPerLink; i++)
                Assert.That(polyline[i].Y, Is.EqualTo(draw_height).Within(1e-3));

            // ...but it sweeps in x from the head (centre) toward the 90° column.
            Assert.That(polyline[SliderSweep.SegmentsPerLink].X, Is.EqualTo(centre_x + 90 * px_per_deg).Within(1e-3));
        }

        [Test]
        public void ZeroDurationPinsEveryNodeToTheBottomLine()
        {
            // A slider collapsed entirely to StartTime (duration 0): head + one node at time 0, different
            // angle. No vertical extent, so every vertex sits on the bottom line (y = drawHeight) with no
            // NaN, while still sweeping in x toward the node's angle column.
            var polyline = new List<Vector2>();
            var nodes = new List<Vector2>();
            var cp = new GarbusPathControlPoint { TimeOffset = 0, RotationOffset = 90 };
            EditorSliderPolyline.Build(new[] { cp }, px_per_deg, centre_x, draw_height, 0.0, polyline, nodes);

            Assert.That(nodes.Count, Is.EqualTo(2));
            Assert.That(nodes[0], Is.EqualTo(new Vector2(centre_x, draw_height)));
            Assert.That(nodes[1].X, Is.EqualTo(centre_x + 90 * px_per_deg).Within(1e-3));
            Assert.That(nodes[1].Y, Is.EqualTo(draw_height).Within(1e-3));

            // Every polyline vertex sits on the bottom line — and none is NaN.
            foreach (var v in polyline)
            {
                Assert.That(float.IsNaN(v.Y), Is.False);
                Assert.That(v.Y, Is.EqualTo(draw_height).Within(1e-3));
            }
        }
    }
}
