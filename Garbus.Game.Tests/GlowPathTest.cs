// Pure-math tests for the GlowPath cross-section profile — the geometric replacement for the
// slider body's blurred GlowEffect. The profile must reproduce what a Gaussian blur of the crisp
// stroke looked like: a saturated core (blur alpha × strength clips at 1) falling off to nothing
// at the outer edge. Plain NUnit — no game host; ColourAt is pure math over the ctor parameters.

using Garbus.Game.Objects.Drawables;
using NUnit.Framework;
using osuTK.Graphics;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class GlowPathTest
    {
        // The gameplay slider body's parameters: 15px-wide line (core radius 7.5), sigma-30 blur,
        // strength 30.
        private const float core_radius = 7.5f;
        private const float sigma = 30f;
        private const float strength = 30f;

        private sealed partial class Probe : GlowPath
        {
            public Probe(float coreRadius, float sigma, float strength, float falloff = 1)
                : base(coreRadius, sigma, strength, falloff)
            {
            }

            // ColourAt: 0 = outermost point of the path, 1 = centre.
            public Color4 At(float position) => ColourAt(position);

            // Alpha at an absolute distance from the centreline, so profiles with different
            // extents (and therefore different position scales) can be compared.
            public float AlphaAtDistance(float distance) => At(1 - distance / PathRadius).A;
        }

        [Test]
        public void ExtendsWellBeyondCore()
        {
            var glow = new Probe(core_radius, sigma, strength);

            // At strength 30 the visible skirt reaches several sigma out; the path must be wide
            // enough to contain it, but not unbounded.
            Assert.That(glow.PathRadius, Is.GreaterThan(core_radius + 2 * sigma));
            Assert.That(glow.PathRadius, Is.LessThanOrEqualTo(core_radius + 5 * sigma));
        }

        [Test]
        public void SaturatedAtCentre()
        {
            var glow = new Probe(core_radius, sigma, strength);

            // Blurred-stroke alpha at the centreline is ~0.197; × strength 30 it clips to 1.
            Assert.That(glow.At(1f).A, Is.EqualTo(1f).Within(1e-3));
        }

        [Test]
        public void TransparentAtOuterEdge()
        {
            var glow = new Probe(core_radius, sigma, strength);

            Assert.That(glow.At(0f).A, Is.LessThan(1f / 255));
        }

        [Test]
        public void FallsOffMonotonicallyFromCentre()
        {
            var glow = new Probe(core_radius, sigma, strength);

            float previous = glow.At(1f).A;

            for (int i = 99; i >= 0; i--)
            {
                float alpha = glow.At(i / 100f).A;
                Assert.That(alpha, Is.LessThanOrEqualTo(previous + 1e-4f), $"alpha rose moving outward at position {i / 100f}");
                previous = alpha;
            }
        }

        [Test]
        public void ProfileIsWhite()
        {
            var glow = new Probe(core_radius, sigma, strength);

            // Tinting comes from the drawable colour hierarchy (side colour), not the texture.
            var colour = glow.At(0.5f);
            Assert.That(colour.R, Is.EqualTo(1f));
            Assert.That(colour.G, Is.EqualTo(1f));
            Assert.That(colour.B, Is.EqualTo(1f));
        }

        [Test]
        public void FalloffExponentShapesTail()
        {
            // Unclipped tails (low strength) sampled one sigma into the skirt: a sub-1 exponent
            // lifts the tail (gentler falloff), an above-1 exponent suppresses it (tighter).
            var standard = new Probe(core_radius, sigma, 2f);
            var soft = new Probe(core_radius, sigma, 2f, 0.5f);
            var hard = new Probe(core_radius, sigma, 2f, 2f);

            float d = core_radius + sigma;

            Assert.That(soft.AlphaAtDistance(d), Is.GreaterThan(standard.AlphaAtDistance(d) + 0.05f));
            Assert.That(hard.AlphaAtDistance(d), Is.LessThan(standard.AlphaAtDistance(d) - 0.05f));
        }

        [Test]
        public void FalloffExponentPreservesMonotonicity()
        {
            var soft = new Probe(core_radius, sigma, strength, 0.5f);

            float previous = soft.At(1f).A;

            for (int i = 99; i >= 0; i--)
            {
                float alpha = soft.At(i / 100f).A;
                Assert.That(alpha, Is.LessThanOrEqualTo(previous + 1e-4f), $"alpha rose moving outward at position {i / 100f}");
                previous = alpha;
            }
        }

        [Test]
        public void StrengthScalesUnsaturatedProfile()
        {
            // At a strength low enough not to clip, peak alpha ≈ strength × 0.197 (the blurred
            // alpha of a 15px stroke under a sigma-30 kernel at the centreline).
            var weak = new Probe(core_radius, sigma, 0.5f);

            Assert.That(weak.At(1f).A, Is.EqualTo(0.5f * 0.1974f).Within(0.01f));
        }
    }
}
