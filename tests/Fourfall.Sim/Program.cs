using Fourfall.Core;

// Headless verification harness for the Fourfall core engine.
// Phase 1: deterministic scenarios for each rule. Phase 2: 100 randomized drops
// with structural invariant checks after every turn, plus a TurnStep replay check:
// applying each turn's recorded step timeline to a mirror grid (the same way the
// Godot BoardView animates it) must reproduce the engine's board exactly.
// Phase 3: rogue-lite meta-progression — Sector hazards (slag, jammed columns),
// board attachments, the Requisition Terminal economy, and a fully automated
// multi-Sector run proving the shop data integrates with the token physics.
// Exit code 0 = all green.

int failures = 0;

void Check(bool condition, string message)
{
    if (!condition)
    {
        failures++;
        Console.WriteLine($"  FAIL: {message}");
    }
}

Console.WriteLine("=== Phase 1: deterministic scenarios ===");

Scenario("Gravity: tokens stack from row 0 upward", () =>
{
    var engine = new GameEngine();
    engine.DropToken(new StandardToken(TokenColor.Red), 3);
    engine.DropToken(new StandardToken(TokenColor.Blue), 3);
    Check(engine.Board.GetToken(3, 0)?.Color == TokenColor.Red, "first token should rest at row 0");
    Check(engine.Board.GetToken(3, 1)?.Color == TokenColor.Blue, "second token should rest at row 1");
    Check(engine.Board.GetToken(3, 2) is null, "row 2 should be empty");
});

Scenario("Horizontal 4-in-a-row clears and scores", () =>
{
    var engine = new GameEngine();
    for (int col = 0; col < 3; col++)
    {
        engine.DropToken(new StandardToken(TokenColor.Red), col);
    }

    TurnResult result = engine.DropToken(new StandardToken(TokenColor.Red), 3);
    Check(result.CascadeWaves == 1, $"expected 1 wave, got {result.CascadeWaves}");
    Check(result.TokensCleared == 4, $"expected 4 cleared, got {result.TokensCleared}");
    Check(result.TotalScore == 40, $"expected score 40, got {result.TotalScore}");
    Check(engine.Board.Count() == 0, "board should be empty after clear");
});

Scenario("Vertical 4-in-a-row clears", () =>
{
    var engine = new GameEngine();
    for (int i = 0; i < 3; i++)
    {
        engine.DropToken(new StandardToken(TokenColor.Green), 0);
    }

    TurnResult result = engine.DropToken(new StandardToken(TokenColor.Green), 0);
    Check(result.CascadeWaves == 1, $"expected 1 wave, got {result.CascadeWaves}");
    Check(engine.Board.Count() == 0, "column should be empty after vertical clear");
});

Scenario("Diagonal 4-in-a-row clears", () =>
{
    var engine = new GameEngine();
    // Staircase of Yellow at (0,0),(1,1),(2,2),(3,3) on Blue filler.
    engine.DropToken(new StandardToken(TokenColor.Yellow), 0);
    engine.DropToken(new StandardToken(TokenColor.Blue), 1);
    engine.DropToken(new StandardToken(TokenColor.Yellow), 1);
    engine.DropToken(new StandardToken(TokenColor.Blue), 2);
    engine.DropToken(new StandardToken(TokenColor.Blue), 2);
    engine.DropToken(new StandardToken(TokenColor.Yellow), 2);
    engine.DropToken(new StandardToken(TokenColor.Blue), 3);
    engine.DropToken(new StandardToken(TokenColor.Blue), 3);
    engine.DropToken(new StandardToken(TokenColor.Blue), 3);
    TurnResult result = engine.DropToken(new StandardToken(TokenColor.Yellow), 3);
    Check(result.CascadeWaves >= 1, "diagonal should trigger a clear");
    Check(result.TokensCleared >= 4, $"expected >=4 cleared, got {result.TokensCleared}");
});

Scenario("Cascade: splice pulls token down into a second match, multiplier compounds", () =>
{
    var engine = new GameEngine(); // growth 2.0
    engine.DropToken(new StandardToken(TokenColor.Blue), 0);
    engine.DropToken(new StandardToken(TokenColor.Blue), 1);
    engine.DropToken(new StandardToken(TokenColor.Blue), 2);
    engine.DropToken(new StandardToken(TokenColor.Red), 3);  // (3,0)
    engine.DropToken(new StandardToken(TokenColor.Blue), 3); // (3,1) - falls into B B B B after wave 1
    engine.DropToken(new StandardToken(TokenColor.Red), 4);
    engine.DropToken(new StandardToken(TokenColor.Red), 5);
    TurnResult result = engine.DropToken(new StandardToken(TokenColor.Red), 6);

    Check(result.CascadeWaves == 2, $"expected 2 waves, got {result.CascadeWaves}");
    // Wave 1: 4 reds x10 x1 = 40. Wave 2: 4 blues x10 x2 = 80.
    Check(result.TotalScore == 120, $"expected score 120, got {result.TotalScore}");
    Check(result.PeakMultiplier == 2.0, $"expected peak multiplier 2, got {result.PeakMultiplier}");
    Check(engine.Board.Count() == 0, "board should be empty after cascade");
});

Scenario("Iron token deletes the token beneath and settles one row lower", () =>
{
    var engine = new GameEngine();
    engine.DropToken(new StandardToken(TokenColor.Red), 0);
    engine.DropToken(new IronToken(TokenColor.Blue), 0);
    Check(engine.Board.GetToken(0, 0) is IronToken, "iron should occupy row 0 (parity shifted)");
    Check(engine.Board.GetToken(0, 1) is null, "row 1 should be empty after iron consumed the red");
    Check(engine.Board.Count() == 1, "only the iron token should remain in the column");
});

Scenario("Iron token into an empty column settles normally", () =>
{
    var engine = new GameEngine();
    engine.DropToken(new IronToken(TokenColor.Blue), 2);
    Check(engine.Board.GetToken(2, 0) is IronToken, "iron should rest at row 0 with nothing to delete");
});

Scenario("Iron token into a full column consumes the top token", () =>
{
    var engine = new GameEngine();
    for (int i = 0; i < Board.Rows; i++)
    {
        engine.DropToken(new StandardToken((TokenColor)(i % 2 == 0 ? 0 : 2)), 0);
    }

    Check(engine.Board.IsColumnFull(0), "setup: column 0 should be full");
    TurnResult result = engine.DropToken(new IronToken(TokenColor.Green), 0);
    Check(!result.Rejected, "iron drop into full column should not be rejected");
    Check(engine.Board.GetToken(0, Board.Rows - 1) is IronToken, "iron should replace the top token");
});

Scenario("Drain token deletes the bottom token and pulls the column down", () =>
{
    var engine = new GameEngine();
    engine.DropToken(new StandardToken(TokenColor.Red), 0);  // bottom - will be drained
    engine.DropToken(new StandardToken(TokenColor.Blue), 0);
    TurnResult result = engine.DropToken(new DrainToken(TokenColor.Green), 0);
    Check(!result.Rejected, "drain drop should succeed");
    Check(engine.Board.GetToken(0, 0)?.Color == TokenColor.Blue, "blue should be pulled down to row 0");
    Check(engine.Board.GetToken(0, 1) is DrainToken, "drain should settle at row 1");
    Check(engine.Board.Count() == 2, "red should be gone");
});

Scenario("Uncleared glass shatters at end of turn", () =>
{
    var engine = new GameEngine();
    TurnResult result = engine.DropToken(new GlassToken(TokenColor.Purple), 4);
    Check(result.GlassShattered == 1, $"expected 1 shattered, got {result.GlassShattered}");
    Check(engine.Board.Count() == 0, "glass should self-delete leaving an empty board");
});

Scenario("Glass cleared in a match scores x3 and does not shatter", () =>
{
    var engine = new GameEngine();
    for (int col = 0; col < 3; col++)
    {
        engine.DropToken(new StandardToken(TokenColor.Red), col);
    }

    TurnResult result = engine.DropToken(new GlassToken(TokenColor.Red), 3);
    // 3 standard x10 + 1 glass x10x3 = 60, multiplier x1.
    Check(result.TotalScore == 60, $"expected score 60, got {result.TotalScore}");
    Check(result.GlassShattered == 0, "cleared glass must not count as shattered");
    Check(engine.Board.Count() == 0, "board should be empty");
});

Scenario("Drain splice can complete a line and trigger a clear", () =>
{
    var engine = new GameEngine();
    engine.DropToken(new StandardToken(TokenColor.Red), 0);
    engine.DropToken(new StandardToken(TokenColor.Red), 1);
    engine.DropToken(new StandardToken(TokenColor.Red), 2);
    engine.DropToken(new StandardToken(TokenColor.Blue), 3); // (3,0)
    engine.DropToken(new StandardToken(TokenColor.Red), 3);  // (3,1)
    // Drain the blue out from under the red: red falls to (3,0), completing the line.
    TurnResult result = engine.DropToken(new DrainToken(TokenColor.Yellow), 3);
    Check(result.CascadeWaves == 1, $"drain-induced settle should clear, got {result.CascadeWaves} waves");
    Check(engine.Board.Count() == 1, "only the drain token should remain");
});

Console.WriteLine();
Console.WriteLine("=== Phase 3: meta-progression scenarios ===");

Scenario("Slag never matches, even color-aligned", () =>
{
    var engine = new GameEngine();
    engine.Board.Place(new StandardToken(TokenColor.Red), 0, 0);
    engine.Board.Place(new StandardToken(TokenColor.Red), 1, 0);
    engine.Board.Place(new StandardToken(TokenColor.Red), 2, 0);
    engine.Board.Place(new SlagToken(), 3, 0); // slag's inert color slot is Red
    Check(MatchScanner.FindMatches(engine.Board).Count == 0, "slag must not complete a red run");
});

Scenario("Slag resists iron and drain removal", () =>
{
    var engine = new GameEngine();
    engine.Board.Place(new SlagToken(), 0, 0);
    engine.DropToken(new IronToken(TokenColor.Blue), 0);
    Check(engine.Board.GetToken(0, 0) is SlagToken, "iron must not crush slag beneath it");
    Check(engine.Board.GetToken(0, 1) is IronToken, "iron should settle on top of slag");

    engine.DropToken(new DrainToken(TokenColor.Green), 0);
    Check(engine.Board.GetToken(0, 0) is SlagToken, "drain must not flush bottom slag");
    Check(engine.Board.Count() == 3, "drain should settle without removing anything");
});

Scenario("Jammed column rejects every drop without mutating the board", () =>
{
    var engine = new GameEngine();
    engine.Board.Place(new StandardToken(TokenColor.Red), 2, 0);
    engine.Board.JamColumn(2);

    TurnResult standard = engine.DropToken(new StandardToken(TokenColor.Blue), 2);
    Check(standard.Rejected, "standard drop into jammed column must be rejected");

    TurnResult iron = engine.DropToken(new IronToken(TokenColor.Blue), 2);
    Check(iron.Rejected, "iron drop into jammed column must be rejected");
    Check(engine.Board.GetToken(2, 0)?.Color == TokenColor.Red, "jam rejection must not run drop effects");
    Check(engine.Board.Count() == 1, "board must be untouched after jam rejections");
});

Scenario("Hazard application: slag count, gravity consistency, jam flag", () =>
{
    var board = new Board();
    var hazard = new SectorHazard
    {
        Name = "Test",
        Description = "3 slag + jam column 3",
        SlagCount = 3,
        JammedColumn = 2,
    };
    hazard.ApplyTo(board, new Random(11));

    Check(board.Count() == 3, $"expected 3 slag, got {board.Count()}");
    Check(board.IsColumnJammed(2), "column 2 should be jammed");
    foreach ((int column, int row, Token token) in board.Occupied())
    {
        Check(token is SlagToken, $"hazard placed non-slag at ({column},{row})");
        Check(column != 2, "slag must not be seeded into the jammed column");
        Check(row == 0 || board.GetToken(column, row - 1) is not null, "hazard slag is floating");
    }
});

Scenario("Golden Chute: +20 flat for settling in column index 3", () =>
{
    var engine = new GameEngine();
    engine.Attachments.Add(new GoldenChute());

    TurnResult onTarget = engine.DropToken(new StandardToken(TokenColor.Red), 3);
    Check(onTarget.TotalScore == 20, $"expected +20 in chute column, got {onTarget.TotalScore}");
    Check(onTarget.AttachmentFlatBonus == 20, "bonus should be attributed to attachments");

    TurnResult offTarget = engine.DropToken(new StandardToken(TokenColor.Blue), 0);
    Check(offTarget.TotalScore == 0, $"expected 0 off-column, got {offTarget.TotalScore}");
});

Scenario("Heated Baseplate: x3 only when the wave touches the bottom row", () =>
{
    var bottom = new GameEngine();
    bottom.Attachments.Add(new HeatedBaseplate());
    for (int col = 0; col < 3; col++)
    {
        bottom.DropToken(new StandardToken(TokenColor.Red), col);
    }

    TurnResult heated = bottom.DropToken(new StandardToken(TokenColor.Red), 3);
    // 4 tokens x10 x1 cascade x3 baseplate = 120.
    Check(heated.TotalScore == 120, $"expected 120 on bottom row, got {heated.TotalScore}");

    var raised = new GameEngine();
    raised.Attachments.Add(new HeatedBaseplate());
    // Inert bottom row, then a red run on row 1.
    raised.Board.Place(new StandardToken(TokenColor.Blue), 0, 0);
    raised.Board.Place(new StandardToken(TokenColor.Green), 1, 0);
    raised.Board.Place(new StandardToken(TokenColor.Yellow), 2, 0);
    raised.Board.Place(new StandardToken(TokenColor.Blue), 3, 0);
    raised.Board.Place(new StandardToken(TokenColor.Red), 0, 1);
    raised.Board.Place(new StandardToken(TokenColor.Red), 1, 1);
    raised.Board.Place(new StandardToken(TokenColor.Red), 2, 1);
    TurnResult unheated = raised.DropToken(new StandardToken(TokenColor.Red), 3);
    Check(unheated.TotalScore == 40, $"expected 40 off bottom row, got {unheated.TotalScore}");
});

Scenario("Both attachments stack: chute flat bonus + baseplate wave multiplier", () =>
{
    var engine = new GameEngine();
    engine.Attachments.Add(new GoldenChute());
    engine.Attachments.Add(new HeatedBaseplate());
    for (int col = 0; col < 3; col++)
    {
        engine.DropToken(new StandardToken(TokenColor.Red), col);
    }

    TurnResult result = engine.DropToken(new StandardToken(TokenColor.Red), 3);
    // +20 chute, then 4x10 x1 x3 = 120 -> 140 total.
    Check(result.TotalScore == 140, $"expected 140 combined, got {result.TotalScore}");
});

Scenario("Requisition Terminal: purchase mechanics and credit accounting", () =>
{
    var state = new RunState { Credits = 1000 };
    RequisitionTerminal terminal = RequisitionTerminal.OpenFor(state);

    Check(terminal.Stock.Count == 5, $"expected 5 offers (3 tokens + 2 attachments), got {terminal.Stock.Count}");
    Check(terminal.Stock.OfType<TokenOffer>().Count() == 3, "expected Iron/Glass/Drain token offers");

    int bagBefore = state.Bag.Count;
    long cost = terminal.Stock[0].Cost;
    Check(terminal.TryPurchase(0, state), "first purchase should succeed");
    Check(state.Credits == 1000 - cost, $"credits should drop by {cost}, at {state.Credits}");
    Check(state.Bag.Count == bagBefore + 1, "token purchase should grow the bag");
    Check(!terminal.TryPurchase(0, state), "re-buying a sold offer must fail");

    int attachmentIndex = -1;
    for (int i = 0; i < terminal.Stock.Count; i++)
    {
        if (terminal.Stock[i] is AttachmentOffer)
        {
            attachmentIndex = i;
            break;
        }
    }

    Check(attachmentIndex >= 0, "stock should contain an attachment offer");
    Check(terminal.TryPurchase(attachmentIndex, state), "attachment purchase should succeed");
    Check(state.Attachments.Count == 1, "attachment should be welded onto the run");

    state.Credits = 0;
    Check(!terminal.TryPurchase(attachmentIndex + 1, state), "broke purchase must fail");
    Check(state.Credits == 0, "failed purchase must not change credits");

    RequisitionTerminal restock = RequisitionTerminal.OpenFor(state);
    Check(
        restock.Stock.OfType<AttachmentOffer>().All(o => o.Attachment.GetType() != state.Attachments[0].GetType()),
        "owned attachments must not be offered again");
});

Scenario("Quota loop: clearing a quota opens the shop, closing it advances", () =>
{
    var run = new RunManager(new RunConfig { Seed = 5, ColorCount = 3, QuotaBase = 10 });
    Check(run.State.QuotaTarget == 10, $"first target should be 10, got {run.State.QuotaTarget}");

    // Force a quota clear through the public surface: bank a guaranteed match.
    for (int col = 0; col < 3; col++)
    {
        run.Engine.Board.Place(new StandardToken(TokenColor.Red), col, 0);
    }

    int parked = 0;
    while (run.State.Phase == RunPhase.Quota && !(run.NextToken is StandardToken { Color: TokenColor.Red }))
    {
        run.PlayTurn(4 + parked % 3); // park non-red draws across the side columns
        parked++;
    }

    if (run.State.Phase == RunPhase.Quota)
    {
        run.PlayTurn(3);
    }

    Check(run.State.Phase == RunPhase.Shop, $"clearing the quota should open the shop, phase={run.State.Phase}");
    Check(run.Terminal is not null, "terminal should be open");
    Check(run.State.Credits >= run.State.QuotaTarget, "credits should bank the scored points");

    run.CloseShop();
    Check(run.State.Phase == RunPhase.Quota, "closing the shop should resume play");
    Check(run.State.QuotaIndex == 1, "quota index should advance");
    Check(run.State.QuotaScore == 0, "quota score should reset");
    Check(run.Engine.Board.Count() == 0, "sector 1 has no hazard: fresh board should be empty");
});

Console.WriteLine();
Console.WriteLine("=== Phase 2: 100 randomized drops ===");

int seed = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 20260609;
Console.WriteLine($"Seed: {seed}");

var rng = new Random(seed);
var fuzzEngine = new GameEngine();
var mirror = new Token?[Board.Columns, Board.Rows];

long totalScore = 0;
int totalWaves = 0;
int maxWaves = 0;
int totalCleared = 0;
int totalShattered = 0;
int rejected = 0;
int exceptions = 0;

for (int i = 0; i < 100; i++)
{
    // Prefer a non-full column so drops keep exercising match/cascade logic, but
    // every 10th drop targets any column to keep the full-column rejection path hot.
    int column = rng.Next(Board.Columns);
    if (i % 10 != 0)
    {
        for (int probe = 0; probe < Board.Columns && fuzzEngine.Board.IsColumnFull(column); probe++)
        {
            column = (column + 1) % Board.Columns;
        }
    }

    // 3 colors keeps random matches frequent enough to stress cascades.
    var color = (TokenColor)rng.Next(3);
    int roll = rng.Next(100);
    Token token = roll switch
    {
        < 70 => new StandardToken(color),
        < 80 => new IronToken(color),
        < 90 => new GlassToken(color),
        _ => new DrainToken(color),
    };

    TurnResult? lastResult = null;
    try
    {
        TurnResult result = fuzzEngine.DropToken(token, column);
        lastResult = result;
        if (result.Rejected)
        {
            rejected++;
        }

        totalScore += result.TotalScore;
        totalWaves += result.CascadeWaves;
        maxWaves = Math.Max(maxWaves, result.CascadeWaves);
        totalCleared += result.TokensCleared;
        totalShattered += result.GlassShattered;
    }
    catch (Exception ex)
    {
        exceptions++;
        failures++;
        Console.WriteLine($"  FAIL: drop {i} ({token.Kind} {token.Color} -> col {column}) threw {ex.GetType().Name}: {ex.Message}");
        continue;
    }

    foreach (string violation in CheckInvariants(fuzzEngine.Board))
    {
        failures++;
        Console.WriteLine($"  FAIL: drop {i} invariant violated: {violation}");
    }

    foreach (string violation in ReplayAndCompare(mirror, lastResult!, fuzzEngine.Board))
    {
        failures++;
        Console.WriteLine($"  FAIL: drop {i} step replay: {violation}");
    }
}

Check(totalWaves > 0, "fuzz produced zero clear waves - cascade path was never exercised");
Check(totalCleared >= totalWaves * 4, "cleared token count inconsistent with wave count");

Console.WriteLine($"Drops: 100  Rejected (full column): {rejected}  Exceptions: {exceptions}");
Console.WriteLine($"Score: {totalScore}  Clear waves: {totalWaves}  Max chain: {maxWaves}");
Console.WriteLine($"Tokens cleared: {totalCleared}  Glass shattered: {totalShattered}");
Console.WriteLine($"Final board occupancy: {fuzzEngine.Board.Count()}/42");
Console.WriteLine(fuzzEngine.Board.Render());

Console.WriteLine();
Console.WriteLine("=== Phase 3 integration: automated multi-Sector run ===");

{
    var run = new RunManager(new RunConfig { Seed = seed, ColorCount = 3, QuotaBase = 40 });
    var runRng = new Random(seed);
    int autoTurns = 0;
    int purchases = 0;
    long creditsSpent = 0;
    int deepestSector = 1;

    while (autoTurns < 800 && run.State.Phase != RunPhase.GameOver && run.State.Sector <= 4)
    {
        if (run.State.Phase == RunPhase.Shop)
        {
            // Greedy buyer: grab every affordable offer, then resume operations.
            RequisitionTerminal terminal = run.Terminal!;
            for (int i = 0; i < terminal.Stock.Count; i++)
            {
                long price = terminal.Stock[i].Cost;
                if (run.TryPurchase(i))
                {
                    purchases++;
                    creditsSpent += price;
                }
            }

            Check(run.State.Credits >= 0, "credits went negative after shopping");
            run.CloseShop();

            // The fresh quota board must honor the active Sector hazard.
            foreach (int jam in run.Engine.Board.JammedColumns())
            {
                Check(run.Hazard.JammedColumn == jam, $"unexpected jammed column {jam}");
            }

            int slag = run.Engine.Board.Occupied().Count(c => c.Token is SlagToken);
            Check(slag <= run.Hazard.SlagCount, $"hazard seeded {slag} slag, max {run.Hazard.SlagCount}");
            continue;
        }

        // Random legal column; lockout (no playable column) is detected by the
        // manager itself right after the turn that fills the board.
        int column = runRng.Next(Board.Columns);
        for (int probe = 0; probe < Board.Columns && run.Engine.Board.GetLandingRow(column) < 0; probe++)
        {
            column = (column + 1) % Board.Columns;
        }

        if (run.Engine.Board.GetLandingRow(column) < 0)
        {
            Check(false, "auto-player found no legal column while phase is still Quota");
            break;
        }

        try
        {
            run.PlayTurn(column);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine($"  FAIL: auto-run turn {autoTurns} threw {ex.GetType().Name}: {ex.Message}");
            break;
        }

        autoTurns++;
        deepestSector = Math.Max(deepestSector, run.State.Sector);

        foreach (string violation in CheckInvariants(run.Engine.Board))
        {
            failures++;
            Console.WriteLine($"  FAIL: auto-run turn {autoTurns} invariant violated: {violation}");
        }
    }

    Check(run.State.QuotasCleared >= 1, "auto-run never cleared a quota - economy loop unreachable");
    Check(purchases >= 1, "auto-run never purchased - shop economy was never exercised");
    Check(run.State.Credits >= 0, "run ended with negative credits");

    Console.WriteLine($"Turns: {autoTurns}  Quotas cleared: {run.State.QuotasCleared}  Deepest sector: {deepestSector}");
    Console.WriteLine($"Purchases: {purchases}  Credits spent: {creditsSpent}  Credits banked: {run.State.Credits}");
    Console.WriteLine($"Bag size: {run.State.Bag.Count}  Attachments: {run.State.Attachments.Count}  Final phase: {run.State.Phase}");
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

void Scenario(string name, Action body)
{
    Console.WriteLine($"- {name}");
    int before = failures;
    try
    {
        body();
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"  FAIL: threw {ex.GetType().Name}: {ex.Message}");
    }

    if (failures == before)
    {
        Console.WriteLine("  ok");
    }
}

// Applies a turn's recorded step timeline to the mirror grid using the same
// mechanics as the Godot BoardView (removals lift cells, moves are read in a lift
// pass then a place pass, settles fill an empty cell), then verifies the mirror
// matches the engine board token-for-token.
static List<string> ReplayAndCompare(Token?[,] mirror, TurnResult result, Board board)
{
    var violations = new List<string>();

    foreach (TurnStep step in result.Steps)
    {
        if (step.Kind == TurnStepKind.Settle)
        {
            if (step.SettledToken is null)
            {
                violations.Add("settle step missing its token");
            }
            else if (mirror[step.SettleColumn, step.SettleRow] is not null)
            {
                violations.Add($"settle into occupied mirror cell ({step.SettleColumn},{step.SettleRow})");
            }
            else
            {
                mirror[step.SettleColumn, step.SettleRow] = step.SettledToken;
            }

            continue;
        }

        foreach ((int column, int row) in step.RemovedCells)
        {
            if (mirror[column, row] is null)
            {
                violations.Add($"{step.Kind} removal from empty mirror cell ({column},{row})");
            }

            mirror[column, row] = null;
        }

        var lifted = new List<(TokenMove Move, Token? Token)>();
        foreach (TokenMove move in step.Moves)
        {
            Token? moving = mirror[move.FromColumn, move.FromRow];
            if (moving is null)
            {
                violations.Add($"{step.Kind} move from empty mirror cell ({move.FromColumn},{move.FromRow})");
            }

            mirror[move.FromColumn, move.FromRow] = null;
            lifted.Add((move, moving));
        }

        foreach ((TokenMove move, Token? moving) in lifted)
        {
            if (mirror[move.ToColumn, move.ToRow] is not null)
            {
                violations.Add($"{step.Kind} move into occupied mirror cell ({move.ToColumn},{move.ToRow})");
            }

            mirror[move.ToColumn, move.ToRow] = moving;
        }
    }

    for (int column = 0; column < Board.Columns; column++)
    {
        for (int row = 0; row < Board.Rows; row++)
        {
            if (!ReferenceEquals(mirror[column, row], board.GetToken(column, row)))
            {
                violations.Add($"mirror desync at ({column},{row}) after replay");
            }
        }
    }

    return violations;
}

static List<string> CheckInvariants(Board board)
{
    var violations = new List<string>();

    for (int column = 0; column < Board.Columns; column++)
    {
        bool gapSeen = false;
        for (int row = 0; row < Board.Rows; row++)
        {
            Token? token = board.GetToken(column, row);
            if (token is null)
            {
                gapSeen = true;
            }
            else if (gapSeen)
            {
                violations.Add($"floating token at ({column},{row}) - gravity not settled");
            }
        }
    }

    foreach ((int column, int row, Token token) in board.Occupied())
    {
        if (token is GlassToken { ShatterPending: true })
        {
            violations.Add($"glass token at ({column},{row}) survived end of turn with shatter flag");
        }
    }

    return violations;
}
