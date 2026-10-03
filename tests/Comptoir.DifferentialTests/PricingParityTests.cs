using System.Data;
using Comptoir.Domain;
using CsCheck;
using Microsoft.Data.SqlClient;

namespace Comptoir.DifferentialTests;

/// <summary>
/// Differential test: random orders are priced by the legacy stored procedure (on SQL Server) and by the .NET port;
/// both results must agree to the cent on every line and every total. A mismatch is shrunk by CsCheck to the
/// smallest order that still differs. Samples run one at a time: in parallel they deadlock each other on the shared
/// tables, which says nothing about pricing.
/// </summary>
[Collection(LegacySqlServerGroup.Name)]
public sealed class PricingParityTests(LegacySqlServer sql)
{
    // Tier values seen in the legacy data, lower case and blanks included (imports from the previous system).
    private static readonly Gen<string> Tier = Gen.OneOfConst("A", "B", "C", "a", "b", " ");
    private static readonly Gen<string> VatCode = Gen.OneOfConst("N", "I", "R", "n", "X");

    // Quantities cluster around the 100-unit threshold, prices cover cents that round both ways.
    private static readonly Gen<int> Quantity = Gen.Frequency((3, Gen.Int[1, 120]), (1, Gen.Int[95, 105]), (1, Gen.Int[1, 2_000]));
    private static readonly Gen<decimal> UnitPrice = Gen.Int[1, 99_999].Select(cents => cents / 100m);

    private static readonly Gen<(string Tier, PricingLine[] Lines)> Order =
        Gen.Select(Tier, Gen.Select(Quantity, UnitPrice, VatCode, (q, p, v) => new PricingLine(q, p, v)).Array[0, 8]);

    [Fact]
    public void ThePortPricesEveryOrderExactlyLikeTheStoredProcedure() =>
        Order.Sample(order =>
        {
            var legacy = PriceWithStoredProcedure(order.Tier, order.Lines);
            var port = OrderPricing.Price(CustomerTiers.FromCode(order.Tier), order.Lines);
            return Same(legacy, port);
        }, iter: 1_500, threads: 1, print: order => $"tier '{order.Tier}', lines: {string.Join(", ", order.Lines.Select(l => $"{l.Quantity}×{l.UnitPrice}/{l.VatCode}"))}");

    private static bool Same(PricedOrder legacy, PricedOrder port) =>
        legacy.TotalHT == port.TotalHT && legacy.ShippingHT == port.ShippingHT && legacy.TotalVAT == port.TotalVAT
        && legacy.TotalTTC == port.TotalTTC
        && legacy.Lines.Count == port.Lines.Count
        && legacy.Lines.Zip(port.Lines).All(pair => pair.First == pair.Second);

    private PricedOrder PriceWithStoredProcedure(string tier, PricingLine[] lines)
    {
        using var connection = new SqlConnection(sql.ConnectionString);
        connection.Open();
        // Everything happens in a transaction that is rolled back: samples never see each other.
        using var transaction = connection.BeginTransaction();

        var customerId = Scalar(connection, transaction,
            "INSERT INTO dbo.Customers (Code, Name, Tier) OUTPUT INSERTED.CustomerId VALUES (LEFT(REPLACE(CONVERT(NVARCHAR(36), NEWID()), '-', ''), 10), N'Test', @tier)",
            ("@tier", tier));
        var orderId = Scalar(connection, transaction,
            "INSERT INTO dbo.Orders (CustomerId) OUTPUT INSERTED.OrderId VALUES (@customer)", ("@customer", customerId));
        foreach (var line in lines)
        {
            var productId = Scalar(connection, transaction,
                "INSERT INTO dbo.Products (Sku, Label, Category, UnitPrice, VatCode, StockQty) OUTPUT INSERTED.ProductId " +
                "VALUES (LEFT(REPLACE(CONVERT(NVARCHAR(36), NEWID()), '-', ''), 20), N'Test', N'Test', @price, @vat, 0)",
                ("@price", line.UnitPrice), ("@vat", line.VatCode));
            Scalar(connection, transaction,
                "INSERT INTO dbo.OrderLines (OrderId, ProductId, Quantity) OUTPUT INSERTED.OrderLineId VALUES (@order, @product, @quantity)",
                ("@order", orderId), ("@product", productId), ("@quantity", line.Quantity));
        }

        using (var price = new SqlCommand("dbo.usp_PriceOrder", connection, transaction) { CommandType = CommandType.StoredProcedure })
        {
            price.Parameters.AddWithValue("@OrderId", orderId);
            price.ExecuteNonQuery();
        }

        var priced = new List<PricedLine>();
        using (var read = new SqlCommand("SELECT UnitPrice, DiscountPct, LineHT, LineVAT FROM dbo.OrderLines WHERE OrderId = @order ORDER BY OrderLineId", connection, transaction))
        {
            read.Parameters.AddWithValue("@order", orderId);
            using var reader = read.ExecuteReader();
            while (reader.Read())
            {
                priced.Add(new PricedLine(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3)));
            }
        }

        using var totals = new SqlCommand("SELECT TotalHT, ShippingHT, TotalVAT, TotalTTC FROM dbo.Orders WHERE OrderId = @order", connection, transaction);
        totals.Parameters.AddWithValue("@order", orderId);
        using var row = totals.ExecuteReader();
        row.Read();
        var result = new PricedOrder(priced, row.GetDecimal(0), row.GetDecimal(1), row.GetDecimal(2), row.GetDecimal(3));
        row.Close();
        transaction.Rollback();
        return result;
    }

    private static int Scalar(SqlConnection connection, SqlTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = new SqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (int)command.ExecuteScalar()!;
    }
}
