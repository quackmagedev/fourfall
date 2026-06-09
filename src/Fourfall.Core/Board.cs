using System.Text;

namespace Fourfall.Core;

/// <summary>
/// 7x6 spatial matrix with Connect Four gravity. Row 0 is the bottom of a column;
/// dropped tokens settle at the lowest empty row. Pure logic, no Godot dependencies.
/// </summary>
public sealed class Board
{
    public const int Columns = 7;
    public const int Rows = 6;

    private readonly Token?[,] _grid = new Token?[Columns, Rows];
    private readonly bool[] _jammed = new bool[Columns];

    public bool IsInside(int column, int row) =>
        column >= 0 && column < Columns && row >= 0 && row < Rows;

    /// <summary>Seals a column shut: no token can fall into it (Sector hazard).</summary>
    public void JamColumn(int column)
    {
        EnsureColumn(column);
        _jammed[column] = true;
    }

    public bool IsColumnJammed(int column)
    {
        EnsureColumn(column);
        return _jammed[column];
    }

    public IEnumerable<int> JammedColumns()
    {
        for (int column = 0; column < Columns; column++)
        {
            if (_jammed[column])
            {
                yield return column;
            }
        }
    }

    /// <summary>True while at least one unjammed column still has room for a drop.</summary>
    public bool HasPlayableColumn()
    {
        for (int column = 0; column < Columns; column++)
        {
            if (!_jammed[column] && _grid[column, Rows - 1] is null)
            {
                return true;
            }
        }

        return false;
    }

    public Token? GetToken(int column, int row)
    {
        EnsureInside(column, row);
        return _grid[column, row];
    }

    public bool IsColumnFull(int column)
    {
        EnsureColumn(column);
        return _grid[column, Rows - 1] is not null;
    }

    /// <summary>Lowest empty row in the column, or -1 if the column is full or jammed.</summary>
    public int GetLandingRow(int column)
    {
        EnsureColumn(column);
        if (_jammed[column])
        {
            return -1;
        }

        for (int row = 0; row < Rows; row++)
        {
            if (_grid[column, row] is null)
            {
                return row;
            }
        }

        return -1;
    }

    public void Place(Token token, int column, int row)
    {
        EnsureInside(column, row);
        if (_grid[column, row] is not null)
        {
            throw new InvalidOperationException($"Cell ({column},{row}) is already occupied.");
        }

        _grid[column, row] = token;
    }

    public Token? RemoveAt(int column, int row)
    {
        EnsureInside(column, row);
        Token? removed = _grid[column, row];
        _grid[column, row] = null;
        return removed;
    }

    /// <summary>
    /// Splices empty slots out of a column: every token above a gap is pulled
    /// downward until the column is contiguous from row 0. Returns tokens moved.
    /// </summary>
    public int CollapseColumn(int column)
    {
        EnsureColumn(column);
        int moved = 0;
        int write = 0;
        for (int read = 0; read < Rows; read++)
        {
            Token? token = _grid[column, read];
            if (token is null)
            {
                continue;
            }

            if (write != read)
            {
                _grid[column, write] = token;
                _grid[column, read] = null;
                moved++;
            }

            write++;
        }

        return moved;
    }

    /// <summary>Applies gravity splicing to every column. Returns total tokens moved.</summary>
    public int ApplyGravity()
    {
        int moved = 0;
        for (int column = 0; column < Columns; column++)
        {
            moved += CollapseColumn(column);
        }

        return moved;
    }

    public IEnumerable<(int Column, int Row, Token Token)> Occupied()
    {
        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
            {
                if (_grid[column, row] is { } token)
                {
                    yield return (column, row, token);
                }
            }
        }
    }

    public int Count()
    {
        int count = 0;
        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
            {
                if (_grid[column, row] is not null)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>ASCII debug view, top row first. Cell = color initial + kind mark.</summary>
    public string Render()
    {
        var sb = new StringBuilder();
        for (int row = Rows - 1; row >= 0; row--)
        {
            for (int column = 0; column < Columns; column++)
            {
                Token? token = _grid[column, row];
                if (token is null)
                {
                    sb.Append(" . ");
                }
                else if (token.Kind == TokenKind.Slag)
                {
                    sb.Append("## ");
                }
                else
                {
                    char color = token.Color.ToString()[0];
                    char kind = token.Kind switch
                    {
                        TokenKind.Iron => 'i',
                        TokenKind.Glass => 'g',
                        TokenKind.Drain => 'd',
                        _ => ' ',
                    };
                    sb.Append(color).Append(kind).Append(' ');
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void EnsureColumn(int column)
    {
        if (column < 0 || column >= Columns)
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, "Column outside board.");
        }
    }

    private void EnsureInside(int column, int row)
    {
        if (!IsInside(column, row))
        {
            throw new ArgumentOutOfRangeException(null, $"Cell ({column},{row}) outside board.");
        }
    }
}
