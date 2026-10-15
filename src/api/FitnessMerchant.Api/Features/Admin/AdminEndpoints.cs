using System.Security.Claims;
using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Infrastructure.Identity;

namespace FitnessMerchant.Api.Features.Admin;

public static class AdminEndpoints
{
    public static IServiceCollection AddAdminFeature(this IServiceCollection services) => services
        .AddScoped<ListAdminProductsQuery>()
        .AddScoped<GetAdminProductQuery>()
        .AddScoped<CreateProductHandler>()
        .AddScoped<UpdateProductHandler>()
        .AddScoped<DeleteProductHandler>()
        .AddScoped<ListAdminCategoriesQuery>()
        .AddScoped<SaveCategoryHandler>()
        .AddScoped<DeleteCategoryHandler>()
        .AddScoped<ListAdminBrandsQuery>()
        .AddScoped<SaveBrandHandler>()
        .AddScoped<DeleteBrandHandler>()
        .AddScoped<UploadImageHandler>()
        .AddScoped<ListAdminImagesQuery>()
        .AddScoped<ListAdminOrdersQuery>()
        .AddScoped<GetAdminOrderQuery>()
        .AddScoped<ChangeOrderStatusHandler>()
        .AddScoped<ExportOrdersHandler>()
        .AddScoped<GetDashboardQuery>()
        .AddScoped<ListAdminReviewsQuery>()
        .AddScoped<SetReviewVisibilityHandler>();

    public static IServiceCollection AddAdminOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExportOptions>(configuration.GetSection(ExportOptions.Section));
        return services;
    }

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Admin policy on the group: every route below is admin-only, including any added later.
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthSetup.AdminPolicy);

        var products = admin.MapGroup("/products").WithTags("Admin: products");
        products.MapGet("", ([AsParameters] AdminProductListParameters parameters, ListAdminProductsQuery query, CancellationToken ct) =>
            query.ExecuteAsync(parameters, ct));
        products.MapGet("/{id:guid}", async (Guid id, GetAdminProductQuery query, CancellationToken ct) =>
            await query.ExecuteAsync(id, ct) is { } product ? Results.Ok(product) : Results.NotFound());
        products.MapPost("", (ProductInput input, CreateProductHandler handler, CancellationToken ct) => handler.ExecuteAsync(input, ct));
        products.MapPut("/{id:guid}", (Guid id, ProductInput input, UpdateProductHandler handler, CancellationToken ct) =>
            handler.ExecuteAsync(id, input, ct));
        products.MapDelete("/{id:guid}", (Guid id, DeleteProductHandler handler, CancellationToken ct) => handler.ExecuteAsync(id, ct));

        var categories = admin.MapGroup("/categories").WithTags("Admin: categories");
        categories.MapGet("", (ListAdminCategoriesQuery query, CancellationToken ct) => query.ExecuteAsync(null, ct));
        categories.MapGet("/{id:guid}", async (Guid id, ListAdminCategoriesQuery query, CancellationToken ct) =>
            (await query.ExecuteAsync(id, ct)).SingleOrDefault() is { } category ? Results.Ok(category) : Results.NotFound());
        categories.MapPost("", (CategoryInput input, SaveCategoryHandler handler, CancellationToken ct) => handler.ExecuteAsync(null, input, ct));
        categories.MapPut("/{id:guid}", (Guid id, CategoryInput input, SaveCategoryHandler handler, CancellationToken ct) =>
            handler.ExecuteAsync(id, input, ct));
        categories.MapDelete("/{id:guid}", (Guid id, DeleteCategoryHandler handler, CancellationToken ct) => handler.ExecuteAsync(id, ct));

        var brands = admin.MapGroup("/brands").WithTags("Admin: brands");
        brands.MapGet("", (ListAdminBrandsQuery query, CancellationToken ct) => query.ExecuteAsync(null, ct));
        brands.MapGet("/{id:guid}", async (Guid id, ListAdminBrandsQuery query, CancellationToken ct) =>
            (await query.ExecuteAsync(id, ct)).SingleOrDefault() is { } brand ? Results.Ok(brand) : Results.NotFound());
        brands.MapPost("", (BrandInput input, SaveBrandHandler handler, CancellationToken ct) => handler.ExecuteAsync(null, input, ct));
        brands.MapPut("/{id:guid}", (Guid id, BrandInput input, SaveBrandHandler handler, CancellationToken ct) =>
            handler.ExecuteAsync(id, input, ct));
        brands.MapDelete("/{id:guid}", (Guid id, DeleteBrandHandler handler, CancellationToken ct) => handler.ExecuteAsync(id, ct));

        var images = admin.MapGroup("/images").WithTags("Admin: images");
        images.MapGet("", (int? page, ListAdminImagesQuery query, CancellationToken ct) =>
            query.ExecuteAsync(Math.Clamp(page ?? 1, 1, 10_000), ct));
        images.MapPost("", (HttpRequest request, UploadImageHandler handler, CancellationToken ct) => handler.ExecuteAsync(request, ct))
            .RequireRateLimiting(RateLimiting.UploadPolicy);

        var orders = admin.MapGroup("/orders").WithTags("Admin: orders");
        orders.MapGet("", ([AsParameters] AdminOrderListParameters parameters, ListAdminOrdersQuery query, CancellationToken ct) =>
            query.ExecuteAsync(parameters, ct));
        // A literal segment outranks the {number} parameter, so "export" never reads as an order number.
        orders.MapGet("/export", (string? from, string? to, ExportOrdersHandler handler, CancellationToken ct) => handler.ExecuteAsync(from, to, ct))
            .RequireRateLimiting(RateLimiting.ExportPolicy);
        orders.MapGet("/{number}", async (string number, GetAdminOrderQuery query, CancellationToken ct) =>
            await query.ExecuteAsync(number, ct) is { } order ? Results.Ok(order) : Results.NotFound());
        orders.MapPost("/{number}/status", (string number, ChangeStatusRequest request, ClaimsPrincipal user, ChangeOrderStatusHandler handler, CancellationToken ct) =>
            handler.ExecuteAsync(number, user.GetUserId(), request, ct));

        var reviews = admin.MapGroup("/reviews").WithTags("Admin: reviews");
        reviews.MapGet("", (string? status, int? page, ListAdminReviewsQuery query, CancellationToken ct) => query.ExecuteAsync(status, page, ct));
        reviews.MapPut("/{id:guid}/visibility", (Guid id, ReviewVisibilityRequest request, ClaimsPrincipal user, SetReviewVisibilityHandler handler, CancellationToken ct) =>
            handler.ExecuteAsync(id, user.GetUserId(), request, ct));

        admin.MapGet("/dashboard", (string? from, string? to, GetDashboardQuery query, CancellationToken ct) => query.ExecuteAsync(from, to, ct))
            .WithTags("Admin: dashboard");

        return app;
    }
}
