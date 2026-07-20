using System;
using ManagedBass.Wasapi;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneHeadlessAudio : GarbusTestScene
    {
        [Resolved]
        private AudioManager audio { get; set; } = null!;

        // The framework blocks real BASS output devices under headless NUnit runs, but that guard does
        // NOT cover the experimental WASAPI path — enabling WASAPI opens the real default output device
        // (device -1) and makes the test suite audible. Guarding here pins the fix in GarbusGameBase.
        [Test]
        public void TestExperimentalWasapiNotEnabledUnderNUnit()
        {
            AddAssert("experimental WASAPI disabled", () => audio.UseExperimentalWasapi.Value, () => Is.False);
        }

        [Test]
        public void TestNoRealWasapiOutputStarted()
        {
            AddAssert("BASS WASAPI output not started", isWasapiOutputStarted, () => Is.False);
        }

        // On Linux the basswasapi native library isn't distributed, so BassWasapi.IsStarted throws
        // DllNotFoundException. A WASAPI output that can't load its native library definitionally cannot
        // be started, so treat that as "not started" rather than letting the missing lib fail the test.
        private static bool isWasapiOutputStarted()
        {
            try
            {
                return BassWasapi.IsStarted;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }
    }
}
