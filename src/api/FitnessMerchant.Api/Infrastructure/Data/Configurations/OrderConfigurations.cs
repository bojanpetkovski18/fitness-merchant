using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessMerchant.Api.Infrastructure.Data.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public const string NumberSequence = "order_numbers";

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Number).HasMaxLength(20);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(o => o.ContactEmail).HasMaxLength(254);
        builder.Property(o => o.PaymentProvider).HasMaxLength(20);
        builder.Property(o => o.PaymentReference).HasMaxLength(100);
        builder.Property(o => o.CardBrand).HasMaxLength(20);
        builder.Property(o => o.CardLast4).HasMaxLength(4).IsFixedLength();
        builder.Property(o => o.IdempotencyKey).HasMaxLength(64);
        builder.Property(o => o.CancellationReason).HasMaxLength(40);
        builder.Property(o => o.Version).IsRowVersion();

        builder.ComplexProperty(o => o.Shipping, address =>
        {
            address.Property(a => a.FullName).HasColumnName("ship_full_name").HasMaxLength(100);
            address.Property(a => a.Line1).HasColumnName("ship_line1").HasMaxLength(200);
            address.Property(a => a.Line2).HasColumnName("ship_line2").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("ship_city").HasMaxLength(100);
            address.Property(a => a.PostalCode).HasColumnName("ship_postal_code").HasMaxLength(20);
            address.Property(a => a.Country).HasColumnName("ship_country").HasMaxLength(60);
            address.Property(a => a.Phone).HasColumnName("ship_phone").HasMaxLength(30);
        });

        builder.HasIndex(o => o.Number).IsUnique();
        builder.HasIndex(o => new { o.UserId, o.IdempotencyKey }).IsUnique();
        builder.HasIndex(o => new { o.UserId, o.PlacedAt }).IsDescending(false, true);
        builder.HasIndex(o => new { o.Status, o.PlacedAt });

        // Orders are financial records: deleting a user must not silently delete them.
        builder.HasOne<AppUser>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(o => o.StatusChanges).WithOne().HasForeignKey(c => c.OrderId).OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_orders_amounts", "subtotal >= 0 AND shipping_cost >= 0 AND total = subtotal + shipping_cost");
            t.HasCheckConstraint("ck_orders_card_last4", "card_last4 IS NULL OR card_last4 ~ '^[0-9]{4}$'");
        });
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.ProductName).HasMaxLength(120);
        builder.Property(i => i.ProductSlug).HasMaxLength(140);
        builder.Property(i => i.Sku).HasMaxLength(32);
        // A product that was ever ordered cannot be deleted, only archived (SPEC F13).
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_order_items_quantity", "quantity > 0 AND line_total = unit_price * quantity"));
    }
}

internal sealed class OrderStatusChangeConfiguration : IEntityTypeConfiguration<OrderStatusChange>
{
    public void Configure(EntityTypeBuilder<OrderStatusChange> builder)
    {
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Note).HasMaxLength(500);
        builder.HasIndex(c => new { c.OrderId, c.ChangedAt });
    }
}
