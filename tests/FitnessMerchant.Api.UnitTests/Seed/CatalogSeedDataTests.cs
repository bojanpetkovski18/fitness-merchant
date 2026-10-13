using System.Xml.Linq;
using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Infrastructure.Seed;

namespace FitnessMerchant.Api.UnitTests.Seed;

public class CatalogSeedDataTests
{
    /// <summary>Real sports-nutrition and fitness brands (incl. the ones v1 used). None may appear in seed data (D002).</summary>
    private static readonly string[] RealBrands =
    [
        "Optimum Nutrition", "Gold Standard", "Rogue", "Nike", "Under Armour", "Life Fitness", "MyProtein", "Myprotein",
        "Bulk", "BSN", "MuscleTech", "Dymatize", "Scitec", "Biotech", "Olimp", "Weider", "Reebok", "Adidas", "Puma",
        "Gymshark", "Lululemon", "Technogym", "Concept2", "Peloton", "Eleiko", "Rogue Fitness", "Garmin", "Polar",
        "Bowflex", "Nautilus", "Precor", "Matrix", "Assault", "Echo Bike", "Ghost", "Transparent Labs", "Cellucor", "C4",
    ];

    private static IEnumerable<string> AllSeedText() =>
        CatalogSeedData.Brands.SelectMany(b => new[] { b.Name, b.Description })
            .Concat(CatalogSeedData.Products.SelectMany(p => new[] { p.Name, p.Description, p.Art.Label, p.Art.Sublabel }))
            .Concat(CatalogSeedData.Categories.SelectMany(c => new[] { c.Name, c.Description }));

    [Fact]
    public void Seed_data_contains_no_real_brand_names()
    {
        var hits = AllSeedText()
            .SelectMany(text => RealBrands
                .Where(brand => System.Text.RegularExpressions.Regex.IsMatch(text, $@"\b{System.Text.RegularExpressions.Regex.Escape(brand)}\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .Select(brand => $"'{brand}' in '{text}'"))
            .ToList();

        Assert.Empty(hits);
    }

    [Fact]
    public void Every_product_references_a_known_category_and_brand()
    {
        var categories = CatalogSeedData.Categories.Select(c => c.Slug).ToHashSet();
        var brands = CatalogSeedData.Brands.Select(b => b.Slug).ToHashSet();

        Assert.All(CatalogSeedData.Products, p =>
        {
            Assert.Contains(p.Category, categories);
            Assert.Contains(p.Brand, brands);
        });
    }

    [Fact]
    public void Product_slugs_are_unique_and_values_are_within_business_rules()
    {
        var slugs = CatalogSeedData.Products.Select(p => Slugs.Slugify(p.Name)).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());

        Assert.All(CatalogSeedData.Products, p =>
        {
            Assert.InRange(p.Price, 0.01m, 99_999m);
            Assert.Equal(p.Price, decimal.Round(p.Price, 2));
            Assert.InRange(p.Discount, 0, Pricing.MaxDiscountPercent);
            Assert.True(p.Stock >= 0);
            Assert.InRange(p.Description.Length, 20, 2000);
        });
    }

    [Fact]
    public void Brand_accent_colours_are_lowercase_hex()
    {
        Assert.All(CatalogSeedData.Brands, b => Assert.Matches("^#[0-9a-f]{6}$", b.AccentColor));
    }

    [Fact]
    public void Each_department_has_four_categories_in_display_order()
    {
        foreach (var department in Enum.GetValues<Department>())
        {
            var order = CatalogSeedData.Categories.Where(c => c.Department == department).Select(c => c.SortOrder).ToList();
            Assert.Equal([1, 2, 3, 4], order);
        }
    }

    [Fact]
    public void All_artwork_is_well_formed_svg_without_scripts_styles_or_external_references()
    {
        var svgs = CatalogSeedData.Products
            .Select(p => (p.Name, Svg: Artwork.Product(p.Art, CatalogSeedData.Brands.Single(b => b.Slug == p.Brand).Name, CatalogSeedData.Brands.Single(b => b.Slug == p.Brand).AccentColor)))
            .Concat(CatalogSeedData.Brands.Select(b => (b.Name, Svg: Artwork.BrandLogo(b.Name, b.AccentColor))));

        Assert.All(svgs, item =>
        {
            var document = XDocument.Parse(item.Svg);
            Assert.Equal("svg", document.Root?.Name.LocalName);
            var elements = document.Descendants().Select(e => e.Name.LocalName).ToHashSet();
            Assert.DoesNotContain("script", elements);
            Assert.DoesNotContain("style", elements);
            Assert.DoesNotContain("foreignObject", elements);
            Assert.DoesNotContain(document.Descendants().Attributes(), a => a.Name.LocalName is "href" || a.Name.LocalName.StartsWith("on", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Every_product_has_distinct_artwork()
    {
        // Images are content-addressed, so two products with identical artwork would silently share one image.
        var duplicates = CatalogSeedData.Products
            .GroupBy(p =>
            {
                var brand = CatalogSeedData.Brands.Single(b => b.Slug == p.Brand);
                return Artwork.Product(p.Art, brand.Name, brand.AccentColor);
            })
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" = ", g.Select(p => p.Name)))
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Brand_names_with_special_characters_are_escaped_in_artwork()
    {
        var svg = Artwork.BrandLogo("Fit & <Strong>", "#336699");

        var document = XDocument.Parse(svg);
        Assert.Equal("FIT & <STRONG>", document.Descendants().Single(e => e.Name.LocalName == "text").Value);
    }
}
