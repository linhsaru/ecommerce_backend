using System.Linq;
using System.Threading.Tasks;
using Application.Common;
using Application.DTOs.Products;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

/// <summary>
/// Service CRUD san pham: phan trang, tim theo slug/id, tao/cap nhat/xoa.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly IAppDbContext _db;

    public ProductService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<(List<ProductDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, int? status, CancellationToken cancellationToken = default)
    {
        var query = _db.Products.AsNoTracking().Where(p => p.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || (p.Slug != null && p.Slug.Contains(search)));
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var total = await query.LongCountAsync(cancellationToken);
        var skip = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100);
        var items = await query
            .OrderBy(p => p.Id)
            .Skip((int)skip)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(p => new ProductDto
            {
                Id = p.Id,
                BrandId = p.BrandId,
                Name = p.Name,
                Slug = p.Slug,
                Description = p.Description,
                Status = p.Status,
                ThumbnailUrl = p.ThumbnailUrl,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<(List<ProductDto> Items, long Total)>.Ok((items, total));
    }

    public async Task<Result<ProductDetailDto?>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages.OrderBy(pi => pi.SortOrder))
            .Include(p => p.ProductVariants.Where(v => v.DeletedAt == null))
            .FirstOrDefaultAsync(p => p.Slug == slug && p.DeletedAt == null, cancellationToken);

        if (product == null)
            return Result<ProductDetailDto?>.Fail("NOT_FOUND", "Product not found.");

        return Result<ProductDetailDto?>.Ok(MapToDetail(product));
    }

    public async Task<Result<ProductDetailDto?>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .Include(p => p.ProductImages.OrderBy(pi => pi.SortOrder))
            .Include(p => p.ProductVariants.Where(v => v.DeletedAt == null))
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken);

        if (product == null)
            return Result<ProductDetailDto?>.Fail("NOT_FOUND", "Product not found.");

        return Result<ProductDetailDto?>.Ok(MapToDetail(product));
    }

    public async Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Products.AnyAsync(p => p.Slug == request.Slug && p.DeletedAt == null, cancellationToken);
        if (exists)
            return Result<ProductDto>.Fail("VALIDATION_ERROR", "Slug already exists.");

        var product = new Product
        {
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description,
            Status = request.Status,
            ThumbnailUrl = request.ThumbnailUrl,
            BrandId = request.BrandId
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        if (request.CategoryIds != null && request.CategoryIds.Count > 0)
        {
            foreach (var catId in request.CategoryIds)
                _db.ProductCategories.Add(new ProductCategory { ProductId = product.Id, CategoryId = catId });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result<ProductDto>.Ok(new ProductDto
        {
            Id = product.Id,
            BrandId = product.BrandId,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            Status = product.Status,
            ThumbnailUrl = product.ThumbnailUrl,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        });
    }

    public async Task<Result<ProductDto>> UpdateAsync(long id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products
            .Include(p => p.ProductCategories)
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken);

        if (product == null)
            return Result<ProductDto>.Fail("NOT_FOUND", "Product not found.");

        if (request.Name != null) product.Name = request.Name;
        if (request.Slug != null)
        {
            var exists = await _db.Products.AnyAsync(p => p.Slug == request.Slug && p.Id != id && p.DeletedAt == null, cancellationToken);
            if (exists)
                return Result<ProductDto>.Fail("VALIDATION_ERROR", "Slug already exists.");
            product.Slug = request.Slug;
        }
        if (request.Description != null) product.Description = request.Description;
        if (request.Status.HasValue) product.Status = request.Status.Value;
        if (request.ThumbnailUrl != null) product.ThumbnailUrl = request.ThumbnailUrl;
        if (request.BrandId.HasValue) product.BrandId = request.BrandId;

        if (request.CategoryIds != null)
        {
            _db.ProductCategories.RemoveRange(product.ProductCategories);
            foreach (var catId in request.CategoryIds)
                _db.ProductCategories.Add(new ProductCategory { ProductId = product.Id, CategoryId = catId });
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Result<ProductDto>.Ok(new ProductDto
        {
            Id = product.Id,
            BrandId = product.BrandId,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            Status = product.Status,
            ThumbnailUrl = product.ThumbnailUrl,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        });
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken);
        if (product == null)
            return Result.Fail("NOT_FOUND", "Product not found.");

        product.MarkDeleted(null);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static ProductDetailDto MapToDetail(Product p)
    {
        return new ProductDetailDto
        {
            Id = p.Id,
            BrandId = p.BrandId,
            Name = p.Name,
            Slug = p.Slug,
            Description = p.Description,
            Status = p.Status,
            ThumbnailUrl = p.ThumbnailUrl,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            BrandName = p.Brand?.Name,
            Categories = p.ProductCategories.Select(pc => new ProductCategoryDto
            {
                Id = pc.Category.Id,
                Name = pc.Category.Name,
                Slug = pc.Category.Slug
            }).ToList(),
            Images = p.ProductImages.Select(pi => new ProductImageDto
            {
                Id = pi.Id,
                Url = pi.Url,
                Alt = pi.Alt,
                SortOrder = pi.SortOrder
            }).ToList(),
            Variants = p.ProductVariants.Select(v => new ProductVariantDto
            {
                Id = v.Id,
                Sku = v.Sku,
                VariantName = v.VariantName,
                Price = v.Price,
                CompareAt = v.CompareAt,
                Status = v.Status
            }).ToList()
        };
    }
}
