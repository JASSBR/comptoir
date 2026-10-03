using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Comptoir.Api.Tests;

[Collection(ApiGroup.Name)]
public sealed class QuoteAndDraftTests(ApiFactory api)
{
    [Fact]
    public async Task Quote_PricesWithoutSaving_AndExplainsFreeShippingAndStock()
    {
        var client = api.ClientFor("sophie");
        var orders = await CountOrdersAsync(client);

        // Tier C (Restaurant Le Bouchon), 2 × 34.90 thermometers (20 %), 999 coffee bags: far more than in stock.
        var response = await client.PostAsJsonAsync("/api/v2/quotes", new { customerId = 4, lines = new[] { new { productId = 17, quantity = 2 }, new { productId = 21, quantity = 999 } } }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var quote = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        quote.GetProperty("customer").GetProperty("tierDiscountPct").GetDecimal().ShouldBe(0m);
        quote.GetProperty("lines")[0].GetProperty("lineHT").GetDecimal().ShouldBe(69.80m);
        quote.GetProperty("lines")[1].GetProperty("volumeDiscount").GetBoolean().ShouldBeTrue();
        quote.GetProperty("outOfStock").EnumerateArray().Select(e => e.GetInt32()).ShouldBe([21]);
        quote.GetProperty("missingForFreeShipping").GetDecimal().ShouldBe(0m);
        (await CountOrdersAsync(client)).ShouldBe(orders);
    }

    [Fact]
    public async Task Quote_BelowFreeShipping_SaysHowMuchIsMissing()
    {
        var response = await api.ClientFor("sophie").PostAsJsonAsync("/api/v2/quotes", new { customerId = 4, lines = new[] { new { productId = 17, quantity = 2 } } }, TestContext.Current.CancellationToken);
        var quote = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        quote.GetProperty("shippingHT").GetDecimal().ShouldBe(15m);
        quote.GetProperty("missingForFreeShipping").GetDecimal().ShouldBe(300m - 69.80m);
    }

    [Theory]
    [InlineData(999, 17, 1)]
    [InlineData(4, 99999, 1)]
    [InlineData(4, 17, 0)]
    public async Task Quote_RejectsUnknownCustomersProductsAndQuantities(int customerId, int productId, int quantity)
    {
        var response = await api.ClientFor("sophie").PostAsJsonAsync("/api/v2/quotes", new { customerId, lines = new[] { new { productId, quantity } } }, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DraftLines_CanBeReplaced_AndAreRepriced_UntilConfirmation()
    {
        var client = api.ClientFor("sophie");
        var created = await client.PostAsJsonAsync("/api/orders", new { customerId = 4, lines = new[] { new { productId = 17, quantity = 1 } } }, TestContext.Current.CancellationToken);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetInt32();

        var replaced = await client.PutAsJsonAsync($"/api/orders/{id}/lines", new[] { new { productId = 17, quantity = 3 } }, TestContext.Current.CancellationToken);
        replaced.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await client.GetFromJsonAsync<JsonElement>($"/api/orders/{id}", TestContext.Current.CancellationToken);
        order.GetProperty("lines").GetArrayLength().ShouldBe(1);
        order.GetProperty("totalHT").GetDecimal().ShouldBe(104.70m);

        (await client.PostAsync($"/api/orders/{id}/confirm", null, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var locked = await client.PutAsJsonAsync($"/api/orders/{id}/lines", new[] { new { productId = 17, quantity = 1 } }, TestContext.Current.CancellationToken);
        locked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await locked.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken)).ShouldBe("Seul un brouillon est modifiable.");
    }

    [Fact]
    public async Task Drafts_RejectBadInput_WithTheLegacyMessages()
    {
        var client = api.ClientFor("sophie");

        var unknownCustomer = await client.PostAsJsonAsync("/api/orders", new { customerId = 999, lines = new[] { new { productId = 17, quantity = 1 } } }, TestContext.Current.CancellationToken);
        (await MessageAsync(unknownCustomer)).ShouldBe("Client inconnu.");

        var badQuantity = await client.PostAsJsonAsync("/api/orders", new { customerId = 4, lines = new[] { new { productId = 17, quantity = 0 } } }, TestContext.Current.CancellationToken);
        (await MessageAsync(badQuantity)).ShouldBe("Quantite invalide.");

        var unknownProduct = await client.PutAsJsonAsync("/api/orders/1/lines", new[] { new { productId = 99999, quantity = 1 } }, TestContext.Current.CancellationToken);
        (await MessageAsync(unknownProduct)).ShouldBe("Article inconnu.");

        (await client.GetAsync("/api/orders/999999", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync("/api/orders/999999/lines", new[] { new { productId = 17, quantity = 1 } }, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Lists_FilterByStatus_AndCustomersAreSortedByName()
    {
        var client = api.ClientFor("sophie");

        var invoiced = await client.GetFromJsonAsync<JsonElement>("/api/orders?status=3", TestContext.Current.CancellationToken);
        invoiced.EnumerateArray().ShouldAllBe(o => o.GetProperty("statusLabel").GetString() == "Facturée");

        var customers = await client.GetFromJsonAsync<JsonElement>("/api/customers", TestContext.Current.CancellationToken);
        var names = customers.EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToList();
        names.ShouldBe(names.Order(StringComparer.OrdinalIgnoreCase).ToList());

        var flour = await client.GetFromJsonAsync<JsonElement>("/api/products?category=Farines", TestContext.Current.CancellationToken);
        flour.GetArrayLength().ShouldBe(3);
    }

    private static async Task<int> CountOrdersAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/orders", TestContext.Current.CancellationToken)).GetArrayLength();

    private static async Task<string?> MessageAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("message").GetString();
    }
}
