using Microsoft.EntityFrameworkCore;
using OrdersUsersApi.Models;
using System.Reflection;

namespace OrdersUsersApi.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderProduct> OrderProducts => Set<OrderProduct>();
        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<OrderProduct>()
                .HasOne(op => op.Order)
                .WithMany(o => o.Products)
                .HasForeignKey(op => op.OrderId);

            modelBuilder.Entity<OrderProduct>()
                .HasOne(op => op.Product)
                .WithMany()
                .HasForeignKey(op => op.ProductId);
        }

        public override int SaveChanges()
        {
            RoundDecimalFields();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            RoundDecimalFields();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void RoundDecimalFields()
        {
            foreach (var entry in ChangeTracker.Entries()
                         .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
            {
                var properties = entry.Entity.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p =>
                        (p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?)) &&
                        p.CanWrite &&
                        p.SetMethod != null &&
                        p.SetMethod.IsPublic
                    );

                foreach (var prop in properties)
                {
                    var value = prop.GetValue(entry.Entity);
                    if (value != null)
                    {
                        var rounded = Math.Round((decimal)value, 0, MidpointRounding.AwayFromZero);
                        prop.SetValue(entry.Entity, rounded);
                    }
                }
            }
        }
    }
}