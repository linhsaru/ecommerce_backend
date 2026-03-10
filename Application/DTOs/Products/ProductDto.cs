using System;

namespace Application.DTOs.Products;

/// <summary>
/// DTO tra ve thong tin san pham (danh sach hoac chi tiet).
/// </summary>
public class ProductDto
{
    public Guid Id { get; init; }
    public Guid? BrandId { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
    public string? Description { get; init; }
    public int Status { get; init; }
    public string? ThumbnailUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
