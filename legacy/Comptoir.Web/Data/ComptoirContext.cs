using System.Data.Entity;
using Comptoir.Web.Models;

namespace Comptoir.Web.Data
{
    public class ComptoirContext : DbContext
    {
        static ComptoirContext()
        {
            // Base creee par les scripts SQL (dossier Database), jamais par EF.
            Database.SetInitializer<ComptoirContext>(null);
        }

        public ComptoirContext() : base("name=Comptoir") { }

        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderLine> OrderLines { get; set; }
        public DbSet<Invoice> Invoices { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Customer>().ToTable("Customers");
            modelBuilder.Entity<Product>().ToTable("Products");
            modelBuilder.Entity<Order>().ToTable("Orders");
            modelBuilder.Entity<OrderLine>().ToTable("OrderLines");
            modelBuilder.Entity<Invoice>().ToTable("Invoices");
            modelBuilder.Entity<Order>().HasMany(o => o.Lines).WithRequired().HasForeignKey(l => l.OrderId);
        }
    }
}
