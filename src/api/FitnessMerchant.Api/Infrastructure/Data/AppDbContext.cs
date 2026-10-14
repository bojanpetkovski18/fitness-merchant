using FitnessMerchant.Api.Domain.Carts;
using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Domain.Media;
using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Infrastructure.Data.Configurations;
using FitnessMerchant.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusChange> OrderStatusChanges => Set<OrderStatusChange>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables first; our configurations rename them
        builder.HasPostgresExtension("pg_trgm");
        builder.HasSequence<long>(OrderConfiguration.NumberSequence);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money everywhere is numeric(12,2) (D012); a narrower precision is set explicitly where needed.
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
    }
}
