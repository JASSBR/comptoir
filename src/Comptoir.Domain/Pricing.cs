namespace Comptoir.Domain;

/// <summary>Customer discount tier. Stored as CHAR(1); compared case-insensitively, as SQL Server's collation does.</summary>
public enum CustomerTier
{
    A,
    B,
    C,
}

public static class CustomerTiers
{
    /// <summary>The legacy procedure treats anything that is not A or B as C (no discount): so does the port.</summary>
    public static CustomerTier FromCode(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "A" => CustomerTier.A,
        "B" => CustomerTier.B,
        _ => CustomerTier.C,
    };
}

/// <summary>Rules signed off by sales management in March 2014 (header of usp_PriceOrder), now explicit and tested.</summary>
public static class PricingPolicy
{
    public const int VolumeThreshold = 100;
    public const decimal VolumeDiscountPct = 3m;
    public const decimal MaxDiscountPct = 12m;
    public const decimal FreeShippingFromHT = 300m;
    public const decimal ShippingFeeHT = 15m;
    public const decimal ShippingVatRate = 0.20m;

    public static decimal TierDiscountPct(CustomerTier tier) => tier switch
    {
        CustomerTier.A => 10m,
        CustomerTier.B => 5m,
        _ => 0m,
    };

    /// <summary>N = normal (20 %), I = intermediate (10 %), R = reduced (5.5 %); anything else: 0, as in the procedure.</summary>
    public static decimal VatRate(string? vatCode) => vatCode?.Trim().ToUpperInvariant() switch
    {
        "N" => 0.20m,
        "I" => 0.10m,
        "R" => 0.055m,
        _ => 0m,
    };
}

public sealed record PricingLine(int Quantity, decimal UnitPrice, string VatCode);

public sealed record PricedLine(decimal UnitPrice, decimal DiscountPct, decimal LineHT, decimal LineVAT);

public sealed record PricedOrder(IReadOnlyList<PricedLine> Lines, decimal TotalHT, decimal ShippingHT, decimal TotalVAT, decimal TotalTTC);

public static class OrderPricing
{
    /// <summary>
    /// Port of dbo.usp_PriceOrder. Two legacy behaviours are kept on purpose because invoices already issued depend
    /// on them (docs/adr/0004-pricing-parity.md):
    /// the volume discount is judged per line, not per product; and VAT is rounded per line, then summed.
    /// </summary>
    public static PricedOrder Price(CustomerTier tier, IReadOnlyList<PricingLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var priced = lines.Select(line => PriceLine(tier, line)).ToList();
        var totalHT = priced.Sum(line => line.LineHT);
        var shippingHT = totalHT < PricingPolicy.FreeShippingFromHT ? PricingPolicy.ShippingFeeHT : 0m;
        var totalVat = priced.Sum(line => line.LineVAT) + SqlRound(shippingHT * PricingPolicy.ShippingVatRate);
        return new PricedOrder(priced, totalHT, shippingHT, totalVat, totalHT + shippingHT + totalVat);
    }

    private static PricedLine PriceLine(CustomerTier tier, PricingLine line)
    {
        var discount = PricingPolicy.TierDiscountPct(tier) + (line.Quantity >= PricingPolicy.VolumeThreshold ? PricingPolicy.VolumeDiscountPct : 0m);
        discount = Math.Min(discount, PricingPolicy.MaxDiscountPct);
        var lineHT = SqlRound(line.Quantity * line.UnitPrice * (100m - discount) / 100m);
        var lineVat = SqlRound(lineHT * PricingPolicy.VatRate(line.VatCode));
        return new PricedLine(line.UnitPrice, discount, lineHT, lineVat);
    }

    /// <summary>T-SQL ROUND(x, 2) on DECIMAL: half away from zero, not .NET's default banker's rounding.</summary>
    public static decimal SqlRound(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
