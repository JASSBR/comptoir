using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Comptoir.Api.Tests;

[Collection(ApiGroup.Name)]
public sealed class OrderTests(ApiFactory api)
{
    [Fact]
    public async Task Requests_WithoutAFacadeToken_AreRejected()
    {
        var response = await api.CreateClient().GetAsync("/api/products", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Catalog_KeepsTheLegacyContract()
    {
        var client = api.ClientFor("sophie");
        var products = await client.GetFromJsonAsync<JsonElement>("/api/products?search=Farine", TestContext.Current.CancellationToken);

        var first = products.EnumerateArray().First();
        first.GetProperty("sku").GetString().ShouldStartWith("FAR-");
        first.GetProperty("vatRate").GetDecimal().ShouldBe(5.5m);
        first.TryGetProperty("available", out _).ShouldBeTrue();
        products.EnumerateArray().Select(p => p.GetProperty("category").GetString()).Distinct().ShouldBe(["Farines"]);
    }

    [Fact]
    public async Task CreatedDraft_IsPricedLikeTheLegacy()
    {
        var client = api.ClientFor("sophie");
        var created = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = await IdOfAsync("Customers", "CustomerId", "Code", "PAT003"),
            lines = new[] { new { productId = await IdOfAsync("Products", "ProductId", "Sku", "EMB-BOI-100"), quantity = 100 } },
        }, TestContext.Current.CancellationToken);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetInt32();

        var order = await client.GetFromJsonAsync<JsonElement>($"/api/orders/{id}", TestContext.Current.CancellationToken);

        // Tier A (10 %) + volume (3 %) capped at 12 %: 100 × 37.50 × 0.88 = 3 300.00 HT, VAT 20 % = 660.00.
        var line = order.GetProperty("lines")[0];
        line.GetProperty("discountPct").GetDecimal().ShouldBe(12m);
        line.GetProperty("lineHT").GetDecimal().ShouldBe(3300m);
        order.GetProperty("totalTTC").GetDecimal().ShouldBe(3960m);
        order.GetProperty("createdBy").GetString().ShouldBe("sophie");
        order.GetProperty("statusLabel").GetString().ShouldBe("Brouillon");
    }

    [Fact]
    public async Task Errors_HaveTheShapesTheAngularJsScreensParse()
    {
        var client = api.ClientFor("sophie");

        var empty = await client.PostAsJsonAsync("/api/orders", new { customerId = 1, lines = Array.Empty<object>() }, TestContext.Current.CancellationToken);
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await empty.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("message").GetString().ShouldBe("Commande vide.");

        var invoiced = await IdOfAsync("Orders", "OrderId", "OrderNumber", $"CMD-{DateTime.UtcNow.Year}-00001");
        var conflict = await client.PostAsync($"/api/orders/{invoiced}/confirm", null, TestContext.Current.CancellationToken);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await conflict.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken)).ShouldBe("Commande introuvable ou deja confirmee.");
    }

    [Fact]
    public async Task OrderConfirmedByTheNewCode_IsShippedAndInvoicedByTheLegacyProcedures()
    {
        var client = api.ClientFor("sophie");
        var customer = await IdOfAsync("Customers", "CustomerId", "Code", "BLG005");
        var flour = await IdOfAsync("Products", "ProductId", "Sku", "FAR-SEI-10");
        var before = await StockAsync(flour);

        var created = await client.PostAsJsonAsync("/api/orders", new { customerId = customer, lines = new[] { new { productId = flour, quantity = 7 }, new { productId = flour, quantity = 3 } } }, TestContext.Current.CancellationToken);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetInt32();
        (await client.PostAsync($"/api/orders/{id}/confirm", null, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await StockAsync(flour)).ShouldBe((before.OnHand, before.Reserved + 10));
        var number = (await client.GetFromJsonAsync<JsonElement>($"/api/orders/{id}", TestContext.Current.CancellationToken)).GetProperty("number").GetString();
        number.ShouldNotBeNull().ShouldMatch(@"^CMD-\d{4}-\d{5}$");

        // The rest of the life cycle is still legacy code: it must accept what the new code wrote.
        await ExecAsync("dbo.usp_ShipOrder", id);
        await ExecAsync("dbo.usp_InvoiceOrder", id);
        (await StockAsync(flour)).ShouldBe((before.OnHand - 10, before.Reserved));
        var order = await client.GetFromJsonAsync<JsonElement>($"/api/orders/{id}", TestContext.Current.CancellationToken);
        order.GetProperty("statusLabel").GetString().ShouldBe("Facturée");
        order.GetProperty("invoiceNumber").GetString().ShouldNotBeNull().ShouldMatch(@"^FA-\d{4}-\d{5}$");
    }

    [Fact]
    public async Task Confirmation_ChecksStockPerProduct_AcrossLines()
    {
        var client = api.ClientFor("sophie");
        var thermometer = await IdOfAsync("Products", "ProductId", "Sku", "MAT-THE-1");
        var available = (await StockAsync(thermometer)) is var s ? s.OnHand - s.Reserved : 0;
        var half = (available / 2) + 1;

        var created = await client.PostAsJsonAsync("/api/orders", new { customerId = 1, lines = new[] { new { productId = thermometer, quantity = half }, new { productId = thermometer, quantity = half } } }, TestContext.Current.CancellationToken);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetInt32();

        var response = await client.PostAsync($"/api/orders/{id}/confirm", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken)).ShouldBe("Stock insuffisant.");
        (await StockAsync(thermometer)).ShouldBe(s);
    }

    [Fact]
    public async Task LegacyAndNewConfirmations_RunningTogether_NeverShareANumber()
    {
        var client = api.ClientFor("sophie");
        var salt = await IdOfAsync("Products", "ProductId", "Sku", "SEL-FIN-25");
        var ids = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            var created = await client.PostAsJsonAsync("/api/orders", new { customerId = 2, lines = new[] { new { productId = salt, quantity = 1 } } }, TestContext.Current.CancellationToken);
            ids.Add((await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetInt32());
        }

        // Half confirmed by the legacy procedure, half by the new endpoint, all at once. Concurrent confirmations can
        // deadlock in the legacy design: the new code replays its transaction by itself, a legacy caller has to retry.
        await Task.WhenAll(ids.Select((id, index) => index % 2 == 0
            ? ExecRetryingDeadlocksAsync("dbo.usp_ConfirmOrder", id)
            : client.PostAsync($"/api/orders/{id}/confirm", null, TestContext.Current.CancellationToken)));

        var numbers = new List<string>();
        foreach (var id in ids)
        {
            numbers.Add((await client.GetFromJsonAsync<JsonElement>($"/api/orders/{id}", TestContext.Current.CancellationToken)).GetProperty("number").GetString() ?? "");
        }

        numbers.ShouldAllBe(n => n.StartsWith("CMD-", StringComparison.Ordinal));
        numbers.Distinct().Count().ShouldBe(12);
    }

    private async Task<int> IdOfAsync(string table, string idColumn, string keyColumn, string key)
    {
        await using var connection = new SqlConnection(api.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        // Identifiers come from the test code itself, never from input.
        await using var command = new SqlCommand($"SELECT {idColumn} FROM dbo.{table} WHERE {keyColumn} = @key", connection);
        command.Parameters.AddWithValue("@key", key);
        return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? throw new InvalidOperationException($"No {table} with {keyColumn} = {key}."));
    }

    private async Task<(int OnHand, int Reserved)> StockAsync(int productId)
    {
        await using var connection = new SqlConnection(api.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand("SELECT StockQty, ReservedQty FROM dbo.Products WHERE ProductId = @id", connection);
        command.Parameters.AddWithValue("@id", productId);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private async Task ExecRetryingDeadlocksAsync(string procedure, int orderId)
    {
        const int DeadlockVictim = 1205;
        var attempt = 1;
        while (true)
        {
            try
            {
                await ExecAsync(procedure, orderId);
                return;
            }
            catch (SqlException exception) when (exception.Number == DeadlockVictim && attempt < 5)
            {
                await Task.Delay(50 * attempt++, TestContext.Current.CancellationToken);
            }
        }
    }

    private async Task ExecAsync(string procedure, int orderId)
    {
        await using var connection = new SqlConnection(api.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@OrderId", orderId);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
