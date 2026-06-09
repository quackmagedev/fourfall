namespace Fourfall.Core;

/// <summary>Aggregated outcome of a single token drop, including all cascades.</summary>
public sealed class TurnResult
{
    /// <summary>True when the drop was illegal (full column with no rule-breaker effect).</summary>
    public bool Rejected { get; internal set; }

    public long TotalScore { get; internal set; }

    /// <summary>Number of clear waves this turn (1 = direct match, 2+ = cascades).</summary>
    public int CascadeWaves { get; internal set; }

    /// <summary>Compounding multiplier applied to the final wave (1, then x growth per wave).</summary>
    public double PeakMultiplier { get; internal set; }

    public int TokensCleared { get; internal set; }

    /// <summary>Glass tokens that self-deleted uncleared at end of turn.</summary>
    public int GlassShattered { get; internal set; }

    /// <summary>Flat points granted by board attachments (already included in TotalScore).</summary>
    public long AttachmentFlatBonus { get; internal set; }

    public List<string> Events { get; } = new();

    /// <summary>
    /// Structured playback timeline for renderers: every removal, gravity slide, and
    /// settle in the order it happened. Mirrors the same mutations described in
    /// <see cref="Events"/>; purely additive — headless consumers can ignore it.
    /// </summary>
    public List<TurnStep> Steps { get; } = new();
}
