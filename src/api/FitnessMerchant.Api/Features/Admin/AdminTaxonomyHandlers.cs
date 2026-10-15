using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Admin;

// Categories and brands are short lists (8 and 6 seeded), so they are returned whole, not paged (D026).
// Last write wins for them: only products carry an xmin concurrency check.

public sealed class ListAdminCategoriesQuery(AppDbContext db)
{
    public Task<List<AdminCategoryResponse>> ExecuteAsync(Guid? id, CancellationToken cancellationToken) =>
        db.Categories.AsNoTracking()
            .Where(c => id == null || c.Id == id)
            .OrderBy(c => c.Department).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new AdminCategoryResponse(c.Id, c.Department, c.Name, c.Slug, c.Description, c.SortOrder, c.Products.Count()))
            .ToListAsync(cancellationToken);
}

public sealed class SaveCategoryHandler(AppDbContext db, ListAdminCategoriesQuery query)
{
    private const int MaxSlugLength = 100;

    /// <summary>Create when <paramref name="id"/> is null, otherwise update.</summary>
    public async Task<IResult> ExecuteAsync(Guid? id, CategoryInput input, CancellationToken cancellationToken)
    {
        var (valid, errors) = AdminValidation.Category(input);
        if (valid is null)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        Category? category = null;
        if (id is { } existingId && (category = await db.Categories.SingleOrDefaultAsync(c => c.Id == existingId, cancellationToken)) is null)
        {
            return Results.NotFound();
        }

        var keepSlug = valid.Slug is null && category is not null && category.Name == valid.Name ? category.Slug : valid.Slug;
        var slug = await TaxonomySlugs.ResolveAsync(db.Categories.Where(c => c.Id != id).Select(c => c.Slug), keepSlug, valid.Name, "category", MaxSlugLength, errors, cancellationToken);
        if (errors.Any)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        if (category is null)
        {
            category = new Category { Id = Guid.CreateVersion7(), Name = valid.Name, Slug = slug };
            db.Categories.Add(category);
        }
        category.Department = valid.Department;
        category.Name = valid.Name;
        category.Slug = slug;
        category.Description = valid.Description;
        category.SortOrder = valid.SortOrder;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception, out _))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["slug"] = ["Another category already uses this slug."] });
        }

        var saved = (await query.ExecuteAsync(category.Id, cancellationToken)).Single();
        return id is null ? Results.Created($"/api/admin/categories/{saved.Id}", saved) : Results.Ok(saved);
    }
}

public sealed class DeleteCategoryHandler(AppDbContext db)
{
    public async Task<IResult> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Results.NotFound();
        }
        var productCount = await db.Products.CountAsync(p => p.CategoryId == id, cancellationToken);
        if (productCount > 0)
        {
            return InUse(productCount);
        }

        db.Categories.Remove(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsForeignKeyViolation(exception))
        {
            return InUse(await db.Products.CountAsync(p => p.CategoryId == id, cancellationToken));
        }
        return Results.NoContent();
    }

    private static IResult InUse(int productCount) => AdminProblems.Conflict(
        "in-use",
        $"This category still has {productCount} {(productCount == 1 ? "product" : "products")}. Move or delete them first.",
        new Dictionary<string, object?> { ["productCount"] = productCount });
}

public sealed class ListAdminBrandsQuery(AppDbContext db)
{
    public async Task<List<AdminBrandResponse>> ExecuteAsync(Guid? id, CancellationToken cancellationToken)
    {
        var rows = await db.Brands.AsNoTracking()
            .Where(b => id == null || b.Id == id)
            .OrderBy(b => b.Name)
            .Select(b => new { b.Id, b.Name, b.Slug, b.Description, b.AccentColor, b.LogoImageId, ProductCount = b.Products.Count() })
            .ToListAsync(cancellationToken);
        return rows
            .Select(b => new AdminBrandResponse(b.Id, b.Name, b.Slug, b.Description, b.AccentColor, b.LogoImageId, CatalogMapping.ImageUrl(b.LogoImageId), b.ProductCount))
            .ToList();
    }
}

public sealed class SaveBrandHandler(AppDbContext db, ListAdminBrandsQuery query)
{
    private const int MaxSlugLength = 100;

    /// <summary>Create when <paramref name="id"/> is null, otherwise update.</summary>
    public async Task<IResult> ExecuteAsync(Guid? id, BrandInput input, CancellationToken cancellationToken)
    {
        var (valid, errors) = AdminValidation.Brand(input);
        if (valid is null)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        Brand? brand = null;
        if (id is { } existingId && (brand = await db.Brands.SingleOrDefaultAsync(b => b.Id == existingId, cancellationToken)) is null)
        {
            return Results.NotFound();
        }

        if (await db.Brands.AnyAsync(b => b.Id != id && b.Name == valid.Name, cancellationToken))
        {
            errors.Add("name", "Another brand already has this name.");
        }
        if (valid.LogoImageId is { } logoId && !await db.Images.AnyAsync(i => i.Id == logoId, cancellationToken))
        {
            errors.Add("logoImageId", "This image no longer exists. Upload it again.");
        }
        var keepSlug = valid.Slug is null && brand is not null && brand.Name == valid.Name ? brand.Slug : valid.Slug;
        var slug = await TaxonomySlugs.ResolveAsync(db.Brands.Where(b => b.Id != id).Select(b => b.Slug), keepSlug, valid.Name, "brand", MaxSlugLength, errors, cancellationToken);
        if (errors.Any)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        if (brand is null)
        {
            brand = new Brand { Id = Guid.CreateVersion7(), Name = valid.Name, Slug = slug, Description = valid.Description, AccentColor = valid.AccentColor };
            db.Brands.Add(brand);
        }
        brand.Name = valid.Name;
        brand.Slug = slug;
        brand.Description = valid.Description;
        brand.AccentColor = valid.AccentColor;
        brand.LogoImageId = valid.LogoImageId;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception, out var constraint))
        {
            var field = constraint == "ix_brands_name" ? "name" : "slug";
            return Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [$"Another brand already uses this {field}."] });
        }

        var saved = (await query.ExecuteAsync(brand.Id, cancellationToken)).Single();
        return id is null ? Results.Created($"/api/admin/brands/{saved.Id}", saved) : Results.Ok(saved);
    }
}

public sealed class DeleteBrandHandler(AppDbContext db)
{
    public async Task<IResult> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var brand = await db.Brands.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand is null)
        {
            return Results.NotFound();
        }
        var productCount = await db.Products.CountAsync(p => p.BrandId == id, cancellationToken);
        if (productCount > 0)
        {
            return InUse(productCount);
        }

        db.Brands.Remove(brand);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsForeignKeyViolation(exception))
        {
            return InUse(await db.Products.CountAsync(p => p.BrandId == id, cancellationToken));
        }
        return Results.NoContent();
    }

    private static IResult InUse(int productCount) => AdminProblems.Conflict(
        "in-use",
        $"This brand still has {productCount} {(productCount == 1 ? "product" : "products")}. Move or delete them first.",
        new Dictionary<string, object?> { ["productCount"] = productCount });
}

internal static class TaxonomySlugs
{
    /// <summary>A chosen slug must be free; otherwise one is generated from the name with a numeric suffix if needed.</summary>
    public static async Task<string> ResolveAsync(
        IQueryable<string> otherSlugs, string? chosen, string name, string fallback, int maxLength, FieldErrors errors, CancellationToken ct)
    {
        if (chosen is not null)
        {
            if (await otherSlugs.AnyAsync(s => s == chosen, ct))
            {
                errors.Add("slug", "This slug is already in use.");
            }
            return chosen;
        }

        var wanted = Slugs.Slugify(name) is { Length: > 0 } s ? s : fallback;
        var taken = await otherSlugs.Where(s => s == wanted || s.StartsWith(wanted + "-")).ToListAsync(ct);
        return Slugs.NextFree(wanted, taken.ToHashSet(StringComparer.Ordinal), maxLength);
    }
}
