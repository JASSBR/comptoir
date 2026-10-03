using System.Data;
using System.Security.Claims;
using Comptoir.Api.Data;
using Comptoir.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Comptoir.Api.Endpoints;

public sealed record OrderLineInput(int ProductId, int Quantity);

public sealed record OrderInput(int CustomerId, string? Comment, IReadOnlyList<OrderLineInput>? Lines);

/// <summary>
/// The legacy OrdersController contract (same routes, same JSON, same messages), reimplemented on the ported domain.
/// Shipping, invoicing and cancellation are not migrated yet: the facade still sends them to the legacy application.
/// </summary>
internal static class OrderEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var orders = api.MapGroup("/orders").WithTags("Orders");
        orders.MapGet("/", ListAsync).WithSummary("Latest 200 orders, optionally by status (legacy contract)");
        orders.MapGet("/{id:int}", GetAsync).WithSummary("An order with its priced lines (legacy contract)");
        orders.MapPost("/", CreateAsync).WithSummary("Create and price a draft (legacy contract)");
        orders.MapPut("/{id:int}/lines", ReplaceLinesAsync).WithSummary("Replace a draft's lines and reprice (legacy contract)");
        orders.MapPost("/{id:int}/confirm", ConfirmAsync).WithSummary("Port of usp_ConfirmOrder: reprice, check and reserve stock, number");
    }

    private static async Task<IResult> ListAsync(ComptoirDb db, byte? status, CancellationToken cancellationToken)
    {
        var query = from o in db.Orders.AsNoTracking()
                    join c in db.Customers on o.CustomerId equals c.CustomerId
                    select new { o, c };
        if (status.HasValue)
        {
            query = query.Where(x => x.o.Status == status.Value);
        }

        var rows = await query.OrderByDescending(x => x.o.OrderId).Take(200).ToListAsync(cancellationToken);
        return TypedResults.Ok(rows.Select(x => new
        {
            id = x.o.OrderId,
            number = x.o.OrderNumber,
            customer = x.c.Name,
            status = x.o.Status,
            statusLabel = OrderStatuses.Label((OrderStatus)x.o.Status),
            createdOn = x.o.CreatedOn,
            totalTTC = x.o.TotalTTC,
        }));
    }

    private static async Task<IResult> GetAsync(int id, ComptoirDb db, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Lines).FirstOrDefaultAsync(o => o.OrderId == id, cancellationToken);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.CustomerId == order.CustomerId, cancellationToken);
        var productIds = order.Lines.Select(l => l.ProductId).ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.ProductId)).ToDictionaryAsync(p => p.ProductId, cancellationToken);
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.OrderId == id, cancellationToken);

        return TypedResults.Ok(new
        {
            id = order.OrderId,
            number = order.OrderNumber,
            customer = new { id = customer.CustomerId, name = customer.Name, tier = customer.Tier },
            status = order.Status,
            statusLabel = OrderStatuses.Label((OrderStatus)order.Status),
            createdOn = order.CreatedOn,
            createdBy = order.CreatedBy,
            comment = order.Comment,
            lines = order.Lines.OrderBy(l => l.OrderLineId).Select(l => new
            {
                productId = l.ProductId,
                sku = products[l.ProductId].Sku,
                label = products[l.ProductId].Label,
                quantity = l.Quantity,
                unitPrice = l.UnitPrice,
                discountPct = l.DiscountPct,
                lineHT = l.LineHT,
                lineVAT = l.LineVAT,
            }),
            totalHT = order.TotalHT,
            shippingHT = order.ShippingHT,
            totalVAT = order.TotalVAT,
            totalTTC = order.TotalTTC,
            invoiceNumber = invoice?.InvoiceNumber,
        });
    }

    private static async Task<IResult> CreateAsync(OrderInput? input, ClaimsPrincipal user, ComptoirDb db, TimeProvider time, CancellationToken cancellationToken)
    {
        if (input?.Lines is not { Count: > 0 } lines)
        {
            return LegacyResults.BadRequest(LegacyMessages.EmptyOrder);
        }

        var invalid = await ValidateLinesAsync(db, lines, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        if (!await db.Customers.AnyAsync(c => c.CustomerId == input.CustomerId, cancellationToken))
        {
            return LegacyResults.BadRequest(LegacyMessages.UnknownCustomer);
        }

        var order = new OrderRow
        {
            CustomerId = input.CustomerId,
            Comment = input.Comment,
            CreatedBy = user.Identity?.Name,
            // The legacy stores server-local DATETIME; both servers run in UTC, so "now" means the same thing.
            CreatedOn = time.GetUtcNow().UtcDateTime,
            Lines = [.. lines.Select(l => new OrderLineRow { ProductId = l.ProductId, Quantity = l.Quantity })],
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        await OrderPricer.PriceAsync(db, order, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"api/orders/{order.OrderId}", new { id = order.OrderId });
    }

    private static async Task<IResult> ReplaceLinesAsync(int id, IReadOnlyList<OrderLineInput>? lines, ComptoirDb db, CancellationToken cancellationToken)
    {
        if (lines is not { Count: > 0 })
        {
            return LegacyResults.BadRequest(LegacyMessages.EmptyOrder);
        }

        var invalid = await ValidateLinesAsync(db, lines, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.OrderId == id, cancellationToken);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (order.Status != (byte)OrderStatus.Draft)
        {
            return LegacyResults.Conflict(LegacyMessages.OnlyDraftEditable);
        }

        db.OrderLines.RemoveRange(order.Lines);
        order.Lines = [.. lines.Select(l => new OrderLineRow { OrderId = id, ProductId = l.ProductId, Quantity = l.Quantity })];
        await db.SaveChangesAsync(cancellationToken);
        await OrderPricer.PriceAsync(db, order, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok();
    }

    private static async Task<IResult> ConfirmAsync(int id, ComptoirDb db, TimeProvider time, CancellationToken cancellationToken)
    {
        // The whole transaction is the retriable unit: a deadlock victim (SQL error 1205) is replayed from the start.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var result = await ConfirmInTransactionAsync(id, db, time, ct);
            if (result is Ok)
            {
                await transaction.CommitAsync(ct);
            }

            return result;
        }, cancellationToken);
    }

    private static async Task<IResult> ConfirmInTransactionAsync(int id, ComptoirDb db, TimeProvider time, CancellationToken cancellationToken)
    {
        // Same lock hints as usp_ConfirmOrder: the legacy and the new code can confirm or ship concurrently.
        var order = await db.Orders.FromSql($"SELECT * FROM dbo.Orders WITH (UPDLOCK) WHERE OrderId = {id}")
            .Include(o => o.Lines).FirstOrDefaultAsync(cancellationToken);
        if (order is null || order.Status != (byte)OrderStatus.Draft)
        {
            return LegacyResults.Conflict(LegacyMessages.NotADraft);
        }

        if (order.Lines.Count == 0)
        {
            return LegacyResults.Conflict(LegacyMessages.EmptyOrder);
        }

        await OrderPricer.PriceAsync(db, order, cancellationToken);

        var stockLines = order.Lines.Select(l => new StockLine(l.ProductId, l.Quantity)).ToList();
        var products = new Dictionary<int, ProductRow>();
        foreach (var productId in stockLines.Select(l => l.ProductId).Distinct().Order())
        {
            // Locked one by one in id order: two confirmations never wait on each other in a cycle.
            products[productId] = await db.Products.FromSql($"SELECT * FROM dbo.Products WITH (UPDLOCK) WHERE ProductId = {productId}")
                .SingleAsync(cancellationToken);
        }

        var levels = products.Values.ToDictionary(p => p.ProductId, p => new StockLevel(p.ProductId, p.StockQty, p.ReservedQty));
        if (Stock.Shortages(stockLines, levels).Count > 0)
        {
            return LegacyResults.Conflict(LegacyMessages.OutOfStock);
        }

        foreach (var (productId, quantity) in Stock.QuantitiesPerProduct(stockLines))
        {
            products[productId].ReservedQty += quantity;
        }

        var now = time.GetUtcNow().UtcDateTime;
        var value = await NextCounterAsync(db, "CMD", now.Year, cancellationToken);
        order.Status = (byte)OrderStatus.Confirmed;
        order.ConfirmedOn = now;
        order.OrderNumber = DocumentNumbers.Format("CMD", now.Year, value);

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok();
    }

    /// <summary>Port of usp_NextCounter, statement for statement: the legacy still numbers invoices with the same table.</summary>
    private static async Task<int> NextCounterAsync(ComptoirDb db, string name, int year, CancellationToken cancellationToken)
    {
        var value = new SqlParameter("@value", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE dbo.Counters WITH (UPDLOCK, HOLDLOCK) SET @value = LastValue = LastValue + 1 WHERE Name = @name AND Year = @year;
            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.Counters (Name, Year, LastValue) VALUES (@name, @year, 1);
                SET @value = 1;
            END
            """,
            [new SqlParameter("@name", name), new SqlParameter("@year", year), value],
            cancellationToken);
        return (int)value.Value;
    }

    private static async Task<IResult?> ValidateLinesAsync(ComptoirDb db, IReadOnlyList<OrderLineInput> lines, CancellationToken cancellationToken)
    {
        if (lines.Any(l => l.Quantity <= 0))
        {
            return LegacyResults.BadRequest(LegacyMessages.InvalidQuantity);
        }

        var ids = lines.Select(l => l.ProductId).Distinct().ToList();
        var known = await db.Products.CountAsync(p => ids.Contains(p.ProductId), cancellationToken);
        // The legacy let SQL Server reject an unknown product with a foreign-key error (HTTP 500); this is a 400.
        return known == ids.Count ? null : LegacyResults.BadRequest(LegacyMessages.UnknownProduct);
    }
}
