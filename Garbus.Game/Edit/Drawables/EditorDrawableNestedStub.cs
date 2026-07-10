// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Drawables/EditorDrawableNestedStub.cs).
// BacHitObject → GarbusHitObject.

using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// Invisible editor representation for nested hit objects (hold heads, slider nodes). Nested objects
/// need a drawable in the tree (see the "nested hit objects" gotcha in CLAUDE.md) but the editor shows
/// nothing for them; they simply auto-judge as time passes.
/// </summary>
public partial class EditorDrawableNestedStub : DrawableHitObject<GarbusHitObject>
{
    public EditorDrawableNestedStub(GarbusHitObject hitObject)
        : base(hitObject)
    {
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset >= 0)
            ApplyMaxResult();
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
    }

    public override void PlaySamples()
    {
        // Same scrub gate as EditorDrawableGarbusHitObject (this stub doesn't derive from it):
        // slider nodes / hold heads must stay silent while seeking with the clock stopped.
        if (Clock.IsRunning)
            base.PlaySamples();
    }
}
