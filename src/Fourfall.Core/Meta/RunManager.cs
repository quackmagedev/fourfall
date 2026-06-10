namespace Fourfall.Core;

/// <summary>
/// Drives the Sector &amp; Quota progression loop on top of the Phase 1 engine.
/// Each Quota plays on a fresh board with the Sector's hazard applied; clearing a
/// Quota opens the Requisition Terminal, clearing three advances the Sector. The
/// run ends when every playable column fills before the Quota target is met.
/// </summary>
public sealed class RunManager
{
    private readonly RunConfig _config;
    private readonly Random _rng;

    public RunManager(RunConfig? config = null)
    {
        _config = config ?? new RunConfig();
        _rng = _config.Seed is int seed ? new Random(seed) : new Random();
        Hazard = HazardCatalog.ForSector(State.Sector, _rng);
        NextToken = DrawFromBag();
        StartQuota();
    }

    public RunState State { get; } = new();

    /// <summary>Engine for the current Quota. Replaced (fresh board) every Quota.</summary>
    public GameEngine Engine { get; private set; } = null!;

    /// <summary>The current Sector's board sabotage. Rerolled on Sector advance.</summary>
    public SectorHazard Hazard { get; private set; }

    /// <summary>Open while <see cref="RunState.Phase"/> is <see cref="RunPhase.Shop"/>.</summary>
    public RequisitionTerminal? Terminal { get; private set; }

    /// <summary>The token the player is about to drop, drawn from the Bag.</summary>
    public Token NextToken { get; private set; }

    /// <summary>
    /// Drops <see cref="NextToken"/> down the column. Scores bank into both the
    /// Quota and the credit balance; phase transitions happen here.
    /// </summary>
    public TurnResult PlayTurn(int column)
    {
        if (State.Phase != RunPhase.Quota)
        {
            throw new InvalidOperationException($"Cannot drop tokens during {State.Phase}.");
        }

        TurnResult result = Engine.DropToken(NextToken, column);
        if (result.Rejected)
        {
            return result;
        }

        State.QuotaScore += result.TotalScore;
        State.Credits += result.TotalScore;
        NextToken = DrawFromBag();

        if (State.QuotaScore >= State.QuotaTarget)
        {
            State.QuotasCleared++;
            State.Phase = RunPhase.Shop;
            Terminal = RequisitionTerminal.OpenFor(State);
        }
        else if (!Engine.Board.HasPlayableColumn())
        {
            State.Phase = RunPhase.GameOver;
        }

        return result;
    }

    /// <summary>Debug helper: immediately satisfies the current Quota and opens the shop.</summary>
    public void ForceCompleteQuota()
    {
        if (State.Phase != RunPhase.Quota)
        {
            return;
        }

        State.QuotaScore = State.QuotaTarget;
        State.QuotasCleared++;
        State.Phase = RunPhase.Shop;
        Terminal = RequisitionTerminal.OpenFor(State);
    }

    /// <summary>Buys from the open terminal. False when closed, sold, or unaffordable.</summary>
    public bool TryPurchase(int offerIndex) =>
        Terminal is not null && Terminal.TryPurchase(offerIndex, State);

    /// <summary>
    /// Leaves the shop and starts the next Quota — advancing the Sector (and
    /// rerolling its hazard) after every third Quota.
    /// </summary>
    public void CloseShop()
    {
        if (State.Phase != RunPhase.Shop)
        {
            throw new InvalidOperationException($"Shop is not open during {State.Phase}.");
        }

        Terminal = null;
        State.QuotaIndex++;
        if (State.QuotaIndex >= RunState.QuotasPerSector)
        {
            State.QuotaIndex = 0;
            State.Sector++;
            Hazard = HazardCatalog.ForSector(State.Sector, _rng);
        }

        State.Phase = RunPhase.Quota;
        StartQuota();
    }

    /// <summary>Fresh board + Sector hazard + welded attachments for the next Quota.</summary>
    private void StartQuota()
    {
        Engine = new GameEngine();
        Engine.Attachments.AddRange(State.Attachments);
        Hazard.ApplyTo(Engine.Board, _rng);

        int step = (State.Sector - 1) * RunState.QuotasPerSector + State.QuotaIndex;
        State.QuotaTarget = (long)Math.Round(
            _config.QuotaBase * Math.Pow(_config.QuotaGrowth, step) / 5.0) * 5;
        State.QuotaScore = 0;
    }

    private Token DrawFromBag()
    {
        TokenSpec spec = State.Bag[_rng.Next(State.Bag.Count)];
        var color = (TokenColor)_rng.Next(_config.ColorCount);
        return spec.Create(color);
    }
}
