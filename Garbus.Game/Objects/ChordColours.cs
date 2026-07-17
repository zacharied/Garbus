// Shared colours for the same-start-time cardinal "chord" highlight (notes + gameplay connector).

using osu.Framework.Graphics;

namespace Garbus.Game.Objects;

public static class ChordColours
{
    /// <summary>The tint applied to every cardinal note that shares its start time with another.</summary>
    public static readonly Colour4 Highlight = Colour4.Yellow;

    /// <summary>The thin, semi-transparent yellow of the gameplay connector.</summary>
    public static readonly Colour4 Connector = new Colour4(1f, 1f, 0f, 0.35f);

    /// <summary>Connector line half-thickness in local px (2px total).</summary>
    public const float ConnectorPathRadius = 1f;
}
