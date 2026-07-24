// Task 9 support: thin subclasses that keep completed transforms instead of pruning them.
//
// osu-framework's Transformable declares `RemoveCompletedTransforms` as `{ get; protected set; }`
// (mirrored by CompositeDrawable for containers), so an unrelated caller constructing a plain
// Sprite/Container/SmoothPath as a child (e.g. from inside a DrawableHitObject) cannot flip the
// flag directly — protected access requires the assignment to happen inside that type's own class
// hierarchy. Garbus.Game.Gameplay.Objects.Pooling.PoolableDrawableWithLifetime (the DrawableHitObject
// base) uses exactly this pattern to force `RemoveCompletedTransforms => false` for the hit object
// itself; these do the same for its plain child drawables so that spawn-intro transforms applied in
// UpdateInitialTransforms (absolute-sequenced, so they must be able to replay) survive being marked
// "completed" on rewind/restart instead of being discarded.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Sprites;

namespace Garbus.Game.Objects.Drawables;

public partial class PersistentSprite : Sprite
{
    public override bool RemoveCompletedTransforms => false;
}

public partial class PersistentSmoothPath : SmoothPath
{
    public override bool RemoveCompletedTransforms => false;
}

public partial class PersistentContainer : Container
{
    public override bool RemoveCompletedTransforms => false;
}

public partial class PersistentContainer<T> : Container<T>
    where T : Drawable
{
    public override bool RemoveCompletedTransforms => false;
}
