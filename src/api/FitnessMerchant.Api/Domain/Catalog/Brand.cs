using FitnessMerchant.Api.Domain.Media;

namespace FitnessMerchant.Api.Domain.Catalog;

public sealed class Brand
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Description { get; set; }

    /// <summary>Lowercase <c>#rrggbb</c>; tints the brand's artwork and UI accents.</summary>
    public required string AccentColor { get; set; }

    public Guid? LogoImageId { get; set; }
    public Image? LogoImage { get; set; }

    public ICollection<Product> Products { get; } = [];
}
