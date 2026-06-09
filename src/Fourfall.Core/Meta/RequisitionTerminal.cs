namespace Fourfall.Core;

/// <summary>One purchasable line item on the Requisition Terminal.</summary>
public abstract class ShopOffer
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required long Cost { get; init; }

    public bool Sold { get; internal set; }

    internal abstract void Apply(RunState state);
}

/// <summary>Adds one upgraded token spec to the player's draw Bag.</summary>
public sealed class TokenOffer : ShopOffer
{
    public required TokenSpec Spec { get; init; }

    internal override void Apply(RunState state) => state.Bag.Add(Spec);
}

/// <summary>Welds a board attachment onto the run (one of each, permanent).</summary>
public sealed class AttachmentOffer : ShopOffer
{
    public required Attachment Attachment { get; init; }

    internal override void Apply(RunState state) => state.Attachments.Add(Attachment);
}

/// <summary>
/// The between-Quota shop. Stock is generated when a Quota clears; purchases spend
/// the run's banked score credits and mutate <see cref="RunState"/> directly.
/// </summary>
public sealed class RequisitionTerminal
{
    private readonly List<ShopOffer> _stock;

    private RequisitionTerminal(List<ShopOffer> stock) => _stock = stock;

    public IReadOnlyList<ShopOffer> Stock => _stock;

    /// <summary>
    /// Builds the terminal's stock: every upgraded token kind at Sector-scaled
    /// prices, plus any attachment not already welded to the board.
    /// </summary>
    public static RequisitionTerminal OpenFor(RunState state)
    {
        // Prices climb 25% per Sector so early credits stay meaningful.
        double sectorScale = 1.0 + 0.25 * (state.Sector - 1);
        static long Scale(long basePrice, double scale) => (long)Math.Round(basePrice * scale / 5.0) * 5;

        var stock = new List<ShopOffer>
        {
            new TokenOffer
            {
                Name = "Iron Token",
                Description = "Crushes the token beneath where it lands, shifting column parity.",
                Cost = Scale(120, sectorScale),
                Spec = new TokenSpec(TokenKind.Iron),
            },
            new TokenOffer
            {
                Name = "Glass Token",
                Description = "Scores x3 when matched, but shatters at end of turn if it survives.",
                Cost = Scale(150, sectorScale),
                Spec = new TokenSpec(TokenKind.Glass),
            },
            new TokenOffer
            {
                Name = "Drain Token",
                Description = "Flushes the bottom token of its column and splices the stack down.",
                Cost = Scale(180, sectorScale),
                Spec = new TokenSpec(TokenKind.Drain),
            },
        };

        if (!state.Attachments.Any(a => a is GoldenChute))
        {
            stock.Add(new AttachmentOffer
            {
                Name = "Golden Chute",
                Description = new GoldenChute().Description,
                Cost = Scale(350, sectorScale),
                Attachment = new GoldenChute(),
            });
        }

        if (!state.Attachments.Any(a => a is HeatedBaseplate))
        {
            stock.Add(new AttachmentOffer
            {
                Name = "Heated Baseplate",
                Description = new HeatedBaseplate().Description,
                Cost = Scale(500, sectorScale),
                Attachment = new HeatedBaseplate(),
            });
        }

        return new RequisitionTerminal(stock);
    }

    /// <summary>Buys the offer at the index. False when sold out or unaffordable.</summary>
    public bool TryPurchase(int index, RunState state)
    {
        if (index < 0 || index >= _stock.Count)
        {
            return false;
        }

        ShopOffer offer = _stock[index];
        if (offer.Sold || state.Credits < offer.Cost)
        {
            return false;
        }

        state.Credits -= offer.Cost;
        offer.Apply(state);
        offer.Sold = true;
        return true;
    }
}
