using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Image> Images => Set<Image>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money everywhere is numeric(12,2) (D012); a narrower precision is set explicitly where needed.
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
    }
}
