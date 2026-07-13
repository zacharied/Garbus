namespace Garbus.Game.Gameplay.Audio
{
    /// <summary>
    /// Identifies a single hitsound by the name to look up in the game's sample store
    /// (e.g. "Gameplay/soft-hitnormal"). Hitsounds are fixed per hit-object type — there is no
    /// chart-author sample/bank/volume configuration. Playback volume is a single fixed constant on
    /// <see cref="HitSoundContainer"/>.
    /// </summary>
    public record GarbusHitSample(string Name);
}
