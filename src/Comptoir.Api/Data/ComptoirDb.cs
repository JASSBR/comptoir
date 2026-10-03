using Microsoft.EntityFrameworkCore;

namespace Comptoir.Api.Data;

// Persistence model of the legacy tables. EF Core maps them and never migrates them: during the transition the
// schema belongs to legacy/Database (ADR 0003). Classes are mutable because EF materializes and tracks them.

public sealed class CustomerRow
{
    public int CustomerId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? City { get; set; }
    public string Tier { get; set; } = "C";
    public bool IsActive { get; set; }
}

public sealed class ProductRow
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Label { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public string VatCode { get; set; } = "N";
    public int StockQty { get; set; }
    public int ReservedQty { get; set; }
    public bool IsActive { get; set; }
}

public sealed class OrderRow
{
    public int OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public int CustomerId { get; set; }
    public byte Status { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ConfirmedOn { get; set; }
    public DateTime? ShippedOn { get; set; }
    public string? Comment { get; set; }
    public decimal? TotalHT { get; set; }
    public decimal? ShippingHT { get; set; }
    public decimal? TotalVAT { get; set; }
    public decimal? TotalTTC { get; set; }
    public ICollection<OrderLineRow> Lines { get; set; } = [];
}

public sealed class OrderLineRow
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

public sealed class InvoiceRow
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public int OrderId { get; set; }
    public DateTime IssuedOn { get; set; }
    public decimal TotalTTC { get; set; }
}

public sealed class ComptoirDb(DbContextOptions<ComptoirDb> options) : DbContext(options)
{
    public DbSet<CustomerRow> Customers => Set<CustomerRow>();
    public DbSet<ProductRow> Products => Set<ProductRow>();
    public DbSet<OrderRow> Orders => Set<OrderRow>();
    public DbSet<OrderLineRow> OrderLines => Set<OrderLineRow>();
    public DbSet<InvoiceRow> Invoices => Set<InvoiceRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerRow>(e =>
        {
            e.ToTable("Customers");
            e.HasKey(c => c.CustomerId);
            e.Property(c => c.Tier).HasColumnType("char(1)");
        });
        modelBuilder.Entity<ProductRow>(e =>
        {
            e.ToTable("Products");
            e.HasKey(p => p.ProductId);
            e.Property(p => p.UnitPrice).HasColumnType("decimal(10,2)");
            e.Property(p => p.VatCode).HasColumnType("char(1)");
        });
        modelBuilder.Entity<OrderRow>(e =>
        {
            e.ToTable("Orders");
            e.HasKey(o => o.OrderId);
            e.Property(o => o.CreatedOn).HasColumnType("datetime");
            e.Property(o => o.ConfirmedOn).HasColumnType("datetime");
            e.Property(o => o.ShippedOn).HasColumnType("datetime");
            e.Property(o => o.TotalHT).HasColumnType("decimal(12,2)");
            e.Property(o => o.ShippingHT).HasColumnType("decimal(10,2)");
            e.Property(o => o.TotalVAT).HasColumnType("decimal(12,2)");
            e.Property(o => o.TotalTTC).HasColumnType("decimal(12,2)");
            e.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId);
        });
        modelBuilder.Entity<OrderLineRow>(e =>
        {
            e.ToTable("OrderLines");
            e.HasKey(l => l.OrderLineId);
            e.Property(l => l.UnitPrice).HasColumnType("decimal(10,2)");
            e.Property(l => l.DiscountPct).HasColumnType("decimal(5,2)");
            e.Property(l => l.LineHT).HasColumnType("decimal(12,2)");
            e.Property(l => l.LineVAT).HasColumnType("decimal(12,2)");
        });
        modelBuilder.Entity<InvoiceRow>(e =>
        {
            e.ToTable("Invoices");
            e.HasKey(i => i.InvoiceId);
            e.Property(i => i.IssuedOn).HasColumnType("datetime");
            e.Property(i => i.TotalTTC).HasColumnType("decimal(12,2)");
        });
    }
}
