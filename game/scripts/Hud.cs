using Fourfall.Core;
using Godot;

namespace Fourfall.Game;

/// <summary>Sector/Quota readouts, credit balance, event line, next-token preview.</summary>
public partial class Hud : CanvasLayer
{
    private Label _sectorLabel = null!;
    private Label _quotaLabel = null!;
    private Label _creditsLabel = null!;
    private Label _eventLabel = null!;
    private Label _nextKindLabel = null!;
    private Label _gameOverLabel = null!;
    private TokenView _nextPreview = null!;

    public override void _Ready()
    {
        _sectorLabel = GetNode<Label>("SectorLabel");
        _quotaLabel = GetNode<Label>("QuotaLabel");
        _creditsLabel = GetNode<Label>("CreditsLabel");
        _eventLabel = GetNode<Label>("EventLabel");
        _nextKindLabel = GetNode<Label>("NextKindLabel");
        _gameOverLabel = GetNode<Label>("GameOverLabel");
        _nextPreview = GetNode<TokenView>("NextPreview");
    }

    public void ShowRun(RunState state, SectorHazard hazard)
    {
        _sectorLabel.Text =
            $"SECTOR {state.Sector} — {hazard.Name.ToUpperInvariant()}  ·  QUOTA {state.QuotaIndex + 1}/{RunState.QuotasPerSector}";
        ShowQuota(state);
        ShowCredits(state.Credits);
    }

    public void ShowQuota(RunState state) =>
        _quotaLabel.Text = $"Quota: {state.QuotaScore} / {state.QuotaTarget}";

    public void ShowCredits(long credits) => _creditsLabel.Text = $"Credits: {credits}";

    public void ShowEvent(string message) => _eventLabel.Text = message;

    public void ShowGameOver(bool visible) => _gameOverLabel.Visible = visible;

    public void ShowNext(Token token)
    {
        _nextPreview.SetToken(token);
        _nextKindLabel.Text = token.Kind == TokenKind.Standard
            ? token.Color.ToString()
            : $"{token.Color} {token.Kind}";
    }
}
