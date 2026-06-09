namespace Fourfall.Core;

/// <summary>
/// Orchestrates a turn: drop effect, gravity settle, recursive cascade resolution
/// with a compounding multiplier, then end-of-turn glass shatter.
/// </summary>
public sealed class GameEngine
{
    public Board Board { get; } = new();

    /// <summary>Cascade multiplier growth per wave: wave 1 = x1, wave 2 = x2, wave 3 = x4...</summary>
    public double CascadeGrowth { get; init; } = 2.0;

    /// <summary>Welded board attachments contributing flat bonuses and wave multipliers.</summary>
    public List<Attachment> Attachments { get; } = new();

    public TurnResult DropToken(Token token, int column)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (column < 0 || column >= Board.Columns)
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, "Column outside board.");
        }

        var result = new TurnResult();

        // Jammed columns reject every drop before any rule-breaker effect can fire.
        if (Board.IsColumnJammed(column))
        {
            result.Rejected = true;
            result.Events.Add($"Rejected: column {column} is jammed.");
            return result;
        }

        // Structural rule-breakers (iron, drain) mutate the column before landing.
        Dictionary<Token, (int Column, int Row)> beforeEffect = Snapshot();
        token.ApplyDropEffect(Board, column);
        RecordDiff(result, TurnStepKind.DropEffect, beforeEffect);

        int landing = Board.GetLandingRow(column);
        if (landing < 0)
        {
            // A column can still be full here when the drop effect was a no-op
            // (standard/glass always, iron blocked by indestructible slag on top),
            // so rejecting leaves the board untouched.
            result.Rejected = true;
            result.Events.Add($"Rejected: column {column} full.");
            return result;
        }

        Board.Place(token, column, landing);
        token.OnPlaced();
        result.Events.Add($"{token.Kind} {token.Color} settled at ({column},{landing}).");
        result.Steps.Add(new TurnStep
        {
            Kind = TurnStepKind.Settle,
            SettleColumn = column,
            SettleRow = landing,
            SettledToken = token,
        });

        foreach (Attachment attachment in Attachments)
        {
            long bonus = attachment.OnTokenSettled(column, landing);
            if (bonus != 0)
            {
                result.TotalScore += bonus;
                result.AttachmentFlatBonus += bonus;
                result.Events.Add($"{attachment.Name}: +{bonus}.");
            }
        }

        double multiplier = 1.0;
        ResolveCascades(result, ref multiplier);
        ShatterGlass(result, ref multiplier);

        return result;
    }

    /// <summary>
    /// Clears matches, splices the board downward, and rescans the new state until
    /// stable. The multiplier compounds across waves and persists into the glass
    /// shatter phase so shatter-triggered cascades keep climbing.
    /// </summary>
    private void ResolveCascades(TurnResult result, ref double multiplier)
    {
        while (true)
        {
            HashSet<(int Column, int Row)> matches = MatchScanner.FindMatches(Board);
            if (matches.Count == 0)
            {
                return;
            }

            result.CascadeWaves++;
            result.PeakMultiplier = multiplier;

            var step = new TurnStep
            {
                Kind = TurnStepKind.ClearWave,
                WaveNumber = result.CascadeWaves,
                Multiplier = multiplier,
            };
            step.RemovedCells.AddRange(matches);

            long waveScore = 0;
            foreach ((int column, int row) in matches)
            {
                Token token = Board.GetToken(column, row)
                    ?? throw new InvalidOperationException($"Match at empty cell ({column},{row}).");
                waveScore += (long)token.BaseScore * token.ScoreMultiplier;
                Board.RemoveAt(column, row);
            }

            double attachmentMult = 1.0;
            foreach (Attachment attachment in Attachments)
            {
                attachmentMult *= attachment.GetWaveMultiplier(matches);
            }

            long scored = (long)(waveScore * multiplier * attachmentMult);
            result.TotalScore += scored;
            result.TokensCleared += matches.Count;
            string attachmentNote = attachmentMult != 1.0 ? $" [attachments x{attachmentMult:0.##}]" : "";
            result.Events.Add(
                $"Wave {result.CascadeWaves}: cleared {matches.Count} tokens for {scored} (x{multiplier:0.##}){attachmentNote}.");

            step.WaveScore = scored;
            Dictionary<Token, (int Column, int Row)> beforeGravity = Snapshot();
            Board.ApplyGravity();
            AppendMoves(step, beforeGravity);
            result.Steps.Add(step);

            multiplier *= CascadeGrowth;
        }
    }

    /// <summary>
    /// End of turn: any glass token still flagged from placement self-deletes, the
    /// board splices down, and the resulting state is rescanned for fresh cascades.
    /// </summary>
    private void ShatterGlass(TurnResult result, ref double multiplier)
    {
        var pending = Board.Occupied()
            .Where(cell => cell.Token is GlassToken { ShatterPending: true })
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        var step = new TurnStep { Kind = TurnStepKind.GlassShatter };
        foreach ((int column, int row, _) in pending)
        {
            step.RemovedCells.Add((column, row));
            Board.RemoveAt(column, row);
        }

        result.GlassShattered += pending.Count;
        result.Events.Add($"End of turn: {pending.Count} glass token(s) shattered.");

        Dictionary<Token, (int Column, int Row)> beforeGravity = Snapshot();
        Board.ApplyGravity();
        AppendMoves(step, beforeGravity);
        result.Steps.Add(step);

        ResolveCascades(result, ref multiplier);
    }

    /// <summary>Maps every on-board token (by reference) to its current cell.</summary>
    private Dictionary<Token, (int Column, int Row)> Snapshot()
    {
        var map = new Dictionary<Token, (int Column, int Row)>();
        foreach ((int column, int row, Token token) in Board.Occupied())
        {
            map[token] = (column, row);
        }

        return map;
    }

    /// <summary>
    /// Diffs the current board against a snapshot and, if anything was removed or
    /// slid, appends a step of the given kind to the result.
    /// </summary>
    private void RecordDiff(
        TurnResult result,
        TurnStepKind kind,
        Dictionary<Token, (int Column, int Row)> before)
    {
        var step = new TurnStep { Kind = kind };
        AppendRemovalsAndMoves(step, before);
        if (step.RemovedCells.Count > 0 || step.Moves.Count > 0)
        {
            result.Steps.Add(step);
        }
    }

    /// <summary>Appends only the slides (tokens present before and after) to the step.</summary>
    private void AppendMoves(TurnStep step, Dictionary<Token, (int Column, int Row)> before)
    {
        Dictionary<Token, (int Column, int Row)> after = Snapshot();
        foreach ((Token token, (int column, int row)) in before)
        {
            if (after.TryGetValue(token, out (int Column, int Row) now) && now != (column, row))
            {
                step.Moves.Add(new TokenMove(column, row, now.Column, now.Row));
            }
        }
    }

    private void AppendRemovalsAndMoves(TurnStep step, Dictionary<Token, (int Column, int Row)> before)
    {
        Dictionary<Token, (int Column, int Row)> after = Snapshot();
        foreach ((Token token, (int column, int row)) in before)
        {
            if (!after.TryGetValue(token, out (int Column, int Row) now))
            {
                step.RemovedCells.Add((column, row));
            }
            else if (now != (column, row))
            {
                step.Moves.Add(new TokenMove(column, row, now.Column, now.Row));
            }
        }
    }
}
