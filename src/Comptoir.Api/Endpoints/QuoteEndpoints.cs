using Comptoir.Api.Data;
using Comptoir.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comptoir.Api.Endpoints;

public sealed record QuoteRequest(int CustomerId, IReadOnlyList<OrderLineInput>? Lines);

/// <summary>New in v2, impossible in the legacy (pricing only existed as an UPDATE on saved rows): price without saving.</summary>
internal static class QuoteEndpoints
{
    public static void Map(IEndpointRouteBuilder api) =>
        api.MapPost("/v2/quotes", QuoteAsync).WithTags("Quotes").WithSummary("Price an order without saving it");

    private static async Task<IResult> QuoteAsync(QuoteRequest request, ComptoirDb db, CancellationToken cancellationToken)
    {
        var lines = request.Lines ?? [];
        if (lines.Any(l => l.Quantity <= 0))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["lines"] = [LegacyMessages.InvalidQuantity] });
        }

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["customerId"] = [LegacyMessages.UnknownCustomer] });
        }

        var ids = lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.ProductId)).ToDictionaryAsync(p => p.ProductId, cancellationToken);
        if (products.Count != ids.Count)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["lines"] = [LegacyMessages.UnknownProduct] });
        }

        var tier = CustomerTiers.FromCode(customer.Tier);
        var priced = OrderPricing.Price(tier, [.. lines.Select(l => new PricingLine(l.Quantity, products[l.ProductId].UnitPrice, products[l.ProductId].VatCode))]);
        var shortages = Stock.Shortages(
            lines.Select(l => new StockLine(l.ProductId, l.Quantity)),
            products.Values.ToDictionary(p => p.ProductId, p => new StockLevel(p.ProductId, p.StockQty, p.ReservedQty)));

        return TypedResults.Ok(new
        {
            customer = new { id = customer.CustomerId, name = customer.Name, tier = tier.ToString(), tierDiscountPct = PricingPolicy.TierDiscountPct(tier) },
            lines = lines.Zip(priced.Lines, (input, price) => new
            {
                productId = input.ProductId,
                sku = products[input.ProductId].Sku,
                label = products[input.ProductId].Label,
                quantity = input.Quantity,
                price.UnitPrice,
                price.DiscountPct,
                price.LineHT,
                price.LineVAT,
                volumeDiscount = input.Quantity >= PricingPolicy.VolumeThreshold,
            }),
            priced.TotalHT,
            priced.ShippingHT,
            priced.TotalVAT,
            priced.TotalTTC,
            missingForFreeShipping = priced.ShippingHT > 0 ? PricingPolicy.FreeShippingFromHT - priced.TotalHT : 0m,
            outOfStock = shortages,
        });
    }
}
