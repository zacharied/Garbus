// Tests for EditorAngleMapping, covering ToX, SnapX, and GhostTwinX.

using System;
using System.Linq;
using Garbus.Game.Edit;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestEditorAngleMapping
    {
        [TearDown]
        public void ResetDirection() => EditorAngleMapping.Direction = 1;

        // --- Direction reflection (reversed view: reflect about the E–W axis) ---

        [Test]
        public void TestNormalModeSouthAtCentre()
        {
            EditorAngleMapping.Direction = 1;
            // South (270°) maps to the grid centre; the centre x-fraction is 0.5.
            Assert.That(EditorAngleMapping.ToX(270), Is.EqualTo(0.5f).Within(1e-5f));
            // West is left of centre, East is right of centre.
            Assert.That(EditorAngleMapping.ToX(180), Is.LessThan(0.5f));
            Assert.That(EditorAngleMapping.ToX(0), Is.GreaterThan(0.5f));
        }

        [Test]
        public void TestReversedModeNorthAtCentreWestStillLeft()
        {
            EditorAngleMapping.Direction = -1;
            // North (90°) maps to the grid centre.
            Assert.That(EditorAngleMapping.ToX(90), Is.EqualTo(0.5f).Within(1e-5f));
            // West stays on the left, East stays on the right (reflection about the E–W axis).
            Assert.That(EditorAngleMapping.ToX(180), Is.LessThan(0.5f));
            Assert.That(EditorAngleMapping.ToX(0), Is.GreaterThan(0.5f));
        }

        [Test]
        public void TestGridOffsetFlipsSignWithDirection()
        {
            EditorAngleMapping.Direction = 1;
            Assert.That(EditorAngleMapping.GridOffset(45), Is.EqualTo(45), "normal: +CCW offset maps to +grid (rightward)");

            EditorAngleMapping.Direction = -1;
            Assert.That(EditorAngleMapping.GridOffset(45), Is.EqualTo(-45), "reversed: +CCW offset maps to −grid (leftward)");
        }

        [Test]
        public void TestReversedModeRoundTrip()
        {
            EditorAngleMapping.Direction = -1;
            for (int a = 0; a < 360; a += 15)
            {
                float recovered = EditorAngleMapping.ToAngle(EditorAngleMapping.ToX(a));
                Assert.That(recovered, Is.EqualTo(a).Within(0.01f), $"reversed round-trip failed for {a}");
            }
        }

        // --- GridDegreesToAngle (explicit-direction integer inverse of ToGridDegrees) ---

        [Test]
        public void TestGridDegreesToAngleNormalCentreIsSouth()
        {
            // Grid centre (180 grid-degrees) is South (270°) in the normal (+1) direction.
            Assert.That(EditorAngleMapping.GridDegreesToAngle(180, 1), Is.EqualTo(270));
        }

        [Test]
        public void TestGridDegreesToAngleReversedCentreIsNorth()
        {
            // Grid centre (180 grid-degrees) is North (90°) once reflected (-1 direction).
            Assert.That(EditorAngleMapping.GridDegreesToAngle(180, -1), Is.EqualTo(90));
        }

        [Test]
        public void TestGridDegreesToAngleNormalLeftEdgeIsOrigin()
        {
            Assert.That(EditorAngleMapping.GridDegreesToAngle(0, 1), Is.EqualTo(90));
        }

        [Test]
        public void TestGridDegreesToAngleAgreesWithApplyDirectionReflection()
        {
            // GridDegreesToAngle(gridDeg, direction) must be the very same reflection ToX/ToAngle apply —
            // i.e. it agrees with normalizing (gridDeg + ANGLE_ORIGIN) and then reflecting when reversed.
            for (int gridDeg = 0; gridDeg < 360; gridDeg += 15)
            {
                int forwardAbsolute = EditorAngleMapping.NormalizeDeg(gridDeg + EditorAngleMapping.ANGLE_ORIGIN);

                Assert.That(EditorAngleMapping.GridDegreesToAngle(gridDeg, 1), Is.EqualTo(forwardAbsolute));
                Assert.That(EditorAngleMapping.GridDegreesToAngle(gridDeg, -1), Is.EqualTo(EditorAngleMapping.NormalizeDeg(-forwardAbsolute)));
            }
        }

        // --- VisibleWrapCopies ---

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
        /// The left edge of the main grid sits at ANGLE_ORIGIN (90°), which is at x = GHOST_DEGREES/TOTAL_DEGREES.
        /// </summary>
        [Test]
        public void TestToXAtOriginIsGhostFraction()
        {
            float expected = (float)EditorAngleMapping.GHOST_DEGREES / EditorAngleMapping.TOTAL_DEGREES;
            Assert.That(EditorAngleMapping.ToX(EditorAngleMapping.ANGLE_ORIGIN), Is.EqualTo(expected).Within(1e-5f));
        }

        /// <summary>
        /// 90° is 0 grid-degrees from the origin, so its x is GHOST/TOTAL.
        /// The task brief confirms "ToX(90) == 0" is a domain check for the left edge of the MAIN grid
        /// (not the full playfield) — i.e. ToGridDegrees(90) == 0.
        /// </summary>
        [Test]
        public void TestToGridDegreesAtOriginIsZero()
        {
            Assert.That(EditorAngleMapping.ToGridDegrees(90), Is.EqualTo(0f).Within(1e-5f));
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
            // Input x = ghostFrac * 0.5 = 15/420 → unwrapped angle = 75° → nearest 45° is 90°.
            // SnapX must return that exact snap target: snappedX = (90 - 90 + 30) / 420 = ghostFrac,
            // and angleDeg = 90.
            float ghostFrac = (float)EditorAngleMapping.GHOST_DEGREES / EditorAngleMapping.TOTAL_DEGREES;
            float xInLeftGhost = ghostFrac * 0.5f;
            (float snappedX, int angleDeg) = EditorAngleMapping.SnapX(xInLeftGhost, 45);

            Assert.That(angleDeg, Is.EqualTo(90), "nearest 45° snap from 75° unwrapped should be 90°");
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
            // ANGLE_ORIGIN (90°) sits on the left edge of the grid, within GHOST_DEGREES of it.
            // Its grid-degrees == 0, which is < GHOST_DEGREES, so it has a twin.
            Assert.That(EditorAngleMapping.GhostTwinX(90), Is.Not.Null);
        }

        [Test]
        public void TestGhostTwinXForRightEdgeAngle()
        {
            // The angle 1° before the right seam (ANGLE_ORIGIN - 1 mod 360 = 89°):
            // grid-degrees = NormalizeDeg(89 - 90) = 359, which is > 360 - GHOST_DEGREES, so has a twin.
            float? twinX = EditorAngleMapping.GhostTwinX(89);
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

        // --- ReflectionSum ---

        [Test]
        public void TestReflectionSumSingleAngleIsSelfMirror()
        {
            // A lone handle mirrors about itself: sum = 2θ, so NormalizeDeg(sum − θ) == θ.
            Assert.That(EditorAngleMapping.ReflectionSum(new[] { 90 }), Is.EqualTo(180));
        }

        [Test]
        public void TestReflectionSumSwapsAdjacentPair()
        {
            // Centre of [60,120] is 90; reflecting swaps them.
            int sum = EditorAngleMapping.ReflectionSum(new[] { 60, 120 });
            Assert.That(sum, Is.EqualTo(180));
            Assert.That(EditorAngleMapping.NormalizeDeg(sum - 60), Is.EqualTo(120));
            Assert.That(EditorAngleMapping.NormalizeDeg(sum - 120), Is.EqualTo(60));
        }

        [Test]
        public void TestReflectionSumUsesLargestGapNotNaiveMidpoint()
        {
            // 0 and 90: the empty region is the 270° arc from 90 back to 0, so the covering arc is [0,90],
            // centre 45 — NOT the naive "average = 45" that only coincidentally matches here.
            Assert.That(EditorAngleMapping.ReflectionSum(new[] { 0, 90 }), Is.EqualTo(90));
        }

        [Test]
        public void TestReflectionSumSeamStraddlingPairSwapsLocally()
        {
            // 350 and 10 straddle the 0° seam: covering arc [350,370], centre 0. They swap about 0 — the
            // pivot is NOT the naive average (180) that would fling them across the circle.
            int sum = EditorAngleMapping.ReflectionSum(new[] { 350, 10 });
            Assert.That(sum, Is.EqualTo(0));
            Assert.That(EditorAngleMapping.NormalizeDeg(sum - 350), Is.EqualTo(10));
            Assert.That(EditorAngleMapping.NormalizeDeg(sum - 10), Is.EqualTo(350));
        }

        [Test]
        public void TestReflectionSumEmptyIsZero()
        {
            Assert.That(EditorAngleMapping.ReflectionSum(System.Array.Empty<int>()), Is.EqualTo(0));
        }
    }
}
