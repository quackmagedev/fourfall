namespace Fourfall.Core;

public enum TokenKind
{
    Standard,
    Iron,
    Glass,
    Drain,
    Slag,
}

/// <summary>
/// Base token. Pure data + rule hooks; no engine or rendering dependencies.
/// </summary>
public abstract class Token
{
    protected Token(TokenColor color) => Color = color;

    public TokenColor Color { get; }

    public abstract TokenKind Kind { get; }

    /// <summary>Points awarded per cell when this token is cleared in a match.</summary>
    public virtual int BaseScore => 10;

    /// <summary>Per-token score multiplier applied on clear.</summary>
    public virtual int ScoreMultiplier => 1;

    /// <summary>False for tokens that never participate in color matches (slag).</summary>
    public virtual bool Matchable => true;

    /// <summary>True for tokens that resist removal by rule-breaker drop effects (slag).</summary>
    public virtual bool Indestructible => false;

    /// <summary>
    /// Structural rule-breaker hook. Called after the column is chosen but before the
    /// landing row is computed, so the token may mutate the column it is falling into.
    /// </summary>
    public virtual void ApplyDropEffect(Board board, int column)
    {
    }

    /// <summary>Called once the token has settled on the board.</summary>
    public virtual void OnPlaced()
    {
    }
}

/// <summary>Normal score behavior, no structural effects.</summary>
public sealed class StandardToken : Token
{
    public StandardToken(TokenColor color) : base(color)
    {
    }

    public override TokenKind Kind => TokenKind.Standard;
}

/// <summary>
/// On drop, deletes the token immediately beneath it (the top of the target column's
/// stack), altering the column's odd/even row parity before settling one row lower.
/// </summary>
public sealed class IronToken : Token
{
    public IronToken(TokenColor color) : base(color)
    {
    }

    public override TokenKind Kind => TokenKind.Iron;

    public override void ApplyDropEffect(Board board, int column)
    {
        int landing = board.GetLandingRow(column);
        // If the column is full the iron token hovers above the top cell; the token
        // "immediately beneath" is the column's topmost token either way.
        int beneath = landing < 0 ? Board.Rows - 1 : landing - 1;
        if (beneath >= 0 && board.GetToken(column, beneath) is { Indestructible: false })
        {
            board.RemoveAt(column, beneath);
        }
    }
}

/// <summary>
/// x3 score multiplier, but flags itself on placement to self-delete at the end of
/// the turn if it was not cleared by a match.
/// </summary>
public sealed class GlassToken : Token
{
    public GlassToken(TokenColor color) : base(color)
    {
    }

    public override TokenKind Kind => TokenKind.Glass;

    public override int ScoreMultiplier => 3;

    /// <summary>Set when placed; any glass still on the board with this flag at end of turn shatters.</summary>
    public bool ShatterPending { get; private set; }

    public override void OnPlaced() => ShatterPending = true;
}

/// <summary>
/// On drop, force-deletes the bottom-most token in the column and splices the whole
/// column down one slot before settling.
/// </summary>
public sealed class DrainToken : Token
{
    public DrainToken(TokenColor color) : base(color)
    {
    }

    public override TokenKind Kind => TokenKind.Drain;

    public override void ApplyDropEffect(Board board, int column)
    {
        if (board.GetToken(column, 0) is { Indestructible: false })
        {
            board.RemoveAt(column, 0);
            board.CollapseColumn(column);
        }
    }
}

/// <summary>
/// Indestructible industrial debris seeded by Sector hazards. Never matches, never
/// scores, and resists iron/drain removal — it simply occupies floor space.
/// </summary>
public sealed class SlagToken : Token
{
    // Slag never participates in color matches, so the color slot is inert.
    public SlagToken() : base(TokenColor.Red)
    {
    }

    public override TokenKind Kind => TokenKind.Slag;

    public override int BaseScore => 0;

    public override bool Matchable => false;

    public override bool Indestructible => true;
}
