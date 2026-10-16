using FitnessMerchant.Api.Domain.Catalog;

namespace FitnessMerchant.Api.Features.Catalog;

public static class CatalogEndpoints
{
    public static IServiceCollection AddCatalogFeature(this IServiceCollection services) => services
        .AddScoped<GetDepartmentsQuery>()
        .AddScoped<ListBrandsQuery>()
        .AddScoped<ListProductsQuery>()
        .AddScoped<GetProductQuery>()
        .AddScoped<GetRelatedProductsQuery>();

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var catalog = app.MapGroup("/api/catalog").WithTags("Catalog").AllowAnonymous();

        catalog.MapGet("/departments", (GetDepartmentsQuery query, CancellationToken ct) => query.ExecuteAsync(ct));

        catalog.MapGet("/brands", async (string? department, ListBrandsQuery query, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(department))
            {
                return Results.Ok(await query.ExecuteAsync(slug: null, ct));
            }
            // Same rule as the product list: a department name, not a number.
            return Enum.TryParse<Department>(department, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) && !department.All(char.IsAsciiDigit)
                ? Results.Ok(await query.ExecuteAsync(slug: null, ct, parsed))
                : Results.ValidationProblem(new Dictionary<string, string[]> { ["department"] = ["Department must be 'supplements' or 'gear'."] });
        });

        catalog.MapGet("/brands/{slug}", async (string slug, ListBrandsQuery query, CancellationToken ct) =>
            (await query.ExecuteAsync(slug, ct)) is [var brand] ? Results.Ok(brand) : Results.NotFound());

        catalog.MapGet("/products", async ([AsParameters] ProductListParameters parameters, ListProductsQuery query, CancellationToken ct) =>
        {
            var (criteria, errors) = ProductListCriteria.Parse(parameters);
            return criteria is null
                ? Results.ValidationProblem(errors)
                : Results.Ok(await query.ExecuteAsync(criteria, ct));
        });

        catalog.MapGet("/products/{slug}", async (string slug, GetProductQuery query, CancellationToken ct) =>
            await query.ExecuteAsync(slug, ct) is { } product ? Results.Ok(product) : Results.NotFound());

        catalog.MapGet("/products/{slug}/related", async (string slug, GetRelatedProductsQuery query, CancellationToken ct) =>
            await query.ExecuteAsync(slug, ct) is { } related ? Results.Ok(related) : Results.NotFound());

        return app;
    }
}
