namespace Fourfall.Core;

public enum RunPhase
{
    /// <summary>Dropping tokens against the current Quota target.</summary>
    Quota,

    /// <summary>Quota cleared; the Requisition Terminal is open.</summary>
    Shop,

    /// <summary>The board locked out before the Quota was met. Run over.</summary>
    GameOver,
}

/// <summary>A purchasable token kind; the color is rolled fresh on every draw.</summary>
public readonly record struct TokenSpec(TokenKind Kind)
{
    public Token Create(TokenColor color) => Kind switch
    {
        TokenKind.Standard => new StandardToken(color),
        TokenKind.Iron => new IronToken(color),
        TokenKind.Glass => new GlassToken(color),
        TokenKind.Drain => new DrainToken(color),
        _ => throw new InvalidOperationException($"{Kind} tokens cannot be drawn from the bag."),
    };
}

/// <summary>Tuning knobs for a run; defaults are the shipping economy.</summary>
public sealed class RunConfig
{
    /// <summary>Fixed seed for deterministic runs (headless verification); null = random.</summary>
    public int? Seed { get; init; }

    /// <summary>Colors in the draw pool. Fewer colors = denser matches (sim tuning).</summary>
    public int ColorCount { get; init; } = 5;

    /// <summary>Target score of Sector 1, Quota 1.</summary>
    public long QuotaBase { get; init; } = 100;

    /// <summary>Geometric growth per Quota step across the whole run.</summary>
    public double QuotaGrowth { get; init; } = 1.5;
}

/// <summary>
/// All persistent rogue-lite state for one run: Sector/Quota position, the score
/// currency, the token Bag drawn from each turn, and welded attachments.
/// </summary>
public sealed class RunState
{
    public const int QuotasPerSector = 3;

    public RunPhase Phase { get; internal set; } = RunPhase.Quota;

    /// <summary>1-based Sector number.</summary>
    public int Sector { get; internal set; } = 1;

    /// <summary>0-based Quota index within the Sector (0..2).</summary>
    public int QuotaIndex { get; internal set; }

    /// <summary>Score the current Quota demands before the board fills.</summary>
    public long QuotaTarget { get; internal set; }

    /// <summary>Score accumulated toward the current Quota.</summary>
    public long QuotaScore { get; internal set; }

    /// <summary>Spendable currency: every point scored is also banked as credits.</summary>
    public long Credits { get; internal set; }

    public int QuotasCleared { get; internal set; }

    /// <summary>
    /// The draw bag. Each turn pulls a uniformly random entry, so purchases shift
    /// the odds toward upgraded tokens without removing the standard floor.
    /// </summary>
    public List<TokenSpec> Bag { get; } = new()
    {
        new TokenSpec(TokenKind.Standard),
        new TokenSpec(TokenKind.Standard),
        new TokenSpec(TokenKind.Standard),
        new TokenSpec(TokenKind.Standard),
        new TokenSpec(TokenKind.Standard),
        new TokenSpec(TokenKind.Standard),
        new TokenSpec(TokenKind.Standard),
        new TokenSpec(TokenKind.Standard),
        new TokenSpec(TokenKind.Standard),
        new TokenSpec(TokenKind.Standard),
    };

    /// <summary>Board attachments owned for the run, applied to every Quota's engine.</summary>
    public List<Attachment> Attachments { get; } = new();
}
