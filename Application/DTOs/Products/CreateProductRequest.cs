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
}
