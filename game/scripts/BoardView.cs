using System.Collections.Generic;
using System.Threading.Tasks;
using Fourfall.Core;
using Godot;

namespace Fourfall.Game;

/// <summary>
/// Visual 7x6 grid mapped 1:1 onto the Phase 1 <see cref="Board"/> matrix
/// (board row 0 = bottom row of the grid). Owns column-selection input and the
/// sequential playback of a turn's <see cref="TurnStep"/> timeline: clears vanish,
/// columns splice downward, and the next cascade wave only starts once the
/// previous animation has finished.
/// </summary>
public partial class BoardView : Node2D
{
    [Signal]
    public delegate void ColumnSelectedEventHandler(int column);

    [Export]
    public PackedScene? TokenScene { get; set; }

    public const float CellSize = 80f;

    private const float VanishDuration = 0.22f;
    private const float SlideDurationPerRow = 0.07f;
    private const float InterWavePause = 0.12f;

    private readonly TokenView?[,] _views = new TokenView?[Board.Columns, Board.Rows];
    private readonly bool[] _jammed = new bool[Board.Columns];
    private Node2D _tokenLayer = null!;
    private int _hoverColumn = Board.Columns / 2;
    private bool _animating;

    public bool IsAnimating => _animating;

    public override void _Ready()
    {
        _tokenLayer = GetNode<Node2D>("Tokens");
    }

    /// <summary>Center of a board cell in local pixels. Board row 0 maps to the bottom.</summary>
    public static Vector2 CellToLocal(int column, int row) => new(
        column * CellSize + CellSize / 2f,
        (Board.Rows - 1 - row) * CellSize + CellSize / 2f);

    public override void _Draw()
    {
        var boardSize = new Vector2(Board.Columns * CellSize, Board.Rows * CellSize);

        // Backboard behind the slots.
        DrawRect(new Rect2(-8f, -8f, boardSize.X + 16f, boardSize.Y + 16f), new Color("23355e"));
        DrawRect(new Rect2(Vector2.Zero, boardSize), new Color("1b2a4a"));

        // Active column highlight while waiting for input.
        if (!_animating && _hoverColumn >= 0)
        {
            DrawRect(
                new Rect2(_hoverColumn * CellSize, 0f, CellSize, boardSize.Y),
                new Color(1f, 1f, 1f, 0.07f));
        }

        // Empty slot wells; spawned tokens sit on top of these.
        for (int column = 0; column < Board.Columns; column++)
        {
            for (int row = 0; row < Board.Rows; row++)
            {
                DrawCircle(CellToLocal(column, row), CellSize * 0.42f, new Color("0e1730"));
            }
        }

        // Jammed columns: hazard shutter with warning stripes across the chute.
        for (int column = 0; column < Board.Columns; column++)
        {
            if (!_jammed[column])
            {
                continue;
            }

            var rect = new Rect2(column * CellSize, 0f, CellSize, boardSize.Y);
            DrawRect(rect, new Color(0.08f, 0.06f, 0.04f, 0.78f));
            var stripe = new Color("d8a022");
            for (float y = -CellSize; y < boardSize.Y; y += CellSize * 0.55f)
            {
                DrawLine(
                    new Vector2(rect.Position.X + 6f, y),
                    new Vector2(rect.End.X - 6f, y + CellSize * 0.55f),
                    stripe,
                    5f);
            }
        }
    }

    /// <summary>Marks columns sealed by the Sector hazard (visual + hover only).</summary>
    public void SetJammedColumns(IEnumerable<int> columns)
    {
        System.Array.Clear(_jammed, 0, _jammed.Length);
        foreach (int column in columns)
        {
            _jammed[column] = true;
        }

        QueueRedraw();
    }

    /// <summary>Clears every token view for a fresh Quota board.</summary>
    public void ResetBoard()
    {
        for (int column = 0; column < Board.Columns; column++)
        {
            for (int row = 0; row < Board.Rows; row++)
            {
                _views[column, row]?.QueueFree();
                _views[column, row] = null;
            }
        }
    }

    /// <summary>Spawns a token view in place, without animation (hazard seeding).</summary>
    public void SpawnStatic(Token token, int column, int row)
    {
        if (TokenScene is null)
        {
            return;
        }

        var view = TokenScene.Instantiate<TokenView>();
        view.SetToken(token);
        view.Position = CellToLocal(column, row);
        _tokenLayer.AddChild(view);
        _views[column, row] = view;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_animating)
        {
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            int column = ColumnAt(GetLocalMousePosition());
            if (column >= 0 && column != _hoverColumn)
            {
                _hoverColumn = column;
                QueueRedraw();
            }
        }
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            int column = ColumnAt(GetLocalMousePosition());
            if (column >= 0)
            {
                EmitSignal(SignalName.ColumnSelected, column);
            }
        }
        else if (@event.IsActionPressed("ui_left"))
        {
            _hoverColumn = Mathf.PosMod(_hoverColumn - 1, Board.Columns);
            QueueRedraw();
        }
        else if (@event.IsActionPressed("ui_right"))
        {
            _hoverColumn = Mathf.PosMod(_hoverColumn + 1, Board.Columns);
            QueueRedraw();
        }
        else if (@event.IsActionPressed("ui_accept"))
        {
            EmitSignal(SignalName.ColumnSelected, _hoverColumn);
        }
    }

    /// <summary>Column under a local-space point, or -1 when outside the board's width.</summary>
    private static int ColumnAt(Vector2 local)
    {
        int column = (int)Mathf.Floor(local.X / CellSize);
        return column >= 0 && column < Board.Columns ? column : -1;
    }

    /// <summary>
    /// Plays a full turn's timeline sequentially. Input is locked for the duration;
    /// each step's removals finish vanishing before its gravity slides start, and
    /// each cascade wave completes before the next begins.
    /// </summary>
    public async Task PlayTurnAsync(TurnResult result)
    {
        _animating = true;
        QueueRedraw();

        foreach (TurnStep step in result.Steps)
        {
            switch (step.Kind)
            {
                case TurnStepKind.Settle:
                    await PlaySettleAsync(step);
                    break;

                case TurnStepKind.ClearWave:
                    SpawnScorePopup(step);
                    await PlayRemovalsAndSlidesAsync(step);
                    await PauseAsync(InterWavePause);
                    break;

                case TurnStepKind.DropEffect:
                case TurnStepKind.GlassShatter:
                    await PlayRemovalsAndSlidesAsync(step);
                    break;
            }
        }

        _animating = false;
        QueueRedraw();
    }

    private async Task PlaySettleAsync(TurnStep step)
    {
        if (TokenScene is null || step.SettledToken is null)
        {
            return;
        }

        var view = TokenScene.Instantiate<TokenView>();
        view.SetToken(step.SettledToken);

        Vector2 target = CellToLocal(step.SettleColumn, step.SettleRow);
        view.Position = new Vector2(target.X, -CellSize * 0.6f);
        _tokenLayer.AddChild(view);
        _views[step.SettleColumn, step.SettleRow] = view;

        float fallRows = (target.Y - view.Position.Y) / CellSize;
        Tween tween = CreateTween();
        tween.TweenProperty(view, "position:y", target.Y, 0.08f + fallRows * SlideDurationPerRow)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        tween.TweenProperty(view, "scale", new Vector2(1.15f, 0.85f), 0.05f);
        tween.TweenProperty(view, "scale", Vector2.One, 0.09f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>Vanish the step's removed tokens, then slide the survivors down.</summary>
    private async Task PlayRemovalsAndSlidesAsync(TurnStep step)
    {
        if (step.RemovedCells.Count > 0)
        {
            var doomed = new List<TokenView>();
            foreach ((int column, int row) in step.RemovedCells)
            {
                TokenView? view = _views[column, row];
                if (view is null)
                {
                    continue;
                }

                _views[column, row] = null;
                doomed.Add(view);
            }

            if (doomed.Count > 0)
            {
                Tween tween = CreateTween().SetParallel();
                foreach (TokenView view in doomed)
                {
                    tween.TweenProperty(view, "scale", Vector2.Zero, VanishDuration)
                        .SetTrans(Tween.TransitionType.Back)
                        .SetEase(Tween.EaseType.In);
                    tween.TweenProperty(view, "modulate:a", 0f, VanishDuration);
                }

                await ToSignal(tween, Tween.SignalName.Finished);
                foreach (TokenView view in doomed)
                {
                    view.QueueFree();
                }
            }
        }

        if (step.Moves.Count > 0)
        {
            // Two passes: lift every mover off the mirror grid before writing the
            // destinations, since a destination cell may be another move's source.
            var moving = new List<(TokenView View, TokenMove Move)>();
            foreach (TokenMove move in step.Moves)
            {
                TokenView? view = _views[move.FromColumn, move.FromRow];
                if (view is null)
                {
                    continue;
                }

                _views[move.FromColumn, move.FromRow] = null;
                moving.Add((view, move));
            }

            if (moving.Count > 0)
            {
                Tween tween = CreateTween().SetParallel();
                foreach ((TokenView view, TokenMove move) in moving)
                {
                    _views[move.ToColumn, move.ToRow] = view;
                    int rowsFallen = move.FromRow - move.ToRow;
                    tween.TweenProperty(
                            view,
                            "position",
                            CellToLocal(move.ToColumn, move.ToRow),
                            0.05f + rowsFallen * SlideDurationPerRow)
                        .SetTrans(Tween.TransitionType.Bounce)
                        .SetEase(Tween.EaseType.Out);
                }

                await ToSignal(tween, Tween.SignalName.Finished);
            }
        }
    }

    /// <summary>Floating "+score xN" text over the wave's cleared cluster. Fire and forget.</summary>
    private void SpawnScorePopup(TurnStep step)
    {
        if (step.RemovedCells.Count == 0)
        {
            return;
        }

        Vector2 centroid = Vector2.Zero;
        foreach ((int column, int row) in step.RemovedCells)
        {
            centroid += CellToLocal(column, row);
        }

        centroid /= step.RemovedCells.Count;

        var label = new Label
        {
            Text = $"+{step.WaveScore}  x{step.Multiplier:0.##}",
            Position = centroid + new Vector2(-50f, -16f),
            ZIndex = 10,
        };
        label.AddThemeFontSizeOverride("font_size", 30);
        label.AddThemeColorOverride("font_color", new Color("ffd866"));
        label.AddThemeColorOverride("font_outline_color", new Color("1b1b1b"));
        label.AddThemeConstantOverride("outline_size", 8);
        AddChild(label);

        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(label, "position:y", label.Position.Y - 56f, 0.7f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.7f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }

    private async Task PauseAsync(float seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    /// <summary>
    /// Debug guard: warns if the visual mirror has drifted from the engine matrix
    /// after a turn finishes playing.
    /// </summary>
    public void SyncCheck(Board board)
    {
        for (int column = 0; column < Board.Columns; column++)
        {
            for (int row = 0; row < Board.Rows; row++)
            {
                bool engineOccupied = board.GetToken(column, row) is not null;
                bool viewOccupied = _views[column, row] is not null;
                if (engineOccupied != viewOccupied)
                {
                    GD.PushWarning(
                        $"Board desync at ({column},{row}): engine={engineOccupied} view={viewOccupied}");
                }
            }
        }
    }
}
