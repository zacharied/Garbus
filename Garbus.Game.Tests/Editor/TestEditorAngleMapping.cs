// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle.Tests/EditorAngleMappingTest.cs).
// Extended with additional assertions for ToX, SnapX, and GhostTwinX.

using System;
using System.Linq;
using Garbus.Game.Edit;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestEditorAngleMapping
    {
        // --- VisibleWrapCopies (ported from BAC EditorAngleMappingTest) ---

        [Test]
        public void TestWrapCopiesFullyOnGrid()
        {
            Assert.That(EditorAngleMapping.VisibleWrapCopies(50, 100), Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void TestWrapCopiesPointRange()
        {
            Assert.That(EditorAngleMapping.VisibleWrapCopies(180, 180), Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void TestWrapCopiesCrossingRightEdge()
        {
            // sweeps past 360: the overhang re-enters from the left as copy k = 1.
            Assert.That(EditorAngleMapping.VisibleWrapCopies(350, 380), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void TestWrapCopiesCrossingLeftEdge()
        {
            // negative sweep below 0: the overhang re-enters from the right as copy k = −1.
            Assert.That(EditorAngleMapping.VisibleWrapCopies(-20, 10), Is.EqualTo(new[] { -1, 0 }));
        }

        [Test]
        public void TestWrapCopiesMultiTurn()
        {
            // two-turn sweep starting at the grid's left edge: one copy per turn (k = 1, 2), plus the
            // head itself re-shown in the right ghost band (k = −1), plus the unshifted copy.
            Assert.That(EditorAngleMapping.VisibleWrapCopies(0, 765), Is.EqualTo(new[] { -1, 0, 1, 2 }));
        }

        [Test]
        public void TestWrapCopiesNeverEmpty()
        {
            // any range overlapping the window must at least contain its own copy.
            for (int start = 0; start < 360; start += 15)
                Assert.That(EditorAngleMapping.VisibleWrapCopies(start, start + 10).ToList(), Does.Contain(0), $"range starting at {start}");
        }

        // --- ToX / ToAngle ---

        /// <summary>
        /// The left edge of the main grid sits at ANGLE_ORIGIN (135°), which is at x = GHOST_DEGREES/TOTAL_DEGREES.
        /// </summary>
        [Test]
        public void TestToXAtOriginIsGhostFraction()
        {
            float expected = (float)EditorAngleMapping.GHOST_DEGREES / EditorAngleMapping.TOTAL_DEGREES;
            Assert.That(EditorAngleMapping.ToX(EditorAngleMapping.ANGLE_ORIGIN), Is.EqualTo(expected).Within(1e-5f));
        }

        /// <summary>
        /// 135° is 0 grid-degrees from the origin, so its x is GHOST/TOTAL.
        /// The task brief confirms "ToX(135) == 0" is a domain check for the left edge of the MAIN grid
        /// (not the full playfield) — i.e. ToGridDegrees(135) == 0.
        /// </summary>
        [Test]
        public void TestToGridDegreesAtOriginIsZero()
        {
            Assert.That(EditorAngleMapping.ToGridDegrees(135), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void TestToXRoundTrip()
        {
            // ToAngle(ToX(a)) should recover the original angle (within float precision).
            for (int a = 0; a < 360; a += 15)
            {
                float xFrac = EditorAngleMapping.ToX(a);
                float recovered = EditorAngleMapping.ToAngle(xFrac);
                Assert.That(recovered, Is.EqualTo(a).Within(0.01f), $"angle {a} round-trip failed");
            }
        }

        // --- SnapX ---

        [Test]
        public void TestSnapXRoundsToIncrement()
        {
            // at exactly angle 90°, snapping by 45 should return 90.
            float xFrac = EditorAngleMapping.ToX(90);
            (float snappedX, int angleDeg) = EditorAngleMapping.SnapX(xFrac, 45);
            Assert.That(angleDeg, Is.EqualTo(90));
        }

        [Test]
        public void TestSnapXInLeftGhostBandSnapsToGridOrigin()
        {
            // A cursor half-way into the left ghost band resolves to the nearest 45° grid snap.
            // Input x = ghostFrac * 0.5 = 15/420 → unwrapped angle = 120° → nearest 45° is 135°.
            // SnapX must return that exact snap target: snappedX = (135 - 135 + 30) / 420 = ghostFrac,
            // and angleDeg = 135.
            float ghostFrac = (float)EditorAngleMapping.GHOST_DEGREES / EditorAngleMapping.TOTAL_DEGREES;
            float xInLeftGhost = ghostFrac * 0.5f;
            (float snappedX, int angleDeg) = EditorAngleMapping.SnapX(xInLeftGhost, 45);

            Assert.That(angleDeg, Is.EqualTo(135), "nearest 45° snap from 120° unwrapped should be 135°");
            Assert.That(snappedX, Is.EqualTo(ghostFrac).Within(1e-5f), "snapped x should be exactly ghostFrac (the grid-left edge)");
        }

        [Test]
        public void TestSnapXInRightGhostBandStaysInBand()
        {
            float ghostFrac = (float)EditorAngleMapping.GHOST_DEGREES / EditorAngleMapping.TOTAL_DEGREES;
            float gridEnd = ghostFrac + (float)360 / EditorAngleMapping.TOTAL_DEGREES;
            float xInRightGhost = (gridEnd + 1f) / 2f; // halfway between grid end and full width
            (float snappedX, _) = EditorAngleMapping.SnapX(xInRightGhost, 45);

            Assert.That(snappedX, Is.GreaterThanOrEqualTo(gridEnd - 0.01f));
        }

        // --- MinimalDiff ---

        [Test]
        public void TestMinimalDiffShortestRotation()
        {
            // 350 → 10: going clockwise is 20°; going the other way is 340° — expect +20.
            Assert.That(EditorAngleMapping.MinimalDiff(350, 10), Is.EqualTo(20));
        }

        [Test]
        public void TestMinimalDiffNegative()
        {
            // 10 → 350: shortest is −20 (counter-clockwise).
            Assert.That(EditorAngleMapping.MinimalDiff(10, 350), Is.EqualTo(-20));
        }

        [Test]
        public void TestMinimalDiffZero()
        {
            Assert.That(EditorAngleMapping.MinimalDiff(90, 90), Is.EqualTo(0));
        }

        [Test]
        public void TestMinimalDiffExactlyHalfTurn()
        {
            // 0 → 180: exactly half-turn, should be ≤ 180.
            int diff = EditorAngleMapping.MinimalDiff(0, 180);
            Assert.That(Math.Abs(diff), Is.EqualTo(180));
        }

        // --- GhostTwinX ---

        [Test]
        public void TestGhostTwinXNullForMidGridAngle()
        {
            // An angle well inside the grid (180°) should have no ghost twin.
            Assert.That(EditorAngleMapping.GhostTwinX(180), Is.Null);
        }

        [Test]
        public void TestGhostTwinXNonNullForNearEdgeAngle()
        {
            // ANGLE_ORIGIN (135°) sits on the left edge of the grid, within GHOST_DEGREES of it.
            // Its grid-degrees == 0, which is < GHOST_DEGREES, so it has a twin.
            Assert.That(EditorAngleMapping.GhostTwinX(135), Is.Not.Null);
        }

        [Test]
        public void TestGhostTwinXForRightEdgeAngle()
        {
            // The angle 1° before the right seam (ANGLE_ORIGIN - 1 mod 360 = 134°):
            // grid-degrees = NormalizeDeg(134 - 135) = 359, which is > 360 - GHOST_DEGREES, so has a twin.
            float? twinX = EditorAngleMapping.GhostTwinX(134);
            Assert.That(twinX, Is.Not.Null);
            // The twin x should be inside the left ghost band.
            float ghostFrac = (float)EditorAngleMapping.GHOST_DEGREES / EditorAngleMapping.TOTAL_DEGREES;
            Assert.That(twinX!.Value, Is.LessThanOrEqualTo(ghostFrac));
        }

        // --- NormalizeDeg ---

        [Test]
        public void TestNormalizeDegNegative()
        {
            Assert.That(EditorAngleMapping.NormalizeDeg(-90), Is.EqualTo(270));
        }

        [Test]
        public void TestNormalizeDegOver360()
        {
            Assert.That(EditorAngleMapping.NormalizeDeg(450), Is.EqualTo(90));
        }
    }
}
