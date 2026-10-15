using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Features.Admin;

namespace FitnessMerchant.Api.UnitTests.Admin;

public class AdminValidationTests
{
    private static readonly ProductInput Valid = new(
        "hal-0099", "  Test Straps  ", null, "Invented.", Guid.NewGuid(), Guid.NewGuid(), 19.90m, null, 5, null, null, null);

    [Fact]
    public void A_valid_product_is_normalized()
    {
        var (product, errors) = AdminValidation.Product(Valid);

        Assert.False(errors.Any);
        Assert.Equal(("HAL-0099", "Test Straps", null, 0, true), (product!.Sku, product.Name, product.Slug, product.DiscountPercent, product.IsActive));
    }

    [Theory]
    [InlineData("HAL--1")]
    [InlineData("-HAL")]
    [InlineData("HAL 1")]
    [InlineData("ÄBC")]
    [InlineData("A23456789012345678901234567890123")] // 33 characters
    public void Sku_must_be_letters_digits_and_single_hyphens(string sku) =>
        Assert.Contains("sku", AdminValidation.Product(Valid with { Sku = sku }).Errors.ToDictionary().Keys);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100000)]
    [InlineData(1.005)]
    public void Price_is_positive_bounded_and_in_cents(double price) =>
        Assert.Contains("price", AdminValidation.Product(Valid with { Price = (decimal)price }).Errors.ToDictionary().Keys);

    [Theory]
    [InlineData("Upper-Case")]
    [InlineData("double--hyphen")]
    [InlineData("trailing-")]
    [InlineData("spaces here")]
    public void A_given_slug_must_already_be_slug_shaped(string slug) =>
        Assert.Contains("slug", AdminValidation.Product(Valid with { Slug = slug }).Errors.ToDictionary().Keys);

    [Fact]
    public void An_empty_slug_means_generate_one() =>
        Assert.Null(AdminValidation.Product(Valid with { Slug = "   " }).Product!.Slug);

    [Fact]
    public void Brand_accent_colour_is_normalized_to_lowercase()
    {
        var (brand, errors) = AdminValidation.Brand(new BrandInput("Brand", null, "Text", " #1F6FEB ", null));

        Assert.False(errors.Any);
        Assert.Equal("#1f6feb", brand!.AccentColor);
    }

    [Fact]
    public void Category_needs_a_department_and_a_bounded_sort_order()
    {
        var errors = AdminValidation.Category(new CategoryInput(null, "Ropes", null, null, 5000)).Errors.ToDictionary();

        Assert.Equal(["department", "sortOrder"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Department.Gear, AdminValidation.Category(new CategoryInput(Department.Gear, "Ropes", null, null, null)).Category!.Department);
    }

    [Theory]
    [InlineData(null, "updated-desc", "all", 1, 20)]
    [InlineData("price-asc", "price-asc", "active", 3, 48)]
    public void List_criteria_take_allow_listed_keys_only(string? sort, string expectedSort, string status, int page, int pageSize)
    {
        var (criteria, errors) = AdminProductCriteria.From(new AdminProductListParameters { Sort = sort, Status = status, Page = page, PageSize = pageSize });

        Assert.False(errors.Any);
        Assert.Equal(AdminProductCriteria.SortKeys[expectedSort], criteria!.Sort);
        Assert.Equal((page, pageSize), (criteria.Page, criteria.PageSize));
    }

    [Fact]
    public void Unknown_sort_and_status_and_out_of_range_paging_are_errors()
    {
        var errors = AdminProductCriteria.From(new AdminProductListParameters { Sort = "price", Status = "deleted", Page = 0, PageSize = 49 }).Errors.ToDictionary();

        Assert.Equal(["page", "pageSize", "sort", "status"], errors.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("straps", new string[0], "straps")]
    [InlineData("straps", new[] { "straps" }, "straps-2")]
    [InlineData("straps", new[] { "straps", "straps-2", "straps-3" }, "straps-4")]
    [InlineData("straps", new[] { "straps-2" }, "straps")]
    public void Next_free_slug_adds_the_first_unused_suffix(string wanted, string[] taken, string expected) =>
        Assert.Equal(expected, Slugs.NextFree(wanted, taken, 140));

    [Fact]
    public void Next_free_slug_stays_within_the_length_limit()
    {
        var wanted = new string('a', 9) + "-bbbb"; // 14 characters

        Assert.Equal("aaaaaaaaa-bbbb", Slugs.NextFree(wanted, [], 14));
        Assert.Equal("aaaaaaaaa-bb-2", Slugs.NextFree(wanted, [wanted], 14));
        Assert.Equal("aaaaaaaaa-b", Slugs.NextFree(wanted, [wanted], 11)); // cut to 11, which is free
        Assert.Equal("aaaaaaaaa-2", Slugs.NextFree(wanted, ["aaaaaaaaa-b"], 11)); // the cut base is taken: no "-" left dangling
    }
}
