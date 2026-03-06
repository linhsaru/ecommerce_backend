using System.Collections.Generic;
using Domain.Common;
namespace Application.DTOs.Products;

/// <summary>
/// DTO chi tiet san pham (bao gom brand, categories, images, variants).
/// </summary>
public sealed class ProductDetailDto : ProductDto
{
    public string? BrandName { get; init; }
    public List<ProductCategoryDto> Categories { get; init; } = new();
    public List<ProductImageDto> Images { get; init; } = new();
    public List<ProductVariantDto> Variants { get; init; } = new();
}

public sealed class ProductCategoryDto
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
}

public sealed class ProductImageDto
{
    public long Id { get; init; }
    public string Url { get; init; } = "";
    public string? Alt { get; init; }
    public int SortOrder { get; init; }
}

public sealed class ProductVariantDto
{
    public long Id { get; init; }
    public string Sku { get; init; } = "";
    public string? VariantName { get; init; }
    public decimal Price { get; init; }
    public decimal? CompareAt { get; init; }
    public int Status { get; init; }
}
