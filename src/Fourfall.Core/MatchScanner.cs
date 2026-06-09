namespace Fourfall.Core;

/// <summary>
/// Detects 4-in-a-row connections of the same color: horizontal, vertical, both
/// diagonals. Overlapping and intersecting runs union into clusters automatically.
/// </summary>
public static class MatchScanner
{
    public const int MinRun = 4;

    private static readonly (int Dc, int Dr)[] Directions =
    {
        (1, 0),  // horizontal
        (0, 1),  // vertical
        (1, 1),  // diagonal up-right
        (1, -1), // diagonal down-right
    };

    public static HashSet<(int Column, int Row)> FindMatches(Board board)
    {
        var matched = new HashSet<(int Column, int Row)>();

        for (int column = 0; column < Board.Columns; column++)
        {
            for (int row = 0; row < Board.Rows; row++)
            {
                Token? origin = board.GetToken(column, row);
                if (origin is null || !origin.Matchable)
                {
                    continue;
                }

                foreach ((int dc, int dr) in Directions)
                {
                    // Only scan from the start of a run to avoid double-counting.
                    if (Connects(board, column - dc, row - dr, origin.Color))
                    {
                        continue;
                    }

                    int length = 1;
                    while (Connects(board, column + dc * length, row + dr * length, origin.Color))
                    {
                        length++;
                    }

                    if (length >= MinRun)
                    {
                        for (int i = 0; i < length; i++)
                        {
                            matched.Add((column + dc * i, row + dr * i));
                        }
                    }
                }
            }
        }

        return matched;
    }

    /// <summary>A cell continues a run only if its token is matchable and color-equal.</summary>
    private static bool Connects(Board board, int column, int row, TokenColor color) =>
        board.IsInside(column, row) &&
        board.GetToken(column, row) is { Matchable: true } token &&
        token.Color == color;
}
