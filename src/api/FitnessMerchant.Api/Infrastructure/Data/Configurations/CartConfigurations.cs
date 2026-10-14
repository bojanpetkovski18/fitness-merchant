using FitnessMerchant.Api.Domain.Carts;
using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessMerchant.Api.Infrastructure.Data.Configurations;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => c.UserId).IsUnique();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.CartId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.HasKey(i => new { i.CartId, i.ProductId });
        // A cart line disappears with its product (only possible for products that were never ordered, M09).
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(t => t.HasCheckConstraint("ck_cart_items_quantity", $"quantity BETWEEN 1 AND {CartPricing.MaxLineQuantity}"));
    }
}
