using System.Collections.Generic;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Danh muc san pham (co the phan cap qua parent_id): name, slug, sort_order.
/// </summary>
public class Category : SoftDeleteEntity<long>
{
    public long? ParentId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public int SortOrder { get; set; }

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
}
