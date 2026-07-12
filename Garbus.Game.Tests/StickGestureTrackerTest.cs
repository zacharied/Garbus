// Unit tests for the slam gesture machine. Plain NUnit — no game host. Positions are built from the
// SliderCatcher angle convention (angle = atan2(-y, x)): a point at `angleDeg` and radius r is
// (r*cos, -r*sin), so +x is 0deg and increasing angle sweeps anticlockwise.

using System;
using System.Numerics;
using Garbus.Game.Core;
using Garbus.Game.Input;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class StickGestureTrackerTest
    {
        private static Vector2 At(float angleDeg, float radius)
        {
            float rad = angleDeg * MathF.PI / 180f;
            return new Vector2(radius * MathF.Cos(rad), -radius * MathF.Sin(rad));
        }

        [Test]
        public void TestFlickTowardsAngleDetected()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(0, 0f));      // centred
            t.AddSample(10, At(0, 0.8f));   // crossed threshold outward at 0deg

            Assert.That(t.FlickedTowards(0, sinceTime: -1000), Is.True);
        }

        [Test]
        public void TestFlickOffAngleRejected()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(0, 0f));
            t.AddSample(10, At(0, 0.8f));   // flick at 0deg

            Assert.That(t.FlickedTowards(90, sinceTime: -1000), Is.False);
        }

        [Test]
        public void TestSlowDriftNeverCrossesThreshold()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(0, 0.1f));
            t.AddSample(10, At(0, 0.3f));
            t.AddSample(20, At(0, 0.5f));   // never reaches 0.7

            Assert.That(t.FlickedTowards(0, sinceTime: -1000), Is.False);
        }

        [Test]
        public void TestFlickBeforeSinceTimeRejected()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(0, 0f));
            t.AddSample(10, At(0, 0.8f));   // crossing at t=10

            Assert.That(t.FlickedTowards(0, sinceTime: 100), Is.False);
        }

        [Test]
        public void TestStaleSamplesPruned()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(0, 0f));
            t.AddSample(10, At(0, 0.8f));   // crossing at t=10
            t.AddSample(500, At(0, 0.8f));  // 500 - 350 = 150 > 10, prunes the crossing pair

            Assert.That(t.FlickedTowards(0, sinceTime: -1000), Is.False);
        }

        [Test]
        public void TestSweepThroughAngleInMatchingDirection()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(-20, 0.8f));   // at edge, -20deg
            t.AddSample(10, At(20, 0.8f));   // at edge, +20deg -> swept anticlockwise through 0deg

            Assert.That(t.SweptThrough(0, RotationalDirection.Anticlockwise, sinceTime: -1000), Is.True);
        }

        [Test]
        public void TestSweepWrongDirectionRejected()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(-20, 0.8f));
            t.AddSample(10, At(20, 0.8f));   // anticlockwise sweep

            Assert.That(t.SweptThrough(0, RotationalDirection.Clockwise, sinceTime: -1000), Is.False);
        }

        [Test]
        public void TestSweepInsideEdgeRejected()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(-20, 0.3f));   // not at edge
            t.AddSample(10, At(20, 0.3f));

            Assert.That(t.SweptThrough(0, RotationalDirection.Anticlockwise, sinceTime: -1000), Is.False);
        }

        [Test]
        public void TestSweepAcrossSeamHandled()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(170, 0.8f));    // at edge, 170deg
            t.AddSample(10, At(190, 0.8f));   // at edge, 190deg (== -170) -> anticlockwise through 180deg

            Assert.That(t.SweptThrough(180, RotationalDirection.Anticlockwise, sinceTime: -1000), Is.True);
        }
    }
}
