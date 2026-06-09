namespace Fourfall.Core;

/// <summary>
/// Describes how a Sector sabotages the starting board: indestructible slag debris
/// seeded onto the floor, and/or a feed column jammed shut for the whole Sector.
/// Reapplied to a fresh board at the start of every Quota in the Sector.
/// </summary>
public sealed class SectorHazard
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Indestructible slag tokens dropped onto random columns.</summary>
    public int SlagCount { get; init; }

    /// <summary>Column index sealed shut for the Sector, or null.</summary>
    public int? JammedColumn { get; init; }

    public void ApplyTo(Board board, Random rng)
    {
        if (JammedColumn is int jam)
        {
            board.JamColumn(jam);
        }

        for (int i = 0; i < SlagCount; i++)
        {
            // Slag falls like any token so the gravity invariant holds. Capped stack
            // height keeps the hazard from walling off a column outright.
            for (int attempt = 0; attempt < 32; attempt++)
            {
                int column = rng.Next(Board.Columns);
                int row = board.GetLandingRow(column);
                if (row >= 0 && row < Board.Rows - 2)
                {
                    board.Place(new SlagToken(), column, row);
                    break;
                }
            }
        }
    }
}

/// <summary>Escalating hazard schedule, one hazard per Sector.</summary>
public static class HazardCatalog
{
    public static SectorHazard ForSector(int sector, Random rng) => sector switch
    {
        <= 1 => new SectorHazard
        {
            Name = "Intake Bay",
            Description = "Calibration shift. No active hazards.",
        },
        2 => new SectorHazard
        {
            Name = "Slag Spill",
            Description = "3 indestructible slag tokens litter the floor.",
            SlagCount = 3,
        },
        3 => new SectorHazard
        {
            Name = "Jammed Feed",
            Description = "Column 3's chute is jammed shut. Nothing falls in.",
            JammedColumn = 2,
        },
        4 => MakeDeepHazard("Foundry Overflow", 4, rng),
        _ => MakeDeepHazard($"Deep Works {sector - 4}", Math.Min(3 + (sector - 3), 8), rng),
    };

    private static SectorHazard MakeDeepHazard(string name, int slagCount, Random rng)
    {
        int jam = rng.Next(1, Board.Columns - 1);
        return new SectorHazard
        {
            Name = name,
            Description = $"{slagCount} slag tokens on the floor and Column {jam + 1} jammed shut.",
            SlagCount = slagCount,
            JammedColumn = jam,
        };
    }
}
