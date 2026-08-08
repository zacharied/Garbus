using System;
using Garbus.Game.Objects.Drawables;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class ShoulderNoteGeometryTest
    {
        // 100 * cos/sin 45°
        private const float diag = 70.71068f;

        [Test]
        public void RightShoulderSquaresStraddleEast()
        {
            var plus = ShoulderNoteGeometry.SquarePosition(0f, 100f, +1f);
            var minus = ShoulderNoteGeometry.SquarePosition(0f, 100f, -1f);

            // +45° points up-right (y negative in screen space); -45° points down-right.
            Assert.That(plus.X, Is.EqualTo(diag).Within(0.01f));
            Assert.That(plus.Y, Is.EqualTo(-diag).Within(0.01f));
            Assert.That(minus.X, Is.EqualTo(diag).Within(0.01f));
            Assert.That(minus.Y, Is.EqualTo(diag).Within(0.01f));
        }

        [Test]
        public void LeftShoulderSquaresStraddleWest()
        {
            var plus = ShoulderNoteGeometry.SquarePosition(180f, 100f, +1f);
            var minus = ShoulderNoteGeometry.SquarePosition(180f, 100f, -1f);

            // 225° -> (-x, +y); 135° -> (-x, -y).
            Assert.That(plus.X, Is.EqualTo(-diag).Within(0.01f));
            Assert.That(plus.Y, Is.EqualTo(diag).Within(0.01f));
            Assert.That(minus.X, Is.EqualTo(-diag).Within(0.01f));
            Assert.That(minus.Y, Is.EqualTo(-diag).Within(0.01f));
        }

        [Test]
        public void VerticalGapGrowsWithRadius()
        {
            float gapNear = gap(50f);
            float gapFar = gap(200f);

            Assert.That(gapFar, Is.GreaterThan(gapNear));
            Assert.That(gapFar, Is.EqualTo(gapNear * 4f).Within(0.01f));

            static float gap(float radius)
            {
                var plus = ShoulderNoteGeometry.SquarePosition(0f, radius, +1f);
                var minus = ShoulderNoteGeometry.SquarePosition(0f, radius, -1f);
                return MathF.Abs(plus.Y - minus.Y);
            }
        }

        [Test]
        public void ZeroRadiusCollapsesToCentre()
        {
            var plus = ShoulderNoteGeometry.SquarePosition(0f, 0f, +1f);
            Assert.That(plus.Length, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void SpawnArcCollapsedAtProgressZero()
        {
            // At spawn start each half-arc has zero span: its inner end sits on its own square's
            // diagonal (base ± 45°), so nothing is drawn.
            Assert.That(ShoulderNoteGeometry.SpawnArcInnerAngleDeg(0f, +1f, 0f), Is.EqualTo(45f).Within(0.001f));
            Assert.That(ShoulderNoteGeometry.SpawnArcInnerAngleDeg(0f, -1f, 0f), Is.EqualTo(-45f).Within(0.001f));
        }

        [Test]
        public void SpawnArcHalvesMeetAtBaseAtProgressOne()
        {
            // At full progress both inner ends land on the base angle, so the two halves touch.
            Assert.That(ShoulderNoteGeometry.SpawnArcInnerAngleDeg(30f, +1f, 1f), Is.EqualTo(30f).Within(0.001f));
            Assert.That(ShoulderNoteGeometry.SpawnArcInnerAngleDeg(30f, -1f, 1f), Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void SpawnArcInnerSweepsInwardPartway()
        {
            // Half-way, the +45° half's inner end sits strictly between its square (base + 45°) and
            // the meeting point (base).
            float inner = ShoulderNoteGeometry.SpawnArcInnerAngleDeg(0f, +1f, 0.5f);
            Assert.That(inner, Is.GreaterThan(0f));
            Assert.That(inner, Is.LessThan(45f));
            Assert.That(inner, Is.EqualTo(22.5f).Within(0.001f));
        }

        [Test]
        public void SectorRotationCentresOnCardinalDirection()
        {
            // CircularProgress fills a 0.25 (90°) wedge clockwise from local up; the rotation places the
            // wedge centre on the side's screen direction (east for a right note, west for a left one).
            Assert.That(ShoulderNoteGeometry.SectorRotationDeg(0f), Is.EqualTo(45f));    // right → east
            Assert.That(ShoulderNoteGeometry.SectorRotationDeg(180f), Is.EqualTo(-135f)); // left → west
        }
    }
}
