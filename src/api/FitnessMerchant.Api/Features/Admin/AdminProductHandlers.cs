using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Admin;

public sealed class ListAdminProductsQuery(AppDbContext db)
{
    public async Task<IResult> ExecuteAsync(AdminProductListParameters parameters, CancellationToken cancellationToken)
    {
        var (criteria, errors) = AdminProductCriteria.From(parameters);
        if (criteria is null)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        var products = db.Products.AsNoTracking();
        if (criteria.Search is { } search)
        {
            var pattern = $"%{search.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal)}%";
            products = products.Where(p => EF.Functions.ILike(p.Name, pattern, @"\") || EF.Functions.ILike(p.Sku, pattern, @"\"));
        }
        if (criteria.Category is { } category)
        {
            products = products.Where(p => p.CategoryId == category);
        }
        if (criteria.Brand is { } brand)
        {
            products = products.Where(p => p.BrandId == brand);
        }
        products = criteria.Status switch
        {
            AdminProductStatus.Active => products.Where(p => p.IsActive),
            AdminProductStatus.Archived => products.Where(p => !p.IsActive),
            _ => products,
        };

        var total = await products.CountAsync(cancellationToken);
        var rows = await Sort(products, criteria.Sort)
            .Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize)
            .Select(p => new
            {
                p.Id, p.Sku, p.Name, p.Slug, p.ImageId, CategoryName = p.Category.Name, BrandName = p.Brand.Name,
                p.Price, p.DiscountPercent, SalePrice = Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2),
                p.StockQuantity, p.IsActive, p.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResponse<AdminProductRow>(
            rows.Select(r => new AdminProductRow(r.Id, r.Sku, r.Name, r.Slug, CatalogMapping.ImageUrl(r.ImageId), r.CategoryName, r.BrandName,
                r.Price, r.DiscountPercent, r.SalePrice, r.StockQuantity, r.IsActive, r.UpdatedAt)).ToList(),
            criteria.Page, criteria.PageSize, total));
    }

    /// <summary>Every sort ends with the ID so paging is stable.</summary>
    private static IQueryable<Product> Sort(IQueryable<Product> products, AdminProductSort sort) => sort switch
    {
        AdminProductSort.UpdatedAsc => products.OrderBy(p => p.UpdatedAt).ThenBy(p => p.Id),
        AdminProductSort.NameAsc => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
        AdminProductSort.NameDesc => products.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
        AdminProductSort.SkuAsc => products.OrderBy(p => p.Sku).ThenBy(p => p.Id),
        AdminProductSort.SkuDesc => products.OrderByDescending(p => p.Sku).ThenBy(p => p.Id),
        AdminProductSort.PriceAsc => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
        AdminProductSort.PriceDesc => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
        AdminProductSort.StockAsc => products.OrderBy(p => p.StockQuantity).ThenBy(p => p.Id),
        AdminProductSort.StockDesc => products.OrderByDescending(p => p.StockQuantity).ThenBy(p => p.Id),
        _ => products.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id),
    };
}

public sealed class GetAdminProductQuery(AppDbContext db)
{
    public async Task<AdminProductResponse?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                Product = p,
                SalePrice = Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2),
                HasOrders = db.OrderItems.Any(i => i.ProductId == p.Id),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var p = row.Product;
        return new AdminProductResponse(p.Id, p.Sku, p.Name, p.Slug, p.Description, p.CategoryId, p.BrandId, p.Price, p.DiscountPercent,
            row.SalePrice, p.StockQuantity, p.IsActive, p.ImageId, CatalogMapping.ImageUrl(p.ImageId), row.HasOrders, p.CreatedAt, p.UpdatedAt, p.Version);
    }
}

/// <summary>Rules shared by create and update that need the database.</summary>
internal static class ProductWriteRules
{
    public const int MaxSlugLength = 140;

    /// <summary>Checks the references and unique values; returns the slug to store (generated when none was given).</summary>
    public static async Task<string?> CheckAsync(AppDbContext db, ValidProduct input, Guid? productId, FieldErrors errors, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId, ct))
        {
            errors.Add("categoryId", "This category no longer exists.");
        }
        if (!await db.Brands.AnyAsync(b => b.Id == input.BrandId, ct))
        {
            errors.Add("brandId", "This brand no longer exists.");
        }
        if (input.ImageId is { } imageId && !await db.Images.AnyAsync(i => i.Id == imageId, ct))
        {
            errors.Add("imageId", "This image no longer exists. Upload it again.");
        }
        if (await db.Products.AnyAsync(p => p.Sku == input.Sku && p.Id != productId, ct))
        {
            errors.Add("sku", "Another product already uses this SKU.");
        }

        if (input.Slug is { } chosen)
        {
            if (await db.Products.AnyAsync(p => p.Slug == chosen && p.Id != productId, ct))
            {
                errors.Add("slug", "Another product already uses this slug.");
            }
            return chosen;
        }

        var wanted = Slugs.Slugify(input.Name) is { Length: > 0 } s ? s : "product";
        var taken = await db.Products
            .Where(p => p.Id != productId && (p.Slug == wanted || p.Slug.StartsWith(wanted + "-")))
            .Select(p => p.Slug)
            .ToListAsync(ct);
        return Slugs.NextFree(wanted, taken.ToHashSet(StringComparer.Ordinal), MaxSlugLength);
    }

    /// <summary>A unique index beat the pre-check (two admins saving at once): report it on the field.</summary>
    public static IResult? FromUniqueViolation(DbUpdateException exception) =>
        DbErrors.IsUniqueViolation(exception, out var constraint) && constraint switch
        {
            "ix_products_sku" => "sku",
            "ix_products_slug" => "slug",
            _ => null,
        } is { } field
            ? Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [$"Another product already uses this {field}."] })
            : null;
}

public sealed class CreateProductHandler(AppDbContext db, GetAdminProductQuery get, TimeProvider time)
{
    public async Task<IResult> ExecuteAsync(ProductInput input, CancellationToken cancellationToken)
    {
        var (valid, errors) = AdminValidation.Product(input);
        if (valid is null)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }
        var slug = await ProductWriteRules.CheckAsync(db, valid, productId: null, errors, cancellationToken);
        if (errors.Any)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        var now = time.GetUtcNow();
        var product = new Product
        {
            Id = Guid.CreateVersion7(now),
            Sku = valid.Sku,
            Name = valid.Name,
            Slug = slug!,
            Description = valid.Description,
            CategoryId = valid.CategoryId,
            BrandId = valid.BrandId,
            Price = valid.Price,
            DiscountPercent = valid.DiscountPercent,
            StockQuantity = valid.StockQuantity,
            IsActive = valid.IsActive,
            ImageId = valid.ImageId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Products.Add(product);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ProductWriteRules.FromUniqueViolation(exception) is { } problem)
        {
            return problem;
        }

        return Results.Created($"/api/admin/products/{product.Id}", await get.ExecuteAsync(product.Id, cancellationToken));
    }
}

public sealed class UpdateProductHandler(AppDbContext db, GetAdminProductQuery get, TimeProvider time)
{
    public async Task<IResult> ExecuteAsync(Guid id, ProductInput input, CancellationToken cancellationToken)
    {
        var (valid, errors) = AdminValidation.Product(input);
        if (input.Version is null)
        {
            errors.Add("version", "Reload the product and try again.");
        }
        if (valid is null || errors.Any)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Results.NotFound();
        }

        // Keep the stored slug when the admin cleared the field but the name did not change.
        var slugInput = valid.Slug is null && valid.Name == product.Name ? valid with { Slug = product.Slug } : valid;
        var slug = await ProductWriteRules.CheckAsync(db, slugInput, product.Id, errors, cancellationToken);
        if (errors.Any)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        // The client's version becomes the expected xmin: if anyone saved since the form was loaded, nothing updates.
        db.Entry(product).Property(p => p.Version).OriginalValue = input.Version!.Value;
        product.Sku = valid.Sku;
        product.Name = valid.Name;
        product.Slug = slug!;
        product.Description = valid.Description;
        product.CategoryId = valid.CategoryId;
        product.BrandId = valid.BrandId;
        product.Price = valid.Price;
        product.DiscountPercent = valid.DiscountPercent;
        product.StockQuantity = valid.StockQuantity;
        product.IsActive = valid.IsActive;
        product.ImageId = valid.ImageId;
        product.UpdatedAt = time.GetUtcNow();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AdminProblems.Conflict("concurrency", "Someone else changed this product since you opened it. Reload to see their changes.");
        }
        catch (DbUpdateException exception) when (ProductWriteRules.FromUniqueViolation(exception) is { } problem)
        {
            return problem;
        }

        return Results.Ok(await get.ExecuteAsync(product.Id, cancellationToken));
    }
}

/// <summary>Products that were ever ordered are archived (orders keep pointing at them); others are deleted.</summary>
public sealed class DeleteProductHandler(AppDbContext db, TimeProvider time)
{
    public async Task<IResult> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Results.NotFound();
        }

        if (!await db.OrderItems.AnyAsync(i => i.ProductId == id, cancellationToken))
        {
            db.Products.Remove(product); // cart lines go with it (cascade)
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return Results.Ok(new DeleteProductResponse(DeleteOutcome.Deleted));
            }
            catch (DbUpdateException exception) when (DbErrors.IsForeignKeyViolation(exception))
            {
                // Ordered in the meantime: the restricting foreign key refused the delete, so archive instead.
                db.ChangeTracker.Clear();
                product = await db.Products.SingleAsync(p => p.Id == id, cancellationToken);
            }
        }

        product.IsActive = false;
        product.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new DeleteProductResponse(DeleteOutcome.Archived));
    }
}

internal static class AdminProblems
{
    public static IResult Conflict(string code, string title, IDictionary<string, object?>? extra = null)
    {
        var extensions = new Dictionary<string, object?>(extra ?? new Dictionary<string, object?>()) { ["code"] = code };
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: title, extensions: extensions);
    }
}
