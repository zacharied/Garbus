using Garbus.Game.Charts;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual.HitObjects;
using NUnit.Framework;
using osu.Framework.Bindables;

// Namespaced under Profiling (not Visual.HitObjects) so the visual test browser groups it into its own
// "Profiling" folder — the browser keys the group name off the scene's namespace.
namespace Garbus.Game.Tests.Profiling
{
    // Profiling scene: a live, heavily-loaded graphical playfield (the real PlayScreen, via
    // HitObjectStreamTestScene) streaming head-only sliders — SliderBody with an empty path, so just a
    // SliderHead and no children — at ~100 objects/second to stress spawn/lifetime/rendering.
    //
    // The total object count is deliberately bounded: PlayScreen builds one NON-POOLED DrawableSliderBody
    // (each with a framebuffer glow layer) per hit object up front, so filling the whole 120 s virtual
    // track (~11.6k objects) froze the scene for ~1.5 minutes on load before anything drew. What actually
    // drives the render load being profiled is the CONCURRENT object count — spawn rate × on-screen travel
    // time — which is unaffected by the total, so a short stream at the same rate is just as heavy while
    // coming up as a live playfield in a couple of seconds.
    //
    // [Explicit] so it's skipped in headless/CI runs but still selectable in the visual test browser,
    // which is where you actually profile it.
    [TestFixture]
    [Explicit("profiling scene — heavy object count, run manually from the visual test browser")]
    public partial class TestSceneHeadOnlySliderStream : HitObjectStreamTestScene
    {
        private const double first_object = 1000;

        // ~100 objects per second → one every 10 ms.
        private const double spacing = 10;

        // Seconds of sustained stream. Kept short so the scene loads promptly (PlayScreen constructs a
        // drawable per object up front); steady-state concurrent load is the same as a longer stream.
        private const double stream_seconds = 15;

        private const int count = (int)(stream_seconds * 1000 / spacing);

        // Cycle directions/sides so objects land all around the ring rather than stacking in one lane.
        private static readonly int[] angles = { 0, 45, 90, 135, 180, 225, 270, 315 };

        protected override void PopulateChart(GarbusChart chart)
        {
            for (int i = 0; i < count; i++)
            {
                chart.HitObjects.Add(new SliderBody
                {
                    StartTime = first_object + i * spacing,
                    AngleDeg = angles[i % angles.Length],
                    Side = i % 2 == 0 ? HorizontalDirection.Right : HorizontalDirection.Left,
                    // Zero control points = head-only slider.
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>(),
                    },
                });
            }
        }
    }
}
