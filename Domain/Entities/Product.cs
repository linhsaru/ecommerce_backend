using System.Collections.Generic;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// San pham: name, slug, description, status (1 active, 0 draft, -1 hidden), thumbnail_url, brand_id.
/// </summary>
public class Product : SoftDeleteEntity<long>
{
    public long? BrandId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    /// <summary>1 active, 0 draft, -1 hidden</summary>
    public int Status { get; set; } = 1;
    public string? ThumbnailUrl { get; set; }

    public Brand? Brand { get; set; }
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
    public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
}
