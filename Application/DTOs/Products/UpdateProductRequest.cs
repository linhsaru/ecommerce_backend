using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Products;

// Cập nhật giá một biến thể (giá bán / niêm yết / giá nhập).
public sealed class UpdateProductVariantPricingRequest
{
    public Guid VariantId { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAt { get; set; }
    public decimal? Cost { get; set; }
}

// Request cap nhat san pham (REST PUT/PATCH body).
public sealed class UpdateProductRequest
{
    [MaxLength(500)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Slug { get; set; }

    public string? Description { get; set; }
    public int? Status { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid? BrandId { get; set; }
    public List<Guid>? CategoryIds { get; set; }

    // Khi có, cập nhật giá biến thể đã chỉ định (thường là biến thể chính).
    public UpdateProductVariantPricingRequest? VariantPricing { get; set; }
}
