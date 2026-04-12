using System.Linq;
using System.Threading.Tasks;
using Application.Common;
using Application.DTOs.Products;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

/// <summary>
/// Service CRUD san pham: phan trang, tim theo slug/id, tao/cap nhat/xoa.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;

    public ProductService(IProductRepository productRepo, ICategoryRepository categoryRepo)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
    }

    public async Task<Result<(List<ProductDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, int? status, List<Guid>? categoryId, string? categorySlug, CancellationToken cancellationToken = default)
    {
        var query = _productRepo.GetQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || (p.Slug != null && p.Slug.Contains(search)));
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            var slug = NormalizeCategorySlug(categorySlug);
            var category = await _categoryRepo.GetBySlugAsync(slug, cancellationToken);
            if (category == null)
            {
                if (Guid.TryParse(categorySlug, out var categoryIdFromRoute))
                    category = await _categoryRepo.GetByIdAsync(categoryIdFromRoute, cancellationToken);
            }

            if (category == null)
                return Result<(List<ProductDto> Items, long Total)>.Fail("NOT_FOUND", "Category not found.");

            var expandedCategoryIds = await ExpandCategoryIdsAsync(new[] { category.Id }, cancellationToken);
            query = query.Where(p =>
                p.ProductCategories.Any(pc => expandedCategoryIds.Contains(pc.CategoryId)));
        }
        else if (categoryId != null && categoryId.Any())
        {
            var expandedCategoryIds = await ExpandCategoryIdsAsync(categoryId, cancellationToken);
            query = query.Where(p =>
                p.ProductCategories.Any(pc => expandedCategoryIds.Contains(pc.CategoryId)));
        }

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
                BrandName = p.Brand != null ? p.Brand.Name : null,
                Name = p.Name,
                Slug = p.Slug,
                Description = p.Description,
                Status = p.Status,
                ThumbnailUrl = p.ThumbnailUrl,
                // Gia goc (uu tien CompareAt, neu null thi dung Price)
                OriginalPrice = p.ProductVariants
                    .Where(v => v.DeletedAt == null && v.Status == 1)
                    .OrderBy(v => v.Price)
                    .Select(v => (decimal?)Math.Round((v.CompareAt ?? v.Price),2))
                    .FirstOrDefault(),
                // Gia sau giam (chinh la gia hien tai)
                DiscountedPrice = p.ProductVariants
                    .Where(v => v.DeletedAt == null && v.Status == 1)
                    .OrderBy(v => v.Price)
                    .Select(v => (decimal?)Math.Round(v.Price, 2) ?? (decimal?)0)
                    .FirstOrDefault(),
                // Phan tram giam gia (%)
                DiscountPercent = p.ProductVariants
                    .Where(v => v.DeletedAt == null && v.Status == 1)
                    .OrderBy(v => v.Price)
                    .Select(v =>
                        v.CompareAt != null && v.CompareAt > 0 && v.Price < v.CompareAt
                            ? (decimal?)Math.Round(((v.CompareAt.Value - v.Price) / v.CompareAt.Value * 100), 2)
                            : (decimal?)null
                    )
                    .FirstOrDefault(),
                // Danh sach anh san pham
                imageProduct = p.ProductImages
                    .OrderBy(img => img.SortOrder)
                    .Select(img => new ProductImageDto
                    {
                        Id = img.Id,
                        Url = img.Url,
                        Alt = img.Alt,
                        SortOrder = img.SortOrder
                    }),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<(List<ProductDto> Items, long Total)>.Ok((items, total));
    }

    private static string NormalizeCategorySlug(string slug)
        => slug.Trim().ToLowerInvariant();

    private async Task<HashSet<Guid>> ExpandCategoryIdsAsync(
        IEnumerable<Guid> rootCategoryIds,
        CancellationToken cancellationToken)
    {
        var roots = rootCategoryIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToHashSet();

        if (!roots.Any())
            return roots;

        var categories = await _categoryRepo.GetQueryable()
            .Select(c => new { c.Id, c.ParentId })
            .ToListAsync(cancellationToken);

        var childrenByParent = categories
            .Where(c => c.ParentId.HasValue)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var allCategoryIds = new HashSet<Guid>(roots);
        var queue = new Queue<Guid>(roots);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (!childrenByParent.TryGetValue(parentId, out var childIds))
                continue;

            foreach (var childId in childIds)
            {
                if (allCategoryIds.Add(childId))
                    queue.Enqueue(childId);
            }
        }

        return allCategoryIds;
    }

    public async Task<Result<ProductDetailDto?>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var product = await _productRepo.GetBySlugAsync(slug, cancellationToken);

        if (product == null)
            return Result<ProductDetailDto?>.Fail("NOT_FOUND", "Product not found.");

        return Result<ProductDetailDto?>.Ok(MapToDetail(product));
    }

    public async Task<Result<ProductDetailDto?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepo.GetByIdAsync(id, cancellationToken);

        if (product == null)
            return Result<ProductDetailDto?>.Fail("NOT_FOUND", "Product not found.");

        return Result<ProductDetailDto?>.Ok(MapToDetail(product));
    }

    public async Task<Result<List<ProductVariantDto>>> GetVariantsByProductIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        if (!await _productRepo.ExistsByIdAsync(productId, cancellationToken))
            return Result<List<ProductVariantDto>>.Fail("NOT_FOUND", "Product not found.");

        var variants = await _productRepo.GetVariantsByProductIdAsync(productId, cancellationToken);
        var dtos = variants.Select(MapVariantToDto).ToList();
        return Result<List<ProductVariantDto>>.Ok(dtos);
    }

    public async Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await _productRepo.ExistsBySlugAsync(request.Slug, null, cancellationToken);
        if (exists)
            return Result<ProductDto>.Fail("VALIDATION_ERROR", "Slug already exists.");

        if (request.InitialVariant != null)
        {
            var iv = request.InitialVariant;
            if (string.IsNullOrWhiteSpace(iv.Sku))
                return Result<ProductDto>.Fail("VALIDATION_ERROR", "SKU biến thể không được để trống.");
            if (iv.Price <= 0)
                return Result<ProductDto>.Fail("VALIDATION_ERROR", "Giá biến thể phải lớn hơn 0.");
            if (iv.Cost.HasValue && iv.Cost.Value < 0)
                return Result<ProductDto>.Fail("VALIDATION_ERROR", "Giá nhập (cost) không được âm.");
            var sku = iv.Sku.Trim();
            if (await _productRepo.ExistsVariantSkuAsync(sku, cancellationToken))
                return Result<ProductDto>.Fail("VALIDATION_ERROR", "SKU đã tồn tại.");
        }

        var product = new Product
        {
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description,
            Status = request.Status,
            ThumbnailUrl = request.ThumbnailUrl,
            BrandId = request.BrandId
        };
        _productRepo.Add(product);
        await _productRepo.SaveChangesAsync(cancellationToken);

        if (request.CategoryIds != null && request.CategoryIds.Count > 0)
        {
            var productCategories = request.CategoryIds.Select(
                catId => new ProductCategory { ProductId = product.Id, CategoryId = catId }).ToList();
            _productRepo.AddProductCategories(productCategories);
            await _productRepo.SaveChangesAsync(cancellationToken);
        }

        if (request.InitialVariant != null)
        {
            var iv = request.InitialVariant;
            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Sku = iv.Sku.Trim(),
                VariantName = string.IsNullOrWhiteSpace(iv.VariantName) ? null : iv.VariantName.Trim(),
                Price = iv.Price,
                CompareAt = iv.CompareAt,
                Cost = iv.Cost,
                Status = 1
            };
            _productRepo.AddProductVariant(variant);
            await _productRepo.SaveChangesAsync(cancellationToken);
        }

        var reloaded = await _productRepo.GetByIdAsync(product.Id, cancellationToken);
        if (reloaded == null)
            return Result<ProductDto>.Fail("NOT_FOUND", "Không tìm thấy sản phẩm sau khi tạo.");

        return Result<ProductDto>.Ok(MapProductToDto(reloaded));
    }

    public async Task<Result<ProductDto>> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _productRepo.GetByIdAsync(id, cancellationToken);

        if (product == null)
            return Result<ProductDto>.Fail("NOT_FOUND", "Product not found.");

        if (request.Name != null) product.Name = request.Name;
        if (request.Slug != null)
        {
            var exists = await _productRepo.ExistsBySlugAsync(request.Slug, id, cancellationToken);
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
            _productRepo.RemoveProductCategories(product.ProductCategories.ToList());
            var productCategories = request.CategoryIds.Select(
                catId => new ProductCategory { ProductId = product.Id, CategoryId = catId }).ToList();
            _productRepo.AddProductCategories(productCategories);
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;
        await _productRepo.SaveChangesAsync(cancellationToken);

        var reloaded = await _productRepo.GetByIdAsync(id, cancellationToken);
        if (reloaded == null)
            return Result<ProductDto>.Fail("NOT_FOUND", "Không tìm thấy sản phẩm sau khi cập nhật.");

        return Result<ProductDto>.Ok(MapProductToDto(reloaded));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepo.GetByIdAsync(id, cancellationToken);
        if (product == null)
            return Result.Fail("NOT_FOUND", "Product not found.");

        product.MarkDeleted(null);
        await _productRepo.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    
    private static ProductDto MapProductToDto(Product p)
    {
        var activeVariants = (p.ProductVariants ?? Enumerable.Empty<ProductVariant>())
            .Where(v => v.DeletedAt == null && v.Status == 1)
            .OrderBy(v => v.Price)
            .ToList();

        var first = activeVariants.FirstOrDefault();
        decimal? originalPrice = null;
        decimal? discountedPrice = null;
        decimal? discountPercent = null;

        if (first != null)
        {
            originalPrice = (decimal)Math.Round((first.CompareAt ?? first.Price), 2);
            discountedPrice = (decimal)Math.Round(first.Price, 2);
            if (first.CompareAt != null && first.CompareAt > 0 && first.Price < first.CompareAt)
                discountPercent = (decimal)Math.Round(((first.CompareAt.Value - first.Price) / first.CompareAt.Value * 100), 2);
        }

        return new ProductDto
        {
            Id = p.Id,
            BrandId = p.BrandId,
            BrandName = p.Brand?.Name,
            Name = p.Name,
            Slug = p.Slug,
            Description = p.Description,
            Status = p.Status,
            ThumbnailUrl = p.ThumbnailUrl,
            OriginalPrice = originalPrice,
            DiscountedPrice = discountedPrice,
            DiscountPercent = discountPercent,
            imageProduct = (p.ProductImages ?? Enumerable.Empty<ProductImage>())
                .OrderBy(img => img.SortOrder)
                .Select(img => new ProductImageDto
                {
                    Id = img.Id,
                    Url = img.Url,
                    Alt = img.Alt,
                    SortOrder = img.SortOrder
                }),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }

    private static ProductDetailDto MapToDetail(Product p)
    {
        var activeVariants = p.ProductVariants
            .Where(v => v.DeletedAt == null && v.Status == 1)
            .OrderBy(v => v.Price)
            .ToList();

        decimal? discountedPrice = activeVariants.FirstOrDefault()?.Price;
        decimal? originalPrice = activeVariants.FirstOrDefault()?.CompareAt ?? discountedPrice;
        decimal? discountPercent = null;

        if (originalPrice.HasValue && originalPrice.Value > 0 && discountedPrice.HasValue && discountedPrice.Value < originalPrice.Value)
        {
            discountPercent = (originalPrice.Value - discountedPrice.Value) / originalPrice.Value * 100;
        }

        return new ProductDetailDto
        {
            Id = p.Id,
            BrandId = p.BrandId,
            Name = p.Name,
            Slug = p.Slug,
            Description = p.Description,
            Status = p.Status,
            ThumbnailUrl = p.ThumbnailUrl,
            OriginalPrice = originalPrice,
            DiscountedPrice = discountedPrice,
            DiscountPercent = discountPercent,
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
            Variants = p.ProductVariants.Select(MapVariantToDto).ToList()
        };
    }

    private static ProductVariantDto MapVariantToDto(ProductVariant v) => new()
    {
        Id = v.Id,
        Sku = v.Sku,
        VariantName = v.VariantName,
        Price = v.Price,
        CompareAt = v.CompareAt,
        Cost = v.Cost,
        Status = v.Status,
        Specifications = v.ProductVariantSpecifications
            .Select(s => new SpecificationItemDto
            {
                Name = s.SpecificationType.Name,
                Unit = s.SpecificationType.Unit,
                Value = s.Value
            }).ToList()
    };
}
