using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Products;

/// <summary>
/// Request cap nhat san pham (REST PUT/PATCH body).
/// </summary>
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
}
