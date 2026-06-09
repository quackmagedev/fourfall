namespace Fourfall.Core;

/// <summary>
/// A board attachment welded on at the Requisition Terminal. Attachments are
/// permanent for the run and hook into the engine's scoring pipeline.
/// </summary>
public abstract class Attachment
{
    public abstract string Name { get; }

    public abstract string Description { get; }

    /// <summary>Flat bonus points when the dropped token settles at the given cell.</summary>
    public virtual long OnTokenSettled(int column, int row) => 0;

    /// <summary>Score multiplier applied to a clear wave covering the given cells.</summary>
    public virtual double GetWaveMultiplier(IReadOnlyCollection<(int Column, int Row)> clearedCells) => 1.0;
}

/// <summary>+20 points every time a token is dropped down column 4 (index 3).</summary>
public sealed class GoldenChute : Attachment
{
    public const int TargetColumn = 3;
    public const long Bonus = 20;

    public override string Name => "Golden Chute";

    public override string Description => $"+{Bonus} points for dropping in Column {TargetColumn + 1}.";

    public override long OnTokenSettled(int column, int row) =>
        column == TargetColumn ? Bonus : 0;
}

/// <summary>x3 multiplier on any clear wave that includes a bottom-row cell.</summary>
public sealed class HeatedBaseplate : Attachment
{
    public const double Multiplier = 3.0;

    public override string Name => "Heated Baseplate";

    public override string Description => $"x{Multiplier:0} multiplier for matches touching the bottom row.";

    public override double GetWaveMultiplier(IReadOnlyCollection<(int Column, int Row)> clearedCells)
    {
        foreach ((_, int row) in clearedCells)
        {
            if (row == 0)
            {
                return Multiplier;
            }
        }

        return 1.0;
    }
}
