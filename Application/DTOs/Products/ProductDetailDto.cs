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
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
}

public sealed class ProductImageDto
{
    public Guid Id { get; init; }
    public string Url { get; init; } = "";
    public string? Alt { get; init; }
    public int SortOrder { get; init; }
}

public sealed class ProductVariantDto
{
    public Guid Id { get; init; }
    public string Sku { get; init; } = "";
    public string? VariantName { get; init; }
    public decimal Price { get; init; }
    public decimal? CompareAt { get; init; }
    public int Status { get; init; }
    //Thông số kỹ thuật của biến thể sản phẩm.
    public List<SpecificationItemDto> Specifications { get; init; } = new();
}

//Thông số kỹ thuật: tên loại thông số, đơn vị, giá trị.
public sealed class SpecificationItemDto
{
    public string Name { get; init; } = "";
    public string? Unit { get; init; }
    public string? Value { get; init; }
}
