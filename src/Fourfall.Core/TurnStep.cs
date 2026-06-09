namespace Fourfall.Core;

public enum TurnStepKind
{
    /// <summary>Structural mutation from a rule-breaker before landing (iron/drain).</summary>
    DropEffect,

    /// <summary>The dropped token settling at its landing cell.</summary>
    Settle,

    /// <summary>One cascade wave: matched cells clear, then gravity slides.</summary>
    ClearWave,

    /// <summary>End-of-turn glass self-deletion, then gravity slides.</summary>
    GlassShatter,
}

/// <summary>A token sliding from one cell to another during a gravity splice.</summary>
public readonly record struct TokenMove(int FromColumn, int FromRow, int ToColumn, int ToRow);

/// <summary>
/// One visually atomic phase of a turn, in playback order. Pure data so a renderer
/// can animate the turn step by step instead of diffing final board state.
/// </summary>
public sealed class TurnStep
{
    public required TurnStepKind Kind { get; init; }

    /// <summary>Cells whose tokens vanish in this step, positions as of step start.</summary>
    public List<(int Column, int Row)> RemovedCells { get; } = new();

    /// <summary>Gravity slides occurring after the removals in this step.</summary>
    public List<TokenMove> Moves { get; } = new();

    /// <summary>Landing cell for <see cref="TurnStepKind.Settle"/> steps; -1 otherwise.</summary>
    public int SettleColumn { get; set; } = -1;

    public int SettleRow { get; set; } = -1;

    public Token? SettledToken { get; set; }

    /// <summary>Score awarded by this wave, multiplier already applied.</summary>
    public long WaveScore { get; set; }

    /// <summary>Cascade multiplier in force for this wave.</summary>
    public double Multiplier { get; set; }

    public int WaveNumber { get; set; }
}
