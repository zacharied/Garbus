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
    }
}
