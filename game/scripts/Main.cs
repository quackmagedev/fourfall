using Fourfall.Core;
using Godot;

namespace Fourfall.Game;

/// <summary>
/// Game root: owns the Phase 3 <see cref="RunManager"/> (Sector &amp; Quota loop,
/// hazards, bag, Requisition Terminal) and bridges column-selection input to engine
/// turns. The board view plays each turn's step timeline before input unlocks.
/// </summary>
public partial class Main : Node2D
{
    private RunManager _run = null!;

    private BoardView _board = null!;
    private Hud _hud = null!;
    private ShopPanel _shop = null!;
    private bool _turnInFlight;

    public override void _Ready()
    {
        _board = GetNode<BoardView>("BoardView");
        _hud = GetNode<Hud>("Hud");
        _shop = GetNode<ShopPanel>("ShopPanel");

        _board.ColumnSelected += OnColumnSelected;
        _shop.PurchaseRequested += OnPurchaseRequested;
        _shop.ResumeRequested += OnShopResume;

        StartRun();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_run.State.Phase == RunPhase.GameOver && @event.IsActionPressed("ui_accept"))
        {
            StartRun();
        }

        if (@event is InputEventKey { Pressed: true, Keycode: Key.F1 })
        {
            DebugSkipQuota();
        }
    }

    private void DebugSkipQuota()
    {
        if (_turnInFlight || _run.State.Phase != RunPhase.Quota)
        {
            return;
        }

        _run.ForceCompleteQuota();
        _hud.ShowQuota(_run.State);
        AudioManager.Instance?.PlayQuotaCleared();
        _shop.Open(_run.Terminal!, _run.State, ShopContextLine());
    }

    private void StartRun()
    {
        _run = new RunManager();
        _hud.ShowGameOver(false);
        BeginQuota();
        _hud.ShowEvent("Shift start. Match 4 of a color to fill the quota before the board jams up.");
    }

    /// <summary>Syncs board visuals and HUD to a freshly hazard-seeded Quota board.</summary>
    private void BeginQuota()
    {
        _board.ResetBoard();
        foreach ((int column, int row, Token token) in _run.Engine.Board.Occupied())
        {
            _board.SpawnStatic(token, column, row);
        }

        _board.SetJammedColumns(_run.Engine.Board.JammedColumns());

        _hud.ShowRun(_run.State, _run.Hazard);
        _hud.ShowNext(_run.NextToken);
        _hud.ShowEvent($"{_run.Hazard.Name}: {_run.Hazard.Description}");
    }

    private async void OnColumnSelected(int column)
    {
        if (_turnInFlight || _run.State.Phase != RunPhase.Quota)
        {
            return;
        }

        _turnInFlight = true;
        try
        {
            TurnResult result = _run.PlayTurn(column);
            if (result.Rejected)
            {
                AudioManager.Instance?.PlayRejection();
                _hud.ShowEvent(_run.Engine.Board.IsColumnJammed(column)
                    ? $"Column {column + 1} is jammed shut."
                    : $"Column {column + 1} is full.");
                return;
            }

            _hud.ShowNext(_run.NextToken);

            await _board.PlayTurnAsync(result);

            _hud.ShowQuota(_run.State);
            _hud.ShowCredits(_run.State.Credits);
            _hud.ShowEvent(BuildTurnSummary(result));
            _board.SyncCheck(_run.Engine.Board);

            switch (_run.State.Phase)
            {
                case RunPhase.Shop:
                    AudioManager.Instance?.PlayQuotaCleared();
                    _shop.Open(_run.Terminal!, _run.State, ShopContextLine());
                    break;

                case RunPhase.GameOver:
                    AudioManager.Instance?.PlayGameOver();
                    _hud.ShowGameOver(true);
                    _hud.ShowEvent("The board locked out below quota. Press Enter to restart the shift.");
                    break;
            }
        }
        finally
        {
            _turnInFlight = false;
        }
    }

    private void OnPurchaseRequested(int index)
    {
        if (_run.State.Phase != RunPhase.Shop || _run.Terminal is null)
        {
            return;
        }

        if (!_run.TryPurchase(index))
        {
            AudioManager.Instance?.PlayPurchaseDenied();
            _hud.ShowEvent("Requisition denied: insufficient credits.");
            return;
        }

        AudioManager.Instance?.PlayPurchase();
        _hud.ShowCredits(_run.State.Credits);
        _shop.Refresh(_run.Terminal, _run.State, ShopContextLine());
    }

    private void OnShopResume()
    {
        if (_run.State.Phase != RunPhase.Shop)
        {
            return;
        }

        int sectorBefore = _run.State.Sector;
        _shop.Close();
        _run.CloseShop();

        if (_run.State.Sector > sectorBefore)
            AudioManager.Instance?.PlaySectorAdvance();
        else
            AudioManager.Instance?.PlayShopClose();

        BeginQuota();
    }

    private string ShopContextLine() =>
        $"Quota {_run.State.QuotaIndex + 1} cleared. Sector {_run.State.Sector} continues after requisitions.";

    private static string BuildTurnSummary(TurnResult result)
    {
        if (result.CascadeWaves == 0)
        {
            if (result.AttachmentFlatBonus > 0)
            {
                return $"Attachment bonus +{result.AttachmentFlatBonus}.";
            }

            return result.GlassShattered > 0
                ? $"{result.GlassShattered} glass token(s) shattered."
                : "No matches.";
        }

        string summary =
            $"{result.CascadeWaves} wave(s), peak x{result.PeakMultiplier:0.##}, +{result.TotalScore}";
        if (result.GlassShattered > 0)
        {
            summary += $"  ({result.GlassShattered} glass shattered)";
        }

        return summary;
    }
}
