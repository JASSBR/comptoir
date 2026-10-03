using System.Globalization;

namespace Comptoir.Domain;

/// <summary>The legacy TINYINT codes, kept: both applications read and write the same column during the transition.</summary>
public enum OrderStatus : byte
{
    Draft = 0,
    Confirmed = 1,
    Shipped = 2,
    Invoiced = 3,
    Cancelled = 9,
}

public static class OrderStatuses
{
    /// <summary>The labels the legacy API returns (statusLabel): the AngularJS screens display them as they are.</summary>
    public static string Label(OrderStatus status) => status switch
    {
        OrderStatus.Draft => "Brouillon",
        OrderStatus.Confirmed => "Confirmée",
        OrderStatus.Shipped => "Expédiée",
        OrderStatus.Invoiced => "Facturée",
        OrderStatus.Cancelled => "Annulée",
        _ => "?",
    };
}

public sealed record StockLine(int ProductId, int Quantity);

public sealed record StockLevel(int ProductId, int OnHand, int Reserved)
{
    public int Available => OnHand - Reserved;
}

public static class Stock
{
    /// <summary>
    /// Products an order cannot be served from. Quantities are summed per product first, as usp_ConfirmOrder does:
    /// two lines of 30 on a product with 50 available are a shortage.
    /// </summary>
    public static IReadOnlyList<int> Shortages(IEnumerable<StockLine> lines, IReadOnlyDictionary<int, StockLevel> levels) =>
        [.. lines.GroupBy(line => line.ProductId)
            .Where(group => !levels.TryGetValue(group.Key, out var level) || level.Available < group.Sum(line => line.Quantity))
            .Select(group => group.Key)
            .Order()];

    public static IReadOnlyDictionary<int, int> QuantitiesPerProduct(IEnumerable<StockLine> lines) =>
        lines.GroupBy(line => line.ProductId).ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
}

public static class DocumentNumbers
{
    /// <summary>"CMD-2026-00042", "FA-2026-00007": the format customers and accounting already know.</summary>
    public static string Format(string prefix, int year, int value) =>
        string.Create(CultureInfo.InvariantCulture, $"{prefix}-{year:D4}-{value:D5}");
}

/// <summary>The legacy procedures' messages, word for word: the AngularJS screens show them to users as they are.</summary>
public static class LegacyMessages
{
    public const string NotADraft = "Commande introuvable ou deja confirmee.";
    public const string EmptyOrder = "Commande vide.";
    public const string OutOfStock = "Stock insuffisant.";
    public const string OnlyDraftEditable = "Seul un brouillon est modifiable.";
    public const string InvalidQuantity = "Quantite invalide.";
    public const string UnknownCustomer = "Client inconnu.";
    public const string UnknownProduct = "Article inconnu.";
}
