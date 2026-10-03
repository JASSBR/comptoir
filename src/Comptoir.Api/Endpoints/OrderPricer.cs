using Comptoir.Api.Data;
using Comptoir.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comptoir.Api.Endpoints;

internal static class OrderPricer
{
    /// <summary>Writes prices, discounts and totals onto a tracked order, as usp_PriceOrder updates its rows.</summary>
    public static async Task PriceAsync(ComptoirDb db, OrderRow order, CancellationToken cancellationToken)
    {
        var tier = await db.Customers.Where(c => c.CustomerId == order.CustomerId).Select(c => c.Tier).SingleAsync(cancellationToken);
        var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.ProductId)).ToDictionaryAsync(p => p.ProductId, cancellationToken);

        var lines = order.Lines.OrderBy(l => l.OrderLineId).ToList();
        var priced = OrderPricing.Price(
            CustomerTiers.FromCode(tier),
            [.. lines.Select(l => new PricingLine(l.Quantity, products[l.ProductId].UnitPrice, products[l.ProductId].VatCode))]);

        foreach (var (row, price) in lines.Zip(priced.Lines))
        {
            row.UnitPrice = price.UnitPrice;
            row.DiscountPct = price.DiscountPct;
            row.LineHT = price.LineHT;
            row.LineVAT = price.LineVAT;
        }

        order.TotalHT = priced.TotalHT;
        order.ShippingHT = priced.ShippingHT;
        order.TotalVAT = priced.TotalVAT;
        order.TotalTTC = priced.TotalTTC;
    }
}
