using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Comptoir.Web.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string Login { get; set; }
        public string DisplayName { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; }
    }

    public class Customer
    {
        public int CustomerId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string Tier { get; set; }
        public bool IsActive { get; set; }
    }

    public class Product
    {
        public int ProductId { get; set; }
        public string Sku { get; set; }
        public string Label { get; set; }
        public string Category { get; set; }
        public decimal UnitPrice { get; set; }
        public string VatCode { get; set; }
        public int StockQty { get; set; }
        public int ReservedQty { get; set; }
        public bool IsActive { get; set; }
    }

    public class Order
    {
        public Order() { Lines = new List<OrderLine>(); }
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public int CustomerId { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? ConfirmedOn { get; set; }
        public DateTime? ShippedOn { get; set; }
        public string Comment { get; set; }
        public decimal? TotalHT { get; set; }
        public decimal? ShippingHT { get; set; }
        public decimal? TotalVAT { get; set; }
        public decimal? TotalTTC { get; set; }
        public virtual ICollection<OrderLine> Lines { get; set; }
    }

    public class OrderLine
    {
        public int OrderLineId { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? DiscountPct { get; set; }
        public decimal? LineHT { get; set; }
        public decimal? LineVAT { get; set; }
    }

    public class Invoice
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public int OrderId { get; set; }
        public DateTime IssuedOn { get; set; }
        public decimal TotalTTC { get; set; }
    }

    // Statuts : 0 brouillon, 1 confirmee, 2 expediee, 3 facturee, 9 annulee
    public static class OrderStatus
    {
        public const byte Brouillon = 0;
        public const byte Confirmee = 1;
        public const byte Expediee = 2;
        public const byte Facturee = 3;
        public const byte Annulee = 9;
    }
}
