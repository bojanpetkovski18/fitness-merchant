using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Features.Catalog;

namespace FitnessMerchant.Api.UnitTests.Catalog;

public class ProductListCriteriaTests
{
    [Fact]
    public void Empty_parameters_give_the_defaults()
    {
        var (criteria, errors) = ProductListCriteria.Parse(new ProductListParameters());

        Assert.Empty(errors);
        Assert.Equal(new ProductListCriteria(null, null, [], [], null, null, false, ProductSort.Newest, 1, 24), criteria! with
        {
            Categories = [],
            Brands = [],
        });
        Assert.Empty(criteria.Categories);
        Assert.Empty(criteria.Brands);
    }

    [Fact]
    public void All_valid_parameters_are_parsed()
    {
        var (criteria, errors) = ProductListCriteria.Parse(new ProductListParameters
        {
            Q = "  whey  ",
            Department = "supplements",
            Category = ["protein", "protein", "pre-workout", " "],
            Brand = ["halvard-nutrition"],
            MinPrice = "9.90",
            MaxPrice = "60",
            InStock = "true",
            Sort = "price-desc",
            Page = "3",
            PageSize = "48",
        });

        Assert.Empty(errors);
        Assert.Equal("whey", criteria!.Search);
        Assert.Equal(Department.Supplements, criteria.Department);
        Assert.Equal(["protein", "pre-workout"], criteria.Categories);
        Assert.Equal(["halvard-nutrition"], criteria.Brands);
        Assert.Equal(9.90m, criteria.MinPrice);
        Assert.Equal(60m, criteria.MaxPrice);
        Assert.True(criteria.InStockOnly);
        Assert.Equal(ProductSort.PriceDesc, criteria.Sort);
        Assert.Equal((3, 48), (criteria.Page, criteria.PageSize));
    }

    [Theory]
    [InlineData(null, null, ProductSort.Newest)]
    [InlineData("whey", null, ProductSort.Relevance)]
    [InlineData("whey", "rating", ProductSort.Rating)]
    [InlineData(null, "relevance", ProductSort.Newest)] // relevance without a term means newest
    [InlineData(null, "discount", ProductSort.Discount)]
    public void Default_sort_depends_on_search(string? q, string? sort, ProductSort expected)
    {
        var (criteria, _) = ProductListCriteria.Parse(new ProductListParameters { Q = q, Sort = sort });

        Assert.Equal(expected, criteria!.Sort);
    }

    [Theory]
    [InlineData("sort", "NEWEST")] // keys are exact
    [InlineData("sort", "name")]
    [InlineData("sort", "price_asc")]
    [InlineData("department", "shoes")]
    [InlineData("department", "0")] // enum numeric values are not accepted
    [InlineData("page", "0")]
    [InlineData("page", "-1")]
    [InlineData("page", "1.5")]
    [InlineData("pageSize", "49")]
    [InlineData("minPrice", "-5")]
    [InlineData("minPrice", "1e3")]
    [InlineData("maxPrice", "1000001")]
    [InlineData("inStock", "yes")]
    public void Invalid_values_are_reported_for_their_field(string field, string value)
    {
        var parameters = field switch
        {
            "sort" => new ProductListParameters { Sort = value },
            "department" => new ProductListParameters { Department = value },
            "page" => new ProductListParameters { Page = value },
            "pageSize" => new ProductListParameters { PageSize = value },
            "minPrice" => new ProductListParameters { MinPrice = value },
            "maxPrice" => new ProductListParameters { MaxPrice = value },
            _ => new ProductListParameters { InStock = value },
        };

        var (criteria, errors) = ProductListCriteria.Parse(parameters);

        Assert.Null(criteria);
        Assert.True(errors.ContainsKey(field), $"Expected an error for {field}, got: {string.Join(", ", errors.Keys)}");
    }

    [Fact]
    public void Min_price_above_max_price_is_an_error_on_max_price()
    {
        var (_, errors) = ProductListCriteria.Parse(new ProductListParameters { MinPrice = "50", MaxPrice = "10" });

        Assert.Contains("maxPrice", errors.Keys);
    }

    [Theory]
    [InlineData("Protein")]
    [InlineData("pre workout")]
    [InlineData("-protein")]
    [InlineData("protein--x")]
    [InlineData("'; drop table products;--")]
    public void Category_and_brand_values_must_be_slugs(string value)
    {
        var (_, errors) = ProductListCriteria.Parse(new ProductListParameters { Category = [value], Brand = [value] });

        Assert.Contains("category", errors.Keys);
        Assert.Contains("brand", errors.Keys);
    }

    [Fact]
    public void Too_many_filter_values_or_a_too_long_search_are_rejected()
    {
        var (_, errors) = ProductListCriteria.Parse(new ProductListParameters
        {
            Category = Enumerable.Range(1, 21).Select(i => $"c{i}").ToArray(),
            Q = new string('a', ProductListCriteria.MaxSearchLength + 1),
        });

        Assert.Contains("category", errors.Keys);
        Assert.Contains("q", errors.Keys);
    }

    [Theory]
    [InlineData("100%", @"100\%")]
    [InlineData("a_b", @"a\_b")]
    [InlineData(@"c:\x", @"c:\\x")]
    [InlineData("plain", "plain")]
    public void Like_wildcards_in_search_terms_are_escaped(string input, string expected) =>
        Assert.Equal(expected, ListProductsQuery.EscapeLike(input));
}
