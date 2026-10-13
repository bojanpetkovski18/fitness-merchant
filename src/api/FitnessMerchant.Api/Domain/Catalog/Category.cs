namespace FitnessMerchant.Api.Domain.Catalog;

public sealed class Category
{
    public Guid Id { get; init; }
    public Department Department { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Product> Products { get; } = [];
}
