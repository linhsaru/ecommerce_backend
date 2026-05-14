using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Products;

/// <summary>
/// Request tao moi san pham (REST POST body).
/// </summary>
public sealed class CreateProductRequest
{
    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = "";

    [Required]
    [MaxLength(500)]
    public string Slug { get; set; } = "";

    public string? Description { get; set; }
    public int Status { get; set; } = 1;
    public string? ThumbnailUrl { get; set; }
    public Guid? BrandId { get; set; }
    public List<Guid>? CategoryIds { get; set; }

    public CreateProductInitialVariantRequest? InitialVariant { get; set; }
}

public sealed class CreateProductInitialVariantRequest
{
    [Required]
    [MaxLength(100)]
    public string Sku { get; set; } = "";

    [MaxLength(500)]
    public string? VariantName { get; set; }

    public decimal Price { get; set; }

    public decimal? CompareAt { get; set; }

    public decimal? Cost { get; set; }
}
