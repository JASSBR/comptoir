using Comptoir.Api.Data;
using Comptoir.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comptoir.Api.Endpoints;

/// <summary>Same URLs and JSON as the legacy CatalogController: the facade can switch them without touching the UI.</summary>
internal static class CatalogEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/products", ProductsAsync).WithSummary("Active products, by category then label (legacy contract)");
        api.MapGet("/customers", CustomersAsync).WithSummary("Active customers, by name (legacy contract)");
    }

    private static async Task<IResult> ProductsAsync(ComptoirDb db, string? search, string? category, CancellationToken cancellationToken)
    {
        var query = db.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(p => p.Label.Contains(search) || p.Sku.Contains(search));
        }

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(p => p.Category == category);
        }

        var products = await query.OrderBy(p => p.Category).ThenBy(p => p.Label).ToListAsync(cancellationToken);
        return TypedResults.Ok(products.Select(p => new
        {
            id = p.ProductId,
            sku = p.Sku,
            label = p.Label,
            category = p.Category,
            unitPrice = p.UnitPrice,
            vatRate = PricingPolicy.VatRate(p.VatCode) * 100m,
            available = p.StockQty - p.ReservedQty,
        }));
    }

    private static async Task<IResult> CustomersAsync(ComptoirDb db, CancellationToken cancellationToken)
    {
        var customers = await db.Customers.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new { id = c.CustomerId, code = c.Code, name = c.Name, city = c.City, tier = c.Tier })
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(customers);
    }
}
